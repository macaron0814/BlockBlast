using UnityEngine;

namespace BlockBlastGame
{
    public class BlockPiece : MonoBehaviour
    {
        public BlockData blockData;
        public SpriteRenderer[] cellRenderers;

        [Header("Visual Settings")]
        public float cellSize = 0.35f;
        public GameObject cellPrefab;

        [Header("Custom Sprite (optional)")]
        [Tooltip("ここに画像を入れると、そのスプライトでブロックを描画します。空の場合は自動生成スプライトを使用します。")]
        public Sprite customSprite;

        // tileSpacingX/Y は TilemapController.tileSpacingX/Y と同じ値を渡すことで連動します
        public void Initialize(BlockData data, Sprite overrideSprite = null,
                               float tileSpacingX = 0.08f, float tileSpacingY = 0.08f)
        {
            blockData = data;
            if (overrideSprite != null)
                customSprite = overrideSprite;
            _tileSpacingX = tileSpacingX;
            _tileSpacingY = tileSpacingY;
            BuildVisual();
        }

        float _tileSpacingX = 0.08f;
        float _tileSpacingY = 0.08f;

        void BuildVisual()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            if (blockData == null) return;

            bool[,] shape = blockData.GetShapeArray();
            int w = shape.GetLength(0);
            int h = shape.GetLength(1);

            float offsetX = -(w - 1) * cellSize * 0.5f;
            float offsetY = -(h - 1) * cellSize * 0.5f;

            var renderers = new System.Collections.Generic.List<SpriteRenderer>();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (!shape[x, y]) continue;

                    var cell = new GameObject($"Cell_{x}_{y}");
                    cell.transform.SetParent(transform);

                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite = customSprite != null ? customSprite : GetOrCreateSquareSprite();
                    // カスタムスプライトがある場合は色を変えずにそのまま表示
                    sr.color  = customSprite != null ? Color.white : GetColorForType(blockData.colorType);
                    sr.sortingLayerName = "Tile";
                    // 行が下（y 小）ほど前面に表示されるよう sortingOrder を設定
                    // 例: h=3 の場合 y=0→12, y=1→11, y=2→10
                    sr.sortingOrder = 10 + (h - 1 - y);
                    renderers.Add(sr);

                    cell.transform.localPosition = new Vector3(
                        offsetX + x * cellSize,
                        offsetY + y * cellSize,
                        0);
                    // tileSpacingX/Y と同じ比率でセルを縮小し、地面タイルと間隔を連動させる
                    cell.transform.localScale = new Vector3(
                        cellSize * (1f - _tileSpacingX),
                        cellSize * (1f - _tileSpacingY),
                        1f);
                }
            }

            cellRenderers = renderers.ToArray();
        }

        static Sprite squareSprite;

        public static Sprite GetOrCreateSquareSprite()
        {
            if (squareSprite != null) return squareSprite;

            const int size   = 64;
            const float cr   = 9f;   // corner radius (px)

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // ---- rounded-rectangle alpha ----
                    float nx = Mathf.Clamp(px, cr, size - 1 - cr);
                    float ny = Mathf.Clamp(py, cr, size - 1 - cr);
                    float d  = Mathf.Sqrt((px - nx) * (px - nx) + (py - ny) * (py - ny));
                    float alpha = Mathf.Clamp01(cr - d + 1f);
                    if (alpha <= 0f) { pixels[py * size + px] = Color.clear; continue; }

                    // ---- brightness gradient (bright top → slightly darker bottom) ----
                    float gy   = 1f - py / (float)(size - 1);   // 1 = top, 0 = bottom
                    float gx   = px / (float)(size - 1);        // 0 = left, 1 = right
                    float base_ = Mathf.Lerp(0.68f, 1.00f, gy * 0.75f + (1f - gx) * 0.25f);

                    // ---- top-edge highlight strip ----
                    float topH  = (1f - Mathf.Clamp01((py - 2f) / 6f)) * 0.30f;
                    // ---- left-edge highlight strip ----
                    float leftH = (1f - Mathf.Clamp01((px - 2f) / 6f)) * 0.20f;
                    // ---- bottom-right inner shadow ----
                    float shadB = Mathf.Clamp01((py - (size - 8f)) / 6f) * 0.20f;
                    float shadR = Mathf.Clamp01((px - (size - 8f)) / 6f) * 0.15f;

                    float bright = Mathf.Clamp01(base_ + topH + leftH - shadB - shadR);
                    pixels[py * size + px] = new Color(bright, bright, bright, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            squareSprite = Sprite.Create(tex,
                new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return squareSprite;
        }

        public static Color GetColorForType(BlockColorType colorType)
        {
            return colorType switch
            {
                BlockColorType.Red => new Color(0.9f, 0.2f, 0.2f),
                BlockColorType.Blue => new Color(0.2f, 0.4f, 0.9f),
                BlockColorType.Green => new Color(0.2f, 0.8f, 0.3f),
                BlockColorType.Yellow => new Color(0.95f, 0.85f, 0.2f),
                BlockColorType.Purple => new Color(0.7f, 0.2f, 0.9f),
                BlockColorType.Orange => new Color(0.95f, 0.55f, 0.1f),
                BlockColorType.Cyan => new Color(0.2f, 0.85f, 0.9f),
                _ => Color.white
            };
        }
    }
}
