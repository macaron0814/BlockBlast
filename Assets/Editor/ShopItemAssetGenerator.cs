#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BlockBlastGame;

/// <summary>
/// CSV「ステージ構成資料」のショップアイテム表に基づき、
///   ・ShopItemEffectTable    (倍率・個数の中央テーブル)
///   ・ShopRarityVisualTable  (レアリティの見た目)
///   ・ShopItemData × 27      (アイテム1個ずつ)
///   ・ShopItemDatabase       (上の 27 個を集約)
///   ・ShopItemPool           (Stage shop / Stage vending / ClearReward)
/// を Assets/ScriptableObjects/Shop/ 以下に一括生成する。
///
/// メニュー: Tools / BlockBlast / Setup Shop Item Assets
/// </summary>
public static class ShopItemAssetGenerator
{
    const string SHOP_FOLDER      = "Assets/ScriptableObjects/Shop";
    const string ITEMS_FOLDER     = "Assets/ScriptableObjects/Shop/Items";
    const string POOLS_FOLDER     = "Assets/ScriptableObjects/Shop/Pools";

    // ─── ID → (category, tierIndex, rarity, price, displayName) の表 (CSV と一致) ───
    struct ItemRow
    {
        public int id;
        public ShopItemCategory category;
        public int tierIndex;
        public Rarity rarity;
        public int price;
        public string displayName;     // CSV のアイテム列
        public string defaultDescTemplate; // CSV の説明文列 (空文字 OK)
    }

