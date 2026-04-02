using UnityEngine;

namespace BlockBlastGame
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "BlockBlast/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemName;
        public string description;
        public Sprite icon;
        public ItemType type;

        [Range(1, 5)]
        public int rarity = 1;

        [Header("Effects")]
        public int turnBonusAmount;
        public string spaceshipPartId;

        public static ItemData CreateRuntime(string name, ItemType type, int rarity)
        {
            var item = CreateInstance<ItemData>();
            item.itemName = name;
            item.type = type;
            item.rarity = rarity;
            return item;
        }
    }
}
