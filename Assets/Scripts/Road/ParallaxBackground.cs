using System;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 背景レイヤーを道路と同じアーチ状に配置し、視差（パララックス）付きでループスクロールさせる。
    /// ArchRoadSystem と同じ原理で動作する。
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        [Serializable]
        public class BackgroundLayer
        {
            [Tooltip("レイヤー名 (表示用)")]
            public string name = "Layer";
            [Tooltip("背景スプライト")]
            public Sprite sprite;
            [Tooltip("スクロール速度の倍率 (1.0 = 道路と同速, 0 = 固定)")]
            [Range(-1f, 1f)]
            public float parallaxFactor = 0.5f;
            [Tooltip("Y オフセット (道路からの高さ)")]
            public float offsetY = 0f;
            [Tooltip("アーチ半径を道路とどれだけ変えるか (正 = より遠く / 平坦)")]
            public float archRadiusOffset = 0f;

            [Header("Sorting")]
            public string sortingLayer = "UI";
            public int sortingOrder = -10;

            [Header("Scale")]
            [Tooltip("タイルの拡大縮小 (X, Y)")]
            public Vector2 scale = Vector2.one;

            [Header("Tiling")]
            [Tooltip("タイル1枚のワールド幅 (0 = スプライトの自然幅 × scaleX を使用)")]
            public float tileWorldWidth = 0f;

            [HideInInspector] public Transform[] tiles;
            [HideInInspector] public float anglePerTile;
            [HideInInspector] public float effectiveRadius;
            [HideInInspector] public float effectiveWidth;
            [HideInInspector] public int totalTiles;
        }

        [Header("Layers — 下から上の順に追加")]
        public BackgroundLayer[] layers;

        [Header("Global Settings")]
        [Tooltip("ArchRoadSystem と同じ基準半径")]
        public float baseArchRadius = 50f;
        [Tooltip("基準スクロール速度 (度/秒) — ArchRoadSystem.scrollSpeed と揃える")]
        public float baseScrollSpeed = 15f;

        float _currentAngle;

        void Start()
        {
            if (layers == null) return;
            foreach (var layer in layers)
                BuildLayer(layer);
        }

        void Update()
        {
            _currentAngle += baseScrollSpeed * Time.deltaTime;
            if (_currentAngle >= 360f) _currentAngle -= 360f;
            if (_currentAngle < 0f)    _currentAngle += 360f;
            if (layers == null) return;
            foreach (var layer in layers)
                RepositionLayer(layer);
        }

        // ──────────────────────────────────
        //  Build
        // ──────────────────────────────────

        void BuildLayer(BackgroundLayer layer)
        {
            if (layer.sprite == null) return;

            layer.effectiveRadius = baseArchRadius + layer.archRadiusOffset;

            // ワールド幅: 指定があればそれ、なければスプライトの自然幅 × scaleX
            float spriteNaturalWidth = layer.sprite.bounds.size.x * layer.scale.x;
            layer.effectiveWidth = layer.tileWorldWidth > 0f
                ? layer.tileWorldWidth
                : spriteNaturalWidth;

            float circumference = 2f * Mathf.PI * layer.effectiveRadius;
            layer.anglePerTile = (layer.effectiveWidth / circumference) * 360f;

            // 360° 一周分のタイル数
            layer.totalTiles = Mathf.CeilToInt(360f / layer.anglePerTile);

            var parent = new GameObject($"BG_{layer.name}");
            parent.transform.SetParent(transform);

            layer.tiles = new Transform[layer.totalTiles];
            for (int i = 0; i < layer.totalTiles; i++)
            {
                var obj = new GameObject($"Tile_{i}");
                obj.transform.SetParent(parent.transform);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite           = layer.sprite;
                sr.sortingLayerName = layer.sortingLayer;
                sr.sortingOrder     = layer.sortingOrder;

                layer.tiles[i] = obj.transform;
            }
        }

        // ──────────────────────────────────
        //  Loop / Reposition
        // ──────────────────────────────────

        void RepositionLayer(BackgroundLayer layer)
        {
            if (layer.tiles == null) return;

            float layerAngle = _currentAngle * layer.parallaxFactor;
            float r          = layer.effectiveRadius;

            for (int i = 0; i < layer.totalTiles; i++)
            {
                float tileAngle = layerAngle + i * layer.anglePerTile;
                float rad = tileAngle * Mathf.Deg2Rad;
                float x   = Mathf.Sin(rad) * r;
                float y   = -r + Mathf.Cos(rad) * r + layer.offsetY;

                layer.tiles[i].position   = transform.position + new Vector3(x, y, 0f);
                layer.tiles[i].rotation   = Quaternion.Euler(0f, 0f, -tileAngle);
                layer.tiles[i].localScale = new Vector3(layer.scale.x, layer.scale.y, 1f);
            }
        }

        // ──────────────────────────────────
        //  Public API
        // ──────────────────────────────────

        public void SetBaseScrollSpeed(float speed) => baseScrollSpeed = speed;

        public void SetBaseArchRadius(float radius)
        {
            baseArchRadius = radius;
            if (layers == null) return;
            foreach (var layer in layers)
            {
                layer.effectiveRadius = baseArchRadius + layer.archRadiusOffset;
                float circ = 2f * Mathf.PI * layer.effectiveRadius;
                layer.anglePerTile = (layer.effectiveWidth / circ) * 360f;
            }
        }
    }
}
