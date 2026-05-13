using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップに並べる「カード 1 枚分」のアイテム定義。
    ///
    /// ■ CSV (ステージ構成資料) との対応
    ///   ID, アイテム,         効果,        レアリティ, 金額, 説明文
    ///   1,  弾でかくなる,     1.01倍,      N,         500,  ...
    ///
    ///   → category = ShopItemCategory.BulletSize
    ///   → tierIndex = 0  (= ShopItemEffectTable.entries[BulletSize].values[0] = 1.01)
    ///   → rarity = Rarity.N
    ///   → price = 500
    ///   → descriptionTemplate に CSV の「説明文」列 (or プレースホルダ {value} を含む文章)
    ///
    /// ■ 設計方針
    ///   ・効果値そのもの (1.01 / 1.05 / +3 …) はここに「持たない」。
    ///     ShopItemEffectTable に集約してあるので tierIndex で参照する。
    ///   ・descriptionTemplate に "{value}" / "{amount}" を含めると、
    ///     ShopCard.Apply 時に ShopItemEffectTable.GetFormatted() の結果で差し替えられる。
    ///     (例: "プリが{value}でかくなる！" → "プリが1.05倍でかくなる！")
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItem_New", menuName = "BlockBlast/Shop/Shop Item")]
    public class ShopItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("CSV の ID 列に対応。一意な番号。")]
        public int id;

        [Tooltip("カードに表示する短い名前 (例: \"弾でかくなる\")。\n" +
                 "未指定なら category 名がそのまま使われる。")]
        public string displayName;

        [Header("Effect")]
        [Tooltip("どの効果カテゴリか (CSV の \"アイテム\" 列)")]
        public ShopItemCategory category;

        [Tooltip("ShopItemEffectTable.entries[category].values[ここのインデックス] で効果値を参照する。\n" +
                 "0=最弱 / 末尾=最強 になるよう Table 側で並べておくこと。")]
        public int tierIndex;

        [Header("Card")]
        [Tooltip("CSV の レアリティ列")]
        public Rarity rarity = Rarity.N;

        [Tooltip("CSV の 金額列")]
        public int price = 500;

        [Tooltip("カードに表示するアイテム画像 (アイコン)")]
        public Sprite icon;

        [Header("Description")]
        [TextArea(2, 6)]
        [Tooltip("カードに表示する説明文テンプレ。\n" +
                 "テキスト中の \"{value}\" / \"{amount}\" は ShopItemEffectTable から取得した値で差し替えられる。\n" +
                 "例: \"プリが{value}でかくなるぞっ！どーんとつよいっ！！\"")]
        public string descriptionTemplate;

        /// <summary>UI 表示用の名前 (displayName が空のときは category 名)</summary>
        public string ResolveDisplayName()
            => string.IsNullOrEmpty(displayName) ? category.ToString() : displayName;
    }
}
