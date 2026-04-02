using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BlockBlastGame
{
    public class TilemapController : MonoBehaviour
    {
        [Header("Tilemaps")]
        public Tilemap boardTilemap;
        public Tilemap blockTilemap;
        public Tilemap previewTilemap;

        [Header("Tiles")]
        public TileBase boardTile;
        public TileBase[] colorTiles;
        public TileBase previewValidTile;
        public TileBase previewInvalidTile;

        [Header("Board Offset")]
        public Vector2Int boardOffset = Vector2Int.zero;

        [Header("Cell Spacing")]
        [Tooltip("横方向の間隔（0 = なし / 0.5 = 最大）。地面タイルとブロックセル両方に連動します。")]
        [Range(0f, 0.5f)]
        public float tileSpacingX = 0.08f;
        [Tooltip("縦方向の間隔（0 = なし / 0.5 = 最大）。地面タイルとブロックセル両方に連動します。")]
        [Range(0f, 0.5f)]
        public float tileSpacingY = 0.08f;

        [Header("Custom Ground Sprite (optional)")]
        [Tooltip("マップ地面タイルに使うスプライト画像を入れてください。空の場合はデフォルトのタイルを使用します。")]
        public Sprite customBoardTileSprite;

        readonly Dictionary<Vector2Int, GameObject> itemOverlays = new Dictionary<Vector2Int, GameObject>();
        static Sprite _starSprite;

        // 設置済みブロック用のカスタムタイルキャッシュ
        Tile _customBlockTile;
        Sprite _cachedBlockSprite;

        public void DrawBoard(int width, int height)
        {
            // カスタムスプライトが設定されていれば boardTile を差し替える
            if (customBoardTileSprite != null)
            {
                var customTile = ScriptableObject.CreateInstance<Tile>();
                customTile.sprite = customBoardTileSprite;
                customTile.color  = Color.white;
                boardTile = customTile;
            }

            // tileSpacingX/Y を Grid.cellGap に適用（地面タイルの間隔）
            var grid = GetComponent<Grid>();
            if (grid != null)
                grid.cellGap = new Vector3(tileSpacingX, tileSpacingY, 0f);

            // blockTilemap を Individual モードにして行ごとのレイヤー制御を有効化
            var blockRenderer = blockTilemap?.GetComponent<TilemapRenderer>();
            if (blockRenderer != null)
                blockRenderer.mode = TilemapRenderer.Mode.Individual;

            boardTilemap.ClearAllTiles();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3Int tilePos = new Vector3Int(x + boardOffset.x, y + boardOffset.y, 0);
                    boardTilemap.SetTile(tilePos, boardTile);
                }
            }
        }

        public void SetBlockTile(int x, int y, BlockColorType colorType)
        {
            Vector3Int tilePos = new Vector3Int(x + boardOffset.x, y + boardOffset.y, 0);

            // BlockSpawner にカスタムスプライトが設定されている場合は
            // 設置前と同じ見た目（色変えなし）で配置する
            var customSprite = GameManager.Instance?.blockSpawner?.blockCellSprite;
            if (customSprite != null)
            {
                // スプライトが変わったときだけタイルを再生成
                if (_customBlockTile == null || _cachedBlockSprite != customSprite)
                {
                    _customBlockTile        = ScriptableObject.CreateInstance<Tile>();
                    _customBlockTile.sprite = customSprite;
                    _customBlockTile.color  = Color.white;
                    _cachedBlockSprite      = customSprite;
                }
                blockTilemap.SetTile(tilePos, _customBlockTile);
            }
            else
            {
                int colorIndex = (int)colorType;
                if (colorTiles != null && colorIndex < colorTiles.Length)
                    blockTilemap.SetTile(tilePos, colorTiles[colorIndex]);
            }

            // 行が下（Y 小）ほど Z が手前になるよう設定
            // → 下の行のスプライトが上の行の前面に表示される
            blockTilemap.SetTileFlags(tilePos, TileFlags.None);
            blockTilemap.SetTransformMatrix(tilePos,
                Matrix4x4.TRS(new Vector3(0f, 0f, y * 0.01f),
                              Quaternion.identity, Vector3.one));
        }

        public void ClearBlockTile(int x, int y)
        {
            Vector3Int tilePos = new Vector3Int(x + boardOffset.x, y + boardOffset.y, 0);
            blockTilemap.SetTile(tilePos, null);
        }

        public void ClearAllBlocks()
        {
            blockTilemap.ClearAllTiles();
        }

        public void ShowPreview(bool[,] shape, Vector2Int boardPos, bool isValid)
        {
            ClearPreview();
            int shapeW = shape.GetLength(0);
            int shapeH = shape.GetLength(1);
            TileBase tile = isValid ? previewValidTile : previewInvalidTile;

            for (int x = 0; x < shapeW; x++)
            {
                for (int y = 0; y < shapeH; y++)
                {
                    if (!shape[x, y]) continue;
                    Vector3Int tilePos = new Vector3Int(
                        boardPos.x + x + boardOffset.x,
                        boardPos.y + y + boardOffset.y, 0);
                    previewTilemap.SetTile(tilePos, tile);
                }
            }
        }

        public void ClearPreview()
        {
            previewTilemap.ClearAllTiles();
        }

        public void SetItemMarker(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (itemOverlays.ContainsKey(key)) return;

            var overlay = new GameObject($"ItemStar_{x}_{y}");
            overlay.transform.SetParent(transform);
            var worldPos = BoardToWorld(key);
            overlay.transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);

            var sr = overlay.AddComponent<SpriteRenderer>();
            sr.sprite = GetStarSprite();
            sr.color = new Color(1f, 1f, 0.3f, 1f);
            sr.sortingOrder = 15;

            overlay.AddComponent<ItemPulse>();

            itemOverlays[key] = overlay;
        }

        public void ClearItemMarker(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (itemOverlays.TryGetValue(key, out var overlay))
            {
                if (overlay != null) Destroy(overlay);
                itemOverlays.Remove(key);
            }
        }

        public void ClearAllItemMarkers()
        {
            foreach (var kvp in itemOverlays)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            itemOverlays.Clear();
        }

        public Vector2Int WorldToBoard(Vector3 worldPos)
        {
            Vector3Int cellPos = boardTilemap.WorldToCell(worldPos);
            return new Vector2Int(cellPos.x - boardOffset.x, cellPos.y - boardOffset.y);
        }

        public Vector3 BoardToWorld(Vector2Int boardPos)
        {
            Vector3Int cellPos = new Vector3Int(boardPos.x + boardOffset.x, boardPos.y + boardOffset.y, 0);
            return boardTilemap.CellToWorld(cellPos) + boardTilemap.cellSize / 2f;
        }

        static Sprite GetStarSprite()
        {
            if (_starSprite != null) return _starSprite;

            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];

            float cx = size / 2f;
            float cy = size / 2f;
            float outerR = size / 2f - 1f;
            float innerR = outerR * 0.4f;
            int points = 4;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = px - cx;
                    float dy = py - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float starAngle = Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f / points) - Mathf.PI / points;
                    float cosA = Mathf.Cos(starAngle);
                    float r = innerR / Mathf.Max(cosA, 0.01f);
                    r = Mathf.Min(r, outerR);

                    float edgeDist = r - dist;
                    if (edgeDist >= 0)
                    {
                        float alpha = Mathf.Clamp01(edgeDist * 2f);
                        pixels[py * size + px] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[py * size + px] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _starSprite;
        }
    }

    public class ItemPulse : MonoBehaviour
    {
        float timer;
        SpriteRenderer sr;
        Vector3 baseScale;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            timer = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            timer += Time.deltaTime * 3f;
            float pulse = 0.8f + 0.2f * Mathf.Sin(timer);
            transform.localScale = baseScale * pulse;

            if (sr != null)
            {
                float alpha = 0.7f + 0.3f * Mathf.Sin(timer * 1.5f);
                sr.color = new Color(1f, 1f, 0.3f, alpha);
            }
        }
    }
}
