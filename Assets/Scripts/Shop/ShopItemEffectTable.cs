using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップアイテムの「効果値」を一括で管理する中央テーブル。
    ///
    /// CSV では 1.01倍 / 1.05倍 / +1 / +5 / +1% などの値が各アイテムに直接書かれているが、
    /// このアセットでカテゴリ単位にまとめて管理することで、後から値を変更すれば
    /// すべての ShopItemData の表記 (説明文) が連動して更新される。
    ///
    /// ■ 使い方
    ///   1. Project ビューで右クリック → Create / BlockBlast / Shop / Effect Table
    ///   2. entries に各 ShopItemCategory のエントリを追加して、values と displayFormat を埋める。
    ///   3. ShopItemData.tierIndex = values[] のインデックスで効果値を参照する。
    ///   4. 説明文テンプレ内の "{value}" がフォーマット後の効果値で差し替えられる (ShopCard.Apply 内)。
    ///
    /// ■ 例 (CSV 準拠の初期値)
    ///   BulletSize:  values=[1.01, 1.02, 1.05, 1.10], displayFormat="{0}倍"
    ///   BulletSpeed: values=[1.05, 1.2, 1.5, 2.0],    displayFormat="{0}倍"
    ///   BulletCount: values=[1, 3, 5],                 displayFormat="プラス{0}"
    ///   LuckUp:      values=[1, 3, 5, 10],             displayFormat="+{0}%"
    ///   MoneyBonus:  values=[100, 300, 500, 1000],     displayFormat="プラス{0}"
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItemEffectTable", menuName = "BlockBlast/Shop/Effect Table")]
    public class ShopItemEffectTable : ScriptableObject
    {
        [Serializable]
        public class CategoryEntry
        {
            [Tooltip("どのカテゴリの効果値か")]
            public ShopItemCategory category;

            [Tooltip("各 Tier の効果値。例: BulletSize なら [1.01, 1.02, 1.05, 1.1]\n" +
                     "ShopItemData.tierIndex でここのインデックスを指す。")]
            public List<float> values = new List<float>();

            [Tooltip("表示用フォーマット文字列。{0} に value が差し込まれる。\n" +
                     "例: \"{0}倍\" / \"プラス{0}\" / \"+{0}%\" / \"{0}\"\n" +
                     "値の小数表現が嫌なら \"{0:0.##}倍\" のようにも指定できる。")]
            public string displayFormat = "{0}";

            [Tooltip("(任意) 整数値しか取らないカテゴリは ON にしておくと \"1.0\" ではなく \"1\" と表示される。\n" +
                     "BulletCount / Penetration / LuckUp / MoneyBonus などに使用。")]
            public bool forceInteger = false;
        }

        [Tooltip("カテゴリ単位の効果値テーブル。重複カテゴリは最初に見つかったものが採用される。")]
        public List<CategoryEntry> entries = new List<CategoryEntry>();

        Dictionary<ShopItemCategory, CategoryEntry> _cache;

        void OnEnable()
        {
            _cache = null;
        }

        void OnValidate()
        {
            _cache = null;
        }

        CategoryEntry GetEntry(ShopItemCategory category)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<ShopItemCategory, CategoryEntry>();
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    if (!_cache.ContainsKey(e.category))
                        _cache[e.category] = e;
                }
            }
            _cache.TryGetValue(category, out var entry);
            return entry;
        }

        /// <summary>カテゴリ+Tier に対する生の効果値 (例: 1.05f / 3f / 100f)。</summary>
        public float GetValue(ShopItemCategory category, int tierIndex)
        {
            var entry = GetEntry(category);
            if (entry == null) return 0f;
            if (tierIndex < 0 || tierIndex >= entry.values.Count) return 0f;
            return entry.values[tierIndex];
        }

        /// <summary>displayFormat に当てはめた表示用文字列 (例: "1.05倍" / "プラス3" / "+10%")。</summary>
        public string GetFormatted(ShopItemCategory category, int tierIndex)
        {
            var entry = GetEntry(category);
            if (entry == null) return "";
            if (tierIndex < 0 || tierIndex >= entry.values.Count) return "";
            float v = entry.values[tierIndex];
            string fmt = string.IsNullOrEmpty(entry.displayFormat) ? "{0}" : entry.displayFormat;
            if (entry.forceInteger)
                return string.Format(fmt, Mathf.RoundToInt(v));
            return string.Format(fmt, v);
        }

        /// <summary>そのカテゴリで定義されている Tier 数。</summary>
        public int GetTierCount(ShopItemCategory category)
        {
            var entry = GetEntry(category);
            return entry != null ? entry.values.Count : 0;
        }
    }
}