    // CSV から起こした 27 行。
    // descriptionTemplate は CSV の説明文列をそのまま入れる (空欄は "" のまま)。
    // 効果値 (1.05倍など) は ShopItemEffectTable.values[tierIndex] から取り、
    // 説明文の {value} を差し替えるので、テンプレ側の値部分は {value} に置き換えてある。
    static readonly ItemRow[] ROWS = new ItemRow[]
    {
        // === 弾でかくなる ===
        new ItemRow{ id= 1, category=ShopItemCategory.BulletSize, tierIndex=0, rarity=Rarity.N,   price= 500, displayName="弾でかくなる", defaultDescTemplate="プリが{value}でかくなるぞっ！どーんとつよいっ！！"},
        new ItemRow{ id= 2, category=ShopItemCategory.BulletSize, tierIndex=1, rarity=Rarity.R,   price=1000, displayName="弾でかくなる", defaultDescTemplate="プリが{value}でかくなるぞっ！どーんとつよいっ！！"},
        new ItemRow{ id= 3, category=ShopItemCategory.BulletSize, tierIndex=2, rarity=Rarity.SR,  price=2500, displayName="弾でかくなる", defaultDescTemplate="プリが{value}でっかくなるぞっ！プリパワーアップ！！"},
        new ItemRow{ id= 4, category=ShopItemCategory.BulletSize, tierIndex=3, rarity=Rarity.SSR, price=4000, displayName="弾でかくなる", defaultDescTemplate="プリが{value}でっかくなるぞっ！ドカンといこうっ！！"},

        // === 弾速 ===
        new ItemRow{ id= 5, category=ShopItemCategory.BulletSpeed, tierIndex=0, rarity=Rarity.N,   price= 500, displayName="弾速", defaultDescTemplate="プリの速さが{value}になる！すぐとどくぞっ！！"},
        new ItemRow{ id= 6, category=ShopItemCategory.BulletSpeed, tierIndex=1, rarity=Rarity.R,   price=1000, displayName="弾速", defaultDescTemplate="プリの速さが{value}になる！すばやくとどくぞっ！！"},
        new ItemRow{ id= 7, category=ShopItemCategory.BulletSpeed, tierIndex=2, rarity=Rarity.SR,  price=2500, displayName="弾速", defaultDescTemplate="プリの速さが{value}になる！テキにとどけっ！！"},
        new ItemRow{ id= 8, category=ShopItemCategory.BulletSpeed, tierIndex=3, rarity=Rarity.SSR, price=4000, displayName="弾速", defaultDescTemplate="プリの速さが{value}になる！ビュンっといけーっ！！"},

        // === 弾数 ===
        new ItemRow{ id= 9, category=ShopItemCategory.BulletCount, tierIndex=0, rarity=Rarity.R,   price=1000, displayName="弾数", defaultDescTemplate="プリが{value}こふえるぞっ！ぽんぽんこうげきだっ！！"},
        new ItemRow{ id=10, category=ShopItemCategory.BulletCount, tierIndex=1, rarity=Rarity.SR,  price=2500, displayName="弾数", defaultDescTemplate="プリが{value}こふえるぞっ！れんぞくプリだっ！！"},
        new ItemRow{ id=11, category=ShopItemCategory.BulletCount, tierIndex=2, rarity=Rarity.SSR, price=4000, displayName="弾数", defaultDescTemplate="プリが{value}こふえるぞっ！もうプリまつりだっ！！"},

        // === 貫通 ===
        new ItemRow{ id=12, category=ShopItemCategory.Penetration, tierIndex=0, rarity=Rarity.R,   price=1000, displayName="貫通", defaultDescTemplate="プリがテキを{value}たいつらぬくぞっ！ズバッとぬけろっ！！"},
        new ItemRow{ id=13, category=ShopItemCategory.Penetration, tierIndex=1, rarity=Rarity.SR,  price=2500, displayName="貫通", defaultDescTemplate="プリがテキを{value}たいつらぬくぞっ！ズバズバいこうっ！！"},
        new ItemRow{ id=14, category=ShopItemCategory.Penetration, tierIndex=2, rarity=Rarity.SSR, price=4000, displayName="貫通", defaultDescTemplate="プリがテキを{value}たいつらぬくぞっ！まとめてズバーッ！！"},

        // === スピード上昇 (プレイヤー) ===
        new ItemRow{ id=15, category=ShopItemCategory.PlayerSpeed, tierIndex=0, rarity=Rarity.SR,  price=3000, displayName="スピード上昇", defaultDescTemplate="トゥインキィーのスピードが{value}になる！にげきるぞっ！！"},
        new ItemRow{ id=16, category=ShopItemCategory.PlayerSpeed, tierIndex=1, rarity=Rarity.SSR, price=4000, displayName="スピード上昇", defaultDescTemplate="トゥインキィーのスピードが{value}になる！ビューンとにげろっ！！"},

        // === ブロックリセット (盤面全消し / UR) ===
        new ItemRow{ id=17, category=ShopItemCategory.BlockReset, tierIndex=0, rarity=Rarity.UR,  price=8000, displayName="ブロックリセット", defaultDescTemplate="ブロックがぜんぶリセットされる！ぱぱっとおきなおそうっ！！"},

        // === ブロック救済 (盤面詰み救済) ===
        new ItemRow{ id=18, category=ShopItemCategory.BlockRescue, tierIndex=0, rarity=Rarity.SR,  price=3000, displayName="ブロック救済", defaultDescTemplate=""},

        // === お金増加 ===
        new ItemRow{ id=19, category=ShopItemCategory.MoneyBonus, tierIndex=0, rarity=Rarity.N,   price= 500, displayName="お金増加", defaultDescTemplate="お金が{value}ふえるぞっ！"},
        new ItemRow{ id=20, category=ShopItemCategory.MoneyBonus, tierIndex=1, rarity=Rarity.R,   price=1000, displayName="お金増加", defaultDescTemplate="お金が{value}ふえるぞっ！"},
        new ItemRow{ id=21, category=ShopItemCategory.MoneyBonus, tierIndex=2, rarity=Rarity.SR,  price=2500, displayName="お金増加", defaultDescTemplate="お金が{value}ふえるぞっ！"},
        new ItemRow{ id=22, category=ShopItemCategory.MoneyBonus, tierIndex=3, rarity=Rarity.SSR, price=4000, displayName="お金増加", defaultDescTemplate="お金が{value}ふえるぞっ！"},

        // === 運UP ===
        new ItemRow{ id=23, category=ShopItemCategory.LuckUp, tierIndex=0, rarity=Rarity.N,   price= 500, displayName="運UP", defaultDescTemplate="良いブロックが出やすくなる {value}"},
        new ItemRow{ id=24, category=ShopItemCategory.LuckUp, tierIndex=1, rarity=Rarity.R,   price=1000, displayName="運UP", defaultDescTemplate="良いブロックが出やすくなる {value}"},
        new ItemRow{ id=25, category=ShopItemCategory.LuckUp, tierIndex=2, rarity=Rarity.SR,  price=2500, displayName="運UP", defaultDescTemplate="良いブロックが出やすくなる {value}"},
        new ItemRow{ id=26, category=ShopItemCategory.LuckUp, tierIndex=3, rarity=Rarity.SSR, price=4000, displayName="運UP", defaultDescTemplate="良いブロックが出やすくなる {value}"},

        // === 救済 (追いつかれた際に一度助かる) ===
        new ItemRow{ id=27, category=ShopItemCategory.SaveOnce, tierIndex=0, rarity=Rarity.SSR, price=4000, displayName="救済", defaultDescTemplate=""},
    };

