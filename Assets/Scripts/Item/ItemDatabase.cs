using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public static class ItemDatabase
    {
        static List<ItemData> allItems;

        public static List<ItemData> GetAllItems()
        {
            if (allItems != null) return allItems;

            allItems = new List<ItemData>();

            // Spaceship Parts
            allItems.Add(CreateItem("エンジン", "宇宙船のメインエンジン", ItemType.SpaceshipPart, 2, "engine"));
            allItems.Add(CreateItem("翼パーツ", "宇宙船の翼", ItemType.SpaceshipPart, 2, "wing"));
            allItems.Add(CreateItem("コックピット", "操縦席モジュール", ItemType.SpaceshipPart, 3, "cockpit"));
            allItems.Add(CreateItem("シールド", "防御シールド装置", ItemType.SpaceshipPart, 3, "shield"));
            allItems.Add(CreateItem("ブースター", "加速ブースター", ItemType.SpaceshipPart, 4, "booster"));
            allItems.Add(CreateItem("レーザー砲", "攻撃用レーザー", ItemType.SpaceshipPart, 4, "laser"));
            allItems.Add(CreateItem("ワープドライブ", "超光速エンジン", ItemType.SpaceshipPart, 5, "warp"));
            allItems.Add(CreateItem("AI操縦システム", "自動操縦AI", ItemType.SpaceshipPart, 5, "ai_pilot"));

            // Turn Bonus Items
            allItems.Add(CreateTurnItem("時計", "ターン+3", 1, 3));
            allItems.Add(CreateTurnItem("砂時計", "ターン+5", 2, 5));
            allItems.Add(CreateTurnItem("時の結晶", "ターン+8", 3, 8));

            // PowerUp Items
            allItems.Add(CreateItem("ボムパーツ", "1列を消去する爆弾", ItemType.PowerUp, 3, ""));
            allItems.Add(CreateItem("カラーチェンジ", "ブロックの色を変える", ItemType.PowerUp, 2, ""));

            return allItems;
        }

        static ItemData CreateItem(string name, string desc, ItemType type, int rarity, string partId)
        {
            var item = ItemData.CreateRuntime(name, type, rarity);
            item.description = desc;
            item.spaceshipPartId = partId;
            return item;
        }

        static ItemData CreateTurnItem(string name, string desc, int rarity, int turnBonus)
        {
            var item = ItemData.CreateRuntime(name, ItemType.TurnBonus, rarity);
            item.description = desc;
            item.turnBonusAmount = turnBonus;
            return item;
        }

        public static List<ItemData> GetItemsByType(ItemType type)
        {
            var result = new List<ItemData>();
            foreach (var item in GetAllItems())
            {
                if (item.type == type)
                    result.Add(item);
            }
            return result;
        }

        public static ItemData GetRandomItem(int maxRarity = 5)
        {
            var candidates = new List<ItemData>();
            foreach (var item in GetAllItems())
            {
                if (item.rarity <= maxRarity)
                    candidates.Add(item);
            }
            if (candidates.Count == 0) return null;

            // Weighted random: lower rarity = higher chance
            float totalWeight = 0;
            foreach (var item in candidates)
                totalWeight += (6 - item.rarity);

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0;
            foreach (var item in candidates)
            {
                cumulative += (6 - item.rarity);
                if (roll <= cumulative)
                    return item;
            }

            return candidates[candidates.Count - 1];
        }
    }
}
