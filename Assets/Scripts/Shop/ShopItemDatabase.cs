using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 全ショップアイテム (ShopItemData) を保持する集約 ScriptableObject。
    ///
    /// ・ShopItemPool が抽選元として参照する
    /// ・ID / カテゴリ / レアリティ単位で検索する API を提供
    /// ・Editor のジェネレータ (ShopItemAssetGenerator) が CSV 準拠の 27 個を一括登録する
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItemDatabase", menuName = "BlockBlast/Shop/Item Database")]
    public class ShopItemDatabase : ScriptableObject
    {
        [Tooltip("全アイテム。Generator で自動登録するか、手動でドラッグしても良い。")]
        public List<ShopItemData> items = new List<ShopItemData>();

        public ShopItemData GetById(int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].id == id) return items[i];
            }
            return null;
        }

        public List<ShopItemData> GetByRarity(Rarity rarity)
        {
            var list = new List<ShopItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].rarity == rarity) list.Add(items[i]);
            }
            return list;
        }

        public List<ShopItemData> GetByRarities(IList<Rarity> rarities)
        {
            var list = new List<ShopItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                for (int r = 0; r < rarities.Count; r++)
                {
                    if (items[i].rarity == rarities[r])
                    {
                        list.Add(items[i]);
                        break;
                    }
                }
            }
            return list;
        }

        public List<ShopItemData> GetByCategory(ShopItemCategory category)
        {
            var list = new List<ShopItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].category == category) list.Add(items[i]);
            }
            return list;
        }
    }
}
