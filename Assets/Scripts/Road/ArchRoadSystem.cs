using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    [Serializable]
    public class CurbEntry
    {
        [Tooltip("縁石スプライト")]
        public Sprite sprite;
        [Tooltip("座標オフセット (X, Y)")]
        public Vector2 offset = new Vector2(0f, 0.05f);
        [Tooltip("スケール (X, Y)")]
        public Vector2 scale = Vector2.one;
    }

    /// <summary>
    /// 道路タイルを360°一周ぐるっと円弧に沿って配置し、ループスクロールさせるシステム。
    /// 白線・縁石は一定間隔で配置。縁石は複数パターンからランダム選択。
    /// </summary>
    public class ArchRoadSystem : MonoBehaviour
    {
        [Header("Sprites — 道路本体")]
        [Tooltip("道路本体のスプライト (156px)")]
        public Sprite roadSprite;

        [Header("Sprites — 白線")]
        [Tooltip("白線スプライト")]
        public Sprite whiteLineSprite;
        [Tooltip("何タイルごとに白線を配置するか (0 = 全タイル)")]
        public int whiteLineInterval = 4;

        [Header("縁石 (複数パターン — 個別に座標・スケール設定可)")]
        [Tooltip("縁石パターンの配列。配置時にランダム選択されます。")]
        public CurbEntry[] curbEntries;
        [Tooltip("何タイルごとに縁石を配置するか (0 = 全タイル)")]
        public int curbInterval = 3;

        [Header("Arch Settings")]
        [Tooltip("アーチの半径。大きいほど平坦に見える")]
        public float archRadius = 50f;
        [Tooltip("タイル1枚のワールド幅 (156px ÷ PPU)")]
        public float tileWorldWidth = 1.56f;

        [Header("Scroll")]
        [Tooltip("道路のスクロール速度 (度/秒)")]
        public float scrollSpeed = 15f;

        [Header("Layer Offsets")]
        [Tooltip("白線の Y オフセット")]
        public float whiteLineOffsetY = 0f;

        [Header("Sorting")]
        public string sortingLayer = "UI";
        public int roadSortingOrder      = 0;
        public int whiteLineSortingOrder  = 1;
        public int curbSortingOrder      = 2;

        float _currentAngle;
        float _anglePerTile;
        int   _totalTiles;
        readonly List<Transform> _roadTiles = new List<Transform>();

        void Start()
        {
            if (roadSprite == null) return;
            BuildRoad();
        }

        void Update()
        {
            _currentAngle += scrollSpeed * Time.deltaTime;
            if (_currentAngle >= 360f) _currentAngle -= 360f;
            if (_currentAngle < 0f)    _currentAngle += 360f;
            RepositionTiles();
        }

        // ──────────────────────────────────
        //  Build — 360° 一周分のタイルを生成
        // ──────────────────────────────────

        void BuildRoad()
        {
            float circumference = 2f * Mathf.PI * archRadius;
            _anglePerTile = (tileWorldWidth / circumference) * 360f;

            // 360° をぐるっと一周する枚数
            _totalTiles = Mathf.CeilToInt(360f / _anglePerTile);

            for (int i = 0; i < _totalTiles; i++)
            {
                var tile = CreateTileObject(i);
                _roadTiles.Add(tile);
            }

            RepositionTiles();
        }

        Transform CreateTileObject(int index)
        {
            var root = new GameObject($"RoadTile_{index}");
            root.transform.SetParent(transform);

            // 道路本体 — 全タイルに配置
            AddSpriteChild(root.transform, "Road", roadSprite,
                           Color.white, sortingLayer, roadSortingOrder, Vector2.zero);

            // 白線 — whiteLineInterval ごとに配置 (0 = 全タイル)
            if (whiteLineSprite != null)
            {
                bool place = whiteLineInterval <= 0 || (index % whiteLineInterval == 0);
                if (place)
                    AddSpriteChild(root.transform, "WhiteLine", whiteLineSprite,
                                   Color.white, sortingLayer, whiteLineSortingOrder,
                                   new Vector2(0f, whiteLineOffsetY));
            }

            // 縁石 — curbInterval ごとに配置、複数パターンからランダム選択
            if (curbEntries != null && curbEntries.Length > 0)
            {
                bool place = curbInterval <= 0 || (index % curbInterval == 0);
                if (place)
                {
                    var entry = curbEntries[UnityEngine.Random.Range(0, curbEntries.Length)];
                    if (entry != null && entry.sprite != null)
                        AddSpriteChild(root.transform, "Curb", entry.sprite,
                                       Color.white, sortingLayer, curbSortingOrder,
                                       entry.offset, entry.scale);
                }
            }

            return root.transform;
        }

        static void AddSpriteChild(Transform parent, string name, Sprite sprite,
                                   Color color, string layer, int order, Vector2 offset,
                                   Vector2? scale = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            if (scale.HasValue)
                obj.transform.localScale = new Vector3(scale.Value.x, scale.Value.y, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite           = sprite;
            sr.color            = color;
            sr.sortingLayerName = layer;
            sr.sortingOrder     = order;
        }

        // ──────────────────────────────────
        //  Loop / Reposition — 360° ループ
        // ──────────────────────────────────

        void RepositionTiles()
        {
            for (int i = 0; i < _totalTiles; i++)
            {
                float tileAngle = _currentAngle + i * _anglePerTile;

                float rad = tileAngle * Mathf.Deg2Rad;
                float x   = Mathf.Sin(rad) * archRadius;
                float y   = -archRadius + Mathf.Cos(rad) * archRadius;

                _roadTiles[i].position = transform.position + new Vector3(x, y, 0f);
                _roadTiles[i].rotation = Quaternion.Euler(0f, 0f, -tileAngle);
            }
        }

        // ──────────────────────────────────
        //  Public API
        // ──────────────────────────────────

        public void SetScrollSpeed(float speed) => scrollSpeed = speed;

        public void SetArchRadius(float radius)
        {
            archRadius = radius;
            float circumference = 2f * Mathf.PI * archRadius;
            _anglePerTile = (tileWorldWidth / circumference) * 360f;
        }
    }
}
