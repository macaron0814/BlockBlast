using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップカードの「レアリティの見た目」を定義する中央テーブル。
    ///
    /// ・枠 (frame) の色 / スプライト
    /// ・背景 (background) の色 / スプライト
    /// ・レアリティバッジ (label) のテキスト / 色
    /// ・名前テキストの色
    ///
    /// ShopCard.Apply() がこの Table を見て、カードの見た目をレアリティに合わせて切り替える。
    /// 個別レアリティ用に異なる Prefab を作る必要はなく、1 種類の Prefab で色だけ切り替えれば良いように設計。
    /// </summary>
    [CreateAssetMenu(fileName = "ShopRarityVisualTable", menuName = "BlockBlast/Shop/Rarity Visual Table")]
    public class ShopRarityVisualTable : ScriptableObject
    {
        [Serializable]
        public class RarityEntry
        {
            public Rarity rarity;

            [Tooltip("バッジに表示する短いラベル (\"N\" / \"R\" / \"SR\" など)。\n" +
                     "空なら enum 名がそのまま使われる。")]
            public string displayLabel = "";

            [Tooltip("枠 (frame) の色 tint")]
            public Color frameColor = Color.white;

            [Tooltip("背景 (background) の色 tint")]
            public Color backgroundColor = Color.white;

            [Tooltip("バッジ / 名前テキストの色 tint")]
            public Color textColor = Color.white;

            [Tooltip("説明文中の {value} (倍率や個数) 部分だけに当てる色。\n" +
                     "ShopCard.Apply 内で TMP の <color=#RRGGBB> タグで囲まれる。\n" +
                     "= レアリティ毎に「数字の色」を変えたい時にここで設定する。")]
            public Color valueColor = new Color(1f, 0.85f, 0.15f, 1f);

            [Tooltip("値段テキスト (\"500\" 等) の色 (買えるとき)。\n" +
                     "ShopCard.priceColorAffordable に上書きされる。\n" +
                     "※買えないときの色 (赤) は ShopCard.priceColorUnaffordable 側で別管理。")]
            public Color priceColor = Color.white;

            [Tooltip("枠用のスプライト (任意)。指定すれば ShopCard の frameImage に当てる")]
            public Sprite frameSprite;

            [Tooltip("背景用のスプライト (任意)。指定すれば ShopCard の backgroundImage に当てる")]
            public Sprite backgroundSprite;

            [Tooltip("バッジのアイコン (任意)。指定すれば ShopCard の rarityBadgeImage に当てる")]
            public Sprite badgeSprite;
        }

        public List<RarityEntry> entries = new List<RarityEntry>();

        Dictionary<Rarity, RarityEntry> _cache;

        void OnEnable()  { _cache = null; }
        void OnValidate(){ _cache = null; }

        public RarityEntry Get(Rarity r)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<Rarity, RarityEntry>();
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    if (!_cache.ContainsKey(e.rarity)) _cache[e.rarity] = e;
                }
            }
            _cache.TryGetValue(r, out var entry);
            return entry;
        }

        /// <summary>displayLabel が空のときは enum 名そのまま返す。</summary>
        public string GetLabel(Rarity r)
        {
            var e = Get(r);
            if (e == null) return r.ToString();
            return string.IsNullOrEmpty(e.displayLabel) ? r.ToString() : e.displayLabel;
        }
    }
}
