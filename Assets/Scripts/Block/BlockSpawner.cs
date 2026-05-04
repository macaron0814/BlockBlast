using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class BlockSpawner : MonoBehaviour
    {
        [Header("Settings")]
        public int blocksPerSet = 3;
        public Transform[] spawnPoints;

        [Header("Shape Source")]
        [Tooltip("使用する BlockData アセットをまとめた Registry。\nアサインされていない場合は Resources/BlockShapeRegistry.asset を自動ロードし、それも無ければ allShapes / 静的な BlockShapeLibrary をフォールバックとして使う。")]
        public BlockShapeRegistry shapeRegistry;

        [Header("Runtime")]
        [Tooltip("Registry が無いとき、または Registry が空のときに利用される BlockData リスト。")]
        public List<BlockData> allShapes;
        public List<BlockPiece> currentPieces = new List<BlockPiece>();

        [Header("Prefab")]
        public GameObject blockPiecePrefab;

        [Header("Custom Visuals (optional)")]
        [Tooltip("ブロックセルに使うスプライト画像を入れてください。空の場合は BlockData.cellSprites もしくは自動生成スプライトを使用します。")]
        public Sprite blockCellSprite;

        [Header("Block Cell Tier (CSV「ブロック増加」)")]
        [Tooltip("出現させる形状の最大セル数。3=「デフォ(3ブロックまで)」/ 4=「+1(4まで)」/ 5=「+1(5まで)」/ 0=制限なし(全ブロック解放)")]
        public int currentMaxCells = BlockShapeLibrary.CellTier.Default;

        [Header("Drop-in Animation")]
        [Tooltip("ブロック出現時の落下開始 Y オフセット（着地点からどれだけ上から落とすか）")]
        public float dropStartOffsetY = 2f;
        [Tooltip("落下にかかる時間（秒）")]
        public float dropDuration = 0.3f;
        [Tooltip("各ブロック間の出現遅延（秒）。0 = 同時")]
        public float dropStagger = 0f;

        // 後方互換: 0 以外なら "形状そのものをランダム化する乱数のシード調整" 等で利用可。
        // 既存スクリプトで参照されている SetDifficulty 等は新 API へリダイレクトする。
        float difficultyMultiplier = 1f;

        void Awake()
        {
            ResolveShapeSource();
        }

        /// <summary>
        /// allShapes に使用する BlockData リストを確定させる。
        /// 優先順:
        ///  1. インスペクタで shapeRegistry がアサインされている
        ///  2. Resources/BlockShapeRegistry.asset (自動ロード)
        ///  3. インスペクタで allShapes に直接渡されたリスト
        ///  4. 静的フォールバック (BlockShapeLibrary.GenerateAllShapes)
        /// </summary>
        public void ResolveShapeSource()
        {
            if (shapeRegistry == null)
                shapeRegistry = Resources.Load<BlockShapeRegistry>("BlockShapeRegistry");

            if (shapeRegistry != null)
            {
                var fromRegistry = shapeRegistry.GetShapes();
                if (fromRegistry.Count > 0)
                {
                    allShapes = fromRegistry;
                    return;
                }
            }

            if (allShapes == null || allShapes.Count == 0)
                allShapes = BlockShapeLibrary.GenerateAllShapes();
        }

        public void SpawnNewSet()
        {
            ClearCurrentPieces();

            for (int i = 0; i < blocksPerSet; i++)
            {
                BlockData template = GetRandomBlock();
                if (template == null) continue;

                BlockData cloned = CloneBlockData(template, GetRandomColor());

                Vector3 spawnPos;
                if (spawnPoints != null && i < spawnPoints.Length)
                {
                    spawnPos = spawnPoints[i].position;
                }
                else
                {
                    var bm     = GameManager.Instance?.boardManager;
                    float cX   = bm != null ? (bm.boardWidth  - 1) * 0.5f : 3.5f;
                    float spc  = bm != null ? bm.boardWidth / 3f           : 3f;
                    float sY   = bm != null ? -bm.boardHeight * 0.3f       : -2.5f;
                    spawnPos   = new Vector3(cX + (i - 1) * spc, sY, 0);
                }

                Transform parent = (spawnPoints != null && i < spawnPoints.Length)
                    ? spawnPoints[i] : transform;

                GameObject pieceObj;
                if (blockPiecePrefab != null)
                {
                    pieceObj = Instantiate(blockPiecePrefab, spawnPos, Quaternion.identity, parent);
                }
                else
                {
                    pieceObj = new GameObject($"BlockPiece_{i}");
                    pieceObj.transform.position = spawnPos;
                    pieceObj.transform.SetParent(parent);
                    pieceObj.AddComponent<BlockPiece>();
                }

                BlockPiece piece = pieceObj.GetComponent<BlockPiece>();
                if (piece != null)
                {
                    var tc = GameManager.Instance?.boardManager?.tilemapController;
                    float sx = tc != null ? tc.tileSpacingX : 0.08f;
                    float sy = tc != null ? tc.tileSpacingY : 0.08f;

                    // BlockData.cellSprites または shapeSprite が設定済みなら BlockPiece は
                    // そちらを優先する。何も無いシェイプにのみ blockCellSprite が共通で適用される。
                    bool hasOwnVisual = cloned.HasPerCellSprites() || cloned.shapeSprite != null;
                    Sprite overrideSprite = hasOwnVisual ? null : blockCellSprite;
                    piece.Initialize(cloned, overrideSprite, sx, sy);
                    currentPieces.Add(piece);

                    StartCoroutine(DropIn(piece.transform, spawnPos, i * dropStagger));
                }
            }
        }

        BlockData CloneBlockData(BlockData source, BlockColorType color)
        {
            var clone = ScriptableObject.CreateInstance<BlockData>();
            clone.blockName    = source.blockName;
            clone.shapeWidth   = source.shapeWidth;
            clone.shapeHeight  = source.shapeHeight;
            clone.shapeFlat    = (bool[])source.shapeFlat.Clone();
            clone.colorType    = color;
            clone.designTheme  = source.designTheme;
            clone.shapeSprite  = source.shapeSprite;
            clone.cellSprites  = source.cellSprites != null
                ? (Sprite[])source.cellSprites.Clone()
                : null;
            return clone;
        }

        BlockData GetRandomBlock()
        {
            if (allShapes == null || allShapes.Count == 0) return null;

            int limit = currentMaxCells <= 0 ? int.MaxValue : currentMaxCells;

            // 最大セル数フィルタ。空になった場合は最小サイズの形状にフォールバック
            var pool = new List<BlockData>(allShapes.Count);
            foreach (var s in allShapes)
            {
                if (s == null) continue;
                if (s.GetCellCount() <= limit) pool.Add(s);
            }
            if (pool.Count == 0)
            {
                // どのシェイプも条件を満たさない極端ケース: 最も小さい形状を 1 個だけ返す
                BlockData smallest = null;
                int smallestCells = int.MaxValue;
                foreach (var s in allShapes)
                {
                    if (s == null) continue;
                    int c = s.GetCellCount();
                    if (c < smallestCells) { smallestCells = c; smallest = s; }
                }
                return smallest;
            }
            return pool[Random.Range(0, pool.Count)];
        }

        BlockColorType GetRandomColor()
        {
            var colors = System.Enum.GetValues(typeof(BlockColorType));
            return (BlockColorType)colors.GetValue(Random.Range(0, colors.Length));
        }

        public void ClearCurrentPieces()
        {
            foreach (var piece in currentPieces)
            {
                if (piece != null)
                    Destroy(piece.gameObject);
            }
            currentPieces.Clear();
        }

        public void RemovePiece(BlockPiece piece)
        {
            currentPieces.Remove(piece);
            if (piece != null)
                Destroy(piece.gameObject);
        }

        public bool HasAvailableBlocks()
        {
            return currentPieces.Count > 0;
        }

        public List<BlockData> GetCurrentBlocks()
        {
            var blocks = new List<BlockData>();
            foreach (var piece in currentPieces)
            {
                if (piece != null)
                    blocks.Add(piece.blockData);
            }
            return blocks;
        }

        // ────────────────────────────────────────
        //  Cell Tier API (CSV「ブロック増加」用)
        // ────────────────────────────────────────

        /// <summary>
        /// シェイプの最大セル数を直接指定する。0 = 制限なし(全ブロック解放)。
        /// </summary>
        public void SetMaxCells(int maxCells)
        {
            currentMaxCells = Mathf.Max(0, maxCells);
        }

        /// <summary>
        /// 「ブロック増加+N」イベント用: 最大セル数を delta ぶん引き上げる。
        /// 既に「制限なし」(currentMaxCells == 0) の場合は何もしない。
        /// </summary>
        public void IncreaseMaxCells(int delta)
        {
            if (delta <= 0) return;
            if (currentMaxCells <= 0) return;
            currentMaxCells += delta;
        }

        /// <summary>
        /// 全ブロック解放 (currentMaxCells = 0) に切り替え。
        /// </summary>
        public void UnlockAllShapes()
        {
            currentMaxCells = 0;
        }

        // ────────────────────────────────────────
        //  Backward-compat shims
        // ────────────────────────────────────────

        /// <summary>
        /// (旧API) 既存呼び出し互換。multiplier ≧ 1 を「全解放」、それ以外は「3 セルまで」へ丸める。
        /// 新規コードは <see cref="SetMaxCells"/> を使ってください。
        /// </summary>
        public void SetDifficulty(float multiplier)
        {
            difficultyMultiplier = multiplier;
            if (multiplier >= 1f) UnlockAllShapes();
            else SetMaxCells(BlockShapeLibrary.CellTier.Default);
        }

        /// <summary>
        /// (旧API) 形状追加イベントを最大セル数の引き上げに変換する。
        /// </summary>
        public void IncreaseAvailableShapeCount(int amount)
        {
            IncreaseMaxCells(amount);
        }

        IEnumerator DropIn(Transform target, Vector3 landPos, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Vector3 startPos = landPos + new Vector3(0f, dropStartOffsetY, 0f);
            target.position = startPos;

            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = EaseOutBounce(t);
                target.position = Vector3.LerpUnclamped(startPos, landPos, ease);
                yield return null;
            }
            target.position = landPos;
        }

        static float EaseOutBounce(float t)
        {
            if (t < 1f / 2.75f)
                return 7.5625f * t * t;
            if (t < 2f / 2.75f)
            {
                t -= 1.5f / 2.75f;
                return 7.5625f * t * t + 0.75f;
            }
            if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;
                return 7.5625f * t * t + 0.9375f;
            }
            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }
    }
}
