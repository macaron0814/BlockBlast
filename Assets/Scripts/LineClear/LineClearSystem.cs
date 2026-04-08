using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class LineClearSystem : MonoBehaviour
    {
        public BoardManager boardManager;
        public TilemapController tilemapController;

        [Header("Animation")]
        public float clearAnimationDelay = 0.05f;
        public float clearAnimationDuration = 0.3f;

        [Header("Clear Effect")]
        [Tooltip("ライン消去エフェクトのスプライト配列（コマ送りアニメーション順に並べる）")]
        public Sprite[] clearEffectFrames;
        [Tooltip("エフェクトの再生速度（秒/フレーム）")]
        public float clearEffectFrameRate = 0.05f;
        [Tooltip("エフェクトの座標オフセット (X, Y)")]
        public Vector2 clearEffectOffset = Vector2.zero;
        [Tooltip("エフェクトのスケール")]
        public Vector2 clearEffectScale = Vector2.one;
        [Tooltip("エフェクトのソーティングレイヤー")]
        public string clearEffectSortingLayer = "Tile";
        [Tooltip("エフェクトのソーティングオーダー")]
        public int clearEffectSortingOrder = 20;

        public LineClearResult CheckAndClearLines()
        {
            var result = new LineClearResult();
            int width = boardManager.boardWidth;
            int height = boardManager.boardHeight;

            for (int y = 0; y < height; y++)
            {
                if (boardManager.IsRowFull(y))
                {
                    result.clearedRows.Add(y);
                    CheckRowSameColor(y, result);
                }
            }

            for (int x = 0; x < width; x++)
            {
                if (boardManager.IsColumnFull(x))
                {
                    result.clearedColumns.Add(x);
                    CheckColumnSameColor(x, result);
                }
            }

            result.linesCleared = result.clearedRows.Count + result.clearedColumns.Count;

            if (result.linesCleared > 0)
            {
                CollectClearedCells(result);
            }

            return result;
        }

        public void ExecuteClearWithCallback(LineClearResult result, Action onComplete)
        {
            StartCoroutine(AnimateAndClear(result, onComplete));
        }

        void CheckRowSameColor(int y, LineClearResult result)
        {
            int width = boardManager.boardWidth;
            BlockColorType firstColor = boardManager.grid[0, y].colorType;
            bool allSame = true;

            for (int x = 1; x < width; x++)
            {
                if (boardManager.grid[x, y].colorType != firstColor)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                result.hasSameColorLine = true;
                result.sameColorType = firstColor;
            }
        }

        void CheckColumnSameColor(int x, LineClearResult result)
        {
            int height = boardManager.boardHeight;
            BlockColorType firstColor = boardManager.grid[x, 0].colorType;
            bool allSame = true;

            for (int y = 1; y < height; y++)
            {
                if (boardManager.grid[x, y].colorType != firstColor)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                result.hasSameColorLine = true;
                result.sameColorType = firstColor;
            }
        }

        void CollectClearedCells(LineClearResult result)
        {
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            int width = boardManager.boardWidth;
            int height = boardManager.boardHeight;

            foreach (int y in result.clearedRows)
            {
                for (int x = 0; x < width; x++)
                    cells.Add(new Vector2Int(x, y));
            }

            foreach (int x in result.clearedColumns)
            {
                for (int y = 0; y < height; y++)
                    cells.Add(new Vector2Int(x, y));
            }

            result.clearedCells.AddRange(cells);
        }

        IEnumerator AnimateAndClear(LineClearResult result, Action onComplete)
        {
            // ブロックを即座に全マス消す
            foreach (var cell in result.clearedCells)
            {
                Vector3 worldPos = boardManager.BoardToWorld(cell);
                SpawnClearEffect(worldPos, boardManager.grid[cell.x, cell.y].colorType);
                boardManager.ClearCell(cell.x, cell.y);
            }

            // エフェクトの再生完了を待つ
            float effectDuration = (clearEffectFrames != null && clearEffectFrames.Length > 0)
                ? clearEffectFrames.Length * clearEffectFrameRate
                : clearAnimationDuration;
            yield return new WaitForSeconds(effectDuration);

            onComplete?.Invoke();
        }

        void SpawnClearEffect(Vector3 position, BlockColorType colorType)
        {
            // スプライトアニメが設定されていればコマ送りで再生
            if (clearEffectFrames != null && clearEffectFrames.Length > 0)
            {
                GameObject effect = new GameObject("ClearEffect");
                effect.transform.position = position + new Vector3(clearEffectOffset.x, clearEffectOffset.y, 0f);
                effect.transform.localScale = new Vector3(clearEffectScale.x, clearEffectScale.y, 1f);

                var sr = effect.AddComponent<SpriteRenderer>();
                sr.sprite           = clearEffectFrames[0];
                sr.color            = Color.white;
                sr.sortingLayerName = clearEffectSortingLayer;
                sr.sortingOrder     = clearEffectSortingOrder;

                StartCoroutine(PlayFrameAnimation(effect, sr));
            }
            else
            {
                // フォールバック: 従来のフェードアウトエフェクト
                GameObject effect = new GameObject("ClearEffect");
                effect.transform.position = position;
                var sr = effect.AddComponent<SpriteRenderer>();
                sr.sprite = BlockPiece.GetOrCreateSquareSprite();
                sr.color  = BlockPiece.GetColorForType(colorType);
                sr.sortingOrder = 10;
                StartCoroutine(FadeAndDestroy(effect, clearAnimationDuration));
            }
        }

        IEnumerator PlayFrameAnimation(GameObject obj, SpriteRenderer sr)
        {
            for (int i = 0; i < clearEffectFrames.Length; i++)
            {
                if (obj == null) yield break;
                sr.sprite = clearEffectFrames[i];
                yield return new WaitForSeconds(clearEffectFrameRate);
            }
            if (obj != null) Destroy(obj);
        }

        IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            Vector3 startScale = obj.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                obj.transform.localScale = startScale * (1f + t * 0.5f);
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = 1f - t;
                    sr.color = c;
                }
                yield return null;
            }

            Destroy(obj);
        }
    }
}