    [MenuItem("Tools/BlockBlast/Setup Shop Item Assets")]
    public static void Setup()
    {
        EnsureFolder(SHOP_FOLDER);
        EnsureFolder(ITEMS_FOLDER);
        EnsureFolder(POOLS_FOLDER);

        var effectTable  = CreateOrLoad<ShopItemEffectTable>(SHOP_FOLDER + "/ShopItemEffectTable.asset");
        PopulateEffectTable(effectTable);
        EditorUtility.SetDirty(effectTable);

        var rarityTable  = CreateOrLoad<ShopRarityVisualTable>(SHOP_FOLDER + "/ShopRarityVisualTable.asset");
        PopulateRarityTable(rarityTable);
        EditorUtility.SetDirty(rarityTable);

        var items = CreateOrLoadAllItems();

        var database = CreateOrLoad<ShopItemDatabase>(SHOP_FOLDER + "/ShopItemDatabase.asset");
        database.items = items;
        EditorUtility.SetDirty(database);

        CreateOrLoadPools();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ShopItemAssetGenerator] 完了: items={items.Count}, effectTable / rarityVisualTable / database / pools 生成。\n" +
                  $"参照: {SHOP_FOLDER}/");
        EditorUtility.RevealInFinder(SHOP_FOLDER + "/ShopItemDatabase.asset");
    }

    // ─────────────────────────────────────
    //  Effect table
    // ─────────────────────────────────────

    static void PopulateEffectTable(ShopItemEffectTable table)
    {
        // 既存エントリは category 単位でマージ (= 一度生成した値を Generator で上書きしない)
        var byCat = new Dictionary<ShopItemCategory, ShopItemEffectTable.CategoryEntry>();
        foreach (var e in table.entries) if (e != null) byCat[e.category] = e;

        UpsertEntry(table, byCat, ShopItemCategory.BulletSize,   new List<float>{1.01f,1.02f,1.05f,1.10f}, "{0}倍", false);
        UpsertEntry(table, byCat, ShopItemCategory.BulletSpeed,  new List<float>{1.05f,1.20f,1.50f,2.00f}, "{0}倍", false);
        UpsertEntry(table, byCat, ShopItemCategory.BulletCount,  new List<float>{1f,3f,5f},               "プラス{0}", true);
        UpsertEntry(table, byCat, ShopItemCategory.Penetration,  new List<float>{1f,3f,5f},               "プラス{0}", true);
        UpsertEntry(table, byCat, ShopItemCategory.PlayerSpeed,  new List<float>{1.10f,1.25f},            "{0}倍", false);
        UpsertEntry(table, byCat, ShopItemCategory.BlockReset,   new List<float>{1f},                     "全リセット", true);
        UpsertEntry(table, byCat, ShopItemCategory.BlockRescue,  new List<float>{1f},                     "盤面救済", true);
        UpsertEntry(table, byCat, ShopItemCategory.MoneyBonus,   new List<float>{100f,300f,500f,1000f},   "プラス{0}", true);
        UpsertEntry(table, byCat, ShopItemCategory.LuckUp,       new List<float>{1f,3f,5f,10f},           "+{0}%",     true);
        UpsertEntry(table, byCat, ShopItemCategory.SaveOnce,     new List<float>{1f},                     "一度助かる", true);
    }

    static void UpsertEntry(ShopItemEffectTable table,
                            Dictionary<ShopItemCategory, ShopItemEffectTable.CategoryEntry> byCat,
                            ShopItemCategory cat, List<float> defaults, string fmt, bool intMode)
    {
        if (byCat.TryGetValue(cat, out var existing))
        {
            // 既存があれば配列長だけ補完する (= ユーザーが触った値は残す)
            while (existing.values.Count < defaults.Count) existing.values.Add(defaults[existing.values.Count]);
            if (string.IsNullOrEmpty(existing.displayFormat)) existing.displayFormat = fmt;
            existing.forceInteger = intMode;
            return;
        }
        var ne = new ShopItemEffectTable.CategoryEntry
        {
            category = cat,
            values = new List<float>(defaults),
            displayFormat = fmt,
            forceInteger = intMode,
        };
        table.entries.Add(ne);
    }

    // ─────────────────────────────────────
    //  Rarity visual table
    // ─────────────────────────────────────

    static void PopulateRarityTable(ShopRarityVisualTable table)
    {
        var byR = new Dictionary<Rarity, ShopRarityVisualTable.RarityEntry>();
        foreach (var e in table.entries) if (e != null) byR[e.rarity] = e;

        // デフォルト色 (好きに調整してください)
        //  frame:  枠色   / bg: 背景色 / text: 名前・バッジ色 / value: 説明文の倍率/個数部分の色 / price: 値段テキストの色 (買えるとき)
        UpsertRarity(table, byR, Rarity.N,   "N",
            frame: new Color(0.78f,0.78f,0.78f),
            bg:    new Color(0.94f,0.94f,0.94f),
            text:  Color.black,
            value: new Color(0.30f,0.30f,0.30f),
            price: new Color(0.20f,0.20f,0.20f),
            outline: Color.white,
            outlineWidth: 0.08f,
            priceOutline: Color.white,
            priceOutlineWidth: 0.08f,
            valueOutline: Color.white,
            valueOutlineWidth: 0.08f);
        UpsertRarity(table, byR, Rarity.R,   "R",
            frame: new Color(0.40f,0.78f,1.00f),
            bg:    new Color(0.85f,0.93f,1.00f),
            text:  new Color(0.05f,0.25f,0.45f),
            value: new Color(0.10f,0.45f,0.85f),
            price: new Color(0.10f,0.35f,0.70f),
            outline: Color.white,
            outlineWidth: 0.08f,
            priceOutline: Color.white,
            priceOutlineWidth: 0.08f,
            valueOutline: Color.white,
            valueOutlineWidth: 0.08f);
        UpsertRarity(table, byR, Rarity.SR,  "SR",
            frame: new Color(0.65f,0.30f,1.00f),
            bg:    new Color(0.92f,0.85f,1.00f),
            text:  new Color(0.35f,0.10f,0.55f),
            value: new Color(0.70f,0.25f,0.95f),
            price: new Color(0.50f,0.15f,0.75f),
            outline: Color.white,
            outlineWidth: 0.09f,
            priceOutline: Color.white,
            priceOutlineWidth: 0.09f,
            valueOutline: Color.white,
            valueOutlineWidth: 0.09f);
        UpsertRarity(table, byR, Rarity.SSR, "SSR",
            frame: new Color(1.00f,0.78f,0.20f),
            bg:    new Color(1.00f,0.97f,0.85f),
            text:  new Color(0.55f,0.35f,0.05f),
            value: new Color(1.00f,0.60f,0.00f),
            price: new Color(0.90f,0.50f,0.05f),
            outline: Color.white,
            outlineWidth: 0.10f,
            priceOutline: Color.white,
            priceOutlineWidth: 0.10f,
            valueOutline: Color.white,
            valueOutlineWidth: 0.10f);
        UpsertRarity(table, byR, Rarity.UR,  "UR",
            frame: new Color(1.00f,0.40f,0.40f),
            bg:    new Color(1.00f,0.85f,0.85f),
            text:  new Color(0.70f,0.05f,0.05f),
            value: new Color(1.00f,0.20f,0.40f),
            price: new Color(0.85f,0.10f,0.30f),
            outline: Color.white,
            outlineWidth: 0.10f,
            priceOutline: Color.white,
            priceOutlineWidth: 0.10f,
            valueOutline: Color.white,
            valueOutlineWidth: 0.10f);
    }

    static void UpsertRarity(ShopRarityVisualTable table,
                             Dictionary<Rarity, ShopRarityVisualTable.RarityEntry> byR,
                             Rarity rarity, string label, Color frame, Color bg, Color text, Color value, Color price,
                                 Color outline, float outlineWidth, Color priceOutline, float priceOutlineWidth,
                                 Color valueOutline, float valueOutlineWidth)
    {
        if (byR.ContainsKey(rarity)) return;
        var ne = new ShopRarityVisualTable.RarityEntry
        {
            rarity = rarity,
            displayLabel = label,
            frameColor = frame,
            backgroundColor = bg,
            textColor = text,
            valueColor = value,
            priceColor = price,
            textOutlineColor = outline,
            textOutlineWidth = outlineWidth,
            priceTextOutlineColor = priceOutline,
            priceTextOutlineWidth = priceOutlineWidth,
            valueTextOutlineColor = valueOutline,
            valueTextOutlineWidth = valueOutlineWidth,
        };
        table.entries.Add(ne);
    }

    // ─────────────────────────────────────
    //  Item assets
    // ─────────────────────────────────────

    static List<ShopItemData> CreateOrLoadAllItems()
    {
        var list = new List<ShopItemData>();
        foreach (var row in ROWS)
        {
            string assetName = $"ShopItem_{row.id:D2}_{row.category}_{row.rarity}.asset";
            string path = $"{ITEMS_FOLDER}/{assetName}";
            var asset = AssetDatabase.LoadAssetAtPath<ShopItemData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ShopItemData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.id          = row.id;
            asset.category    = row.category;
            asset.tierIndex   = row.tierIndex;
            asset.rarity      = row.rarity;
            asset.price       = row.price;
            asset.displayName = row.displayName;
            if (string.IsNullOrEmpty(asset.descriptionTemplate))
                asset.descriptionTemplate = row.defaultDescTemplate;
            EditorUtility.SetDirty(asset);
            list.Add(asset);
        }
        list.Sort((a, b) => a.id.CompareTo(b.id));
        return list;
    }

    // ─────────────────────────────────────
    //  Pool assets (ステージ毎)
    // ─────────────────────────────────────

    static void CreateOrLoadPools()
    {
        // CSV のショップ行に直接対応
        CreatePool("Stage1Shop_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR},                  6, false, 1.0f);
        CreatePool("Stage2Shop_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR, Rarity.SSR},      6, false, 1.0f);
        CreatePool("Stage3Shop_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR, Rarity.SSR},      6, true,  0.5f);  // SSR セール確定
        CreatePool("Stage4Shop_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR, Rarity.SSR},      6, false, 1.0f);
        CreatePool("Stage5Shop_Pool", new[]{Rarity.R, Rarity.SR, Rarity.SSR},                6, false, 1.0f);
        CreatePool("Stage6Shop_Pool", new[]{Rarity.R, Rarity.SR, Rarity.SSR},                6, false, 1.0f);
        CreatePool("Stage7Shop_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR, Rarity.SSR, Rarity.UR}, 6, true, 0.5f); // SSR セール確定
        // CSV の自販機行に直接対応 (VendingMachineFlowController.vendingStagePools に割り当てる)
        CreatePool("Stage3Vending1_Pool", new[]{Rarity.N, Rarity.R},                         3, false, 1.0f);
        CreatePool("Stage7Vending1_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR},              3, false, 1.0f);
        CreatePool("Stage8Vending1_Pool", new[]{Rarity.N, Rarity.R, Rarity.SR},              3, false, 1.0f);
        CreatePool("Stage8Vending2_Pool", new[]{Rarity.R, Rarity.SR},                        3, false, 1.0f);
        CreatePool("Stage8Vending3_Pool", new[]{Rarity.R, Rarity.SR},                        3, false, 1.0f);

        // 汎用フォールバック
        CreatePool("Vending3_Pool",   new[]{Rarity.N, Rarity.R, Rarity.SR},                  3, false, 1.0f);
        CreatePool("ClearReward_Pool",new[]{Rarity.SSR, Rarity.UR},                          3, false, 1.0f);
    }

    static ShopItemPool CreatePool(string name, Rarity[] rarities, int count, bool ssrSale, float discount)
    {
        string path = $"{POOLS_FOLDER}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ShopItemPool>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ShopItemPool>();
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.poolName = name;
        asset.allowedRarities = new List<Rarity>(rarities);
        asset.drawCount = count;
        asset.guaranteedSsrSale = ssrSale;
        asset.ssrSaleDiscount = discount;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    // ─────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────

    static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        var leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
