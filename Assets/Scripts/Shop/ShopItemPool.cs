using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 「どのレアリティから何枚抽出するか」を定義したショップ出現プール。
    ///
    /// CSV のステージ構成資料に対応:
    ///   Stage1 ショップ    : N, R, SR     からランダム 6 個
    ///   Stage2 ショップ    : N, R, SR, SSR からランダム 6 個
    ///   Stage3 ショップ    : N, R, SR, SSR からランダム 6 個 (SSR セール確定)
    ///   Stage7 ショップ    : N, R, SR, SSR, UR からランダム 6 個 (SSR セール確定)
    ///   自販機 (Vending)  : N, R, SR     からランダム 3 個
    ///   クリア報酬        : SSR, UR      からランダム 3 個
    ///
    /// Drawer (= ShopCardSelector / ShopFlowController) はこのプールを 1 つ受け取り、
    /// DrawItems(database, rng) で抽選結果を返す。
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItemPool", menuName = "BlockBlast/Shop/Item Pool")]
    public class ShopItemPool : ScriptableObject
    {
        [Tooltip("プール名 (デバッグ表示用)。例: \"Stage1Shop\" / \"Stage7Shop\" / \"Vending3\"")]
        public string poolName;

        [Tooltip("このプールが対象とするレアリティ。\n" +
                 "CSV: \"N, R, SR からランダム6つ\" などに直接対応。")]
        public List<Rarity> allowedRarities = new List<Rarity>();

        [Tooltip("抽出する枚数。通常は 6 (ショップ) / 3 (自販機・クリア報酬)。")]
        public int drawCount = 6;

        [Tooltip("重複アイテムを許可するか。\n" +
                 "OFF (デフォルト) なら同じ ShopItemData が同じショップに 2 個並ぶことはない。")]
        public bool allowDuplicates = false;

        [Header("SSR セール (任意)")]
        [Tooltip("ON: 抽出結果に必ず 1 個以上 SSR を含める。さらにその SSR は割引価格になる。\n" +
                 "Stage3 / Stage7 の \"SSR セール確定\" 用。")]
        public bool guaranteedSsrSale = false;

        [Tooltip("SSR セール時の価格倍率 (0.5 = 半額)")]
        [Range(0.05f, 1f)]
        public float ssrSaleDiscount = 0.5f;

        // ─────────────────────────────────────
        //  抽選
        // ─────────────────────────────────────

        /// <summary>抽選結果の 1 枚分。</summary>
        public class DrawResult
        {
            public ShopItemData item;
            public int finalPrice;
            public bool isOnSale;
        }

        /// <summary>
        /// database から allowedRarities に該当するアイテムを drawCount 枚抽出する。
        /// SSR セール指定があれば 1 枚を SSR + 割引にする。
        /// </summary>
        public List<DrawResult> Draw(ShopItemDatabase database, System.Random rng = null)
        {
            var results = new List<DrawResult>();
            if (database == null) return results;
            if (allowedRarities == null || allowedRarities.Count == 0) return results;

            rng ??= new System.Random();

            var candidates = database.GetByRarities(allowedRarities);
            if (candidates.Count == 0) return results;

            int remaining = drawCount;

            // SSR セール確定 (まず SSR を 1 個確保)
            if (guaranteedSsrSale && remaining > 0)
            {
                var ssrCandidates = database.GetByRarity(Rarity.SSR);
                if (ssrCandidates.Count > 0)
                {
                    var pick = ssrCandidates[rng.Next(ssrCandidates.Count)];
                    int salePrice = Mathf.Max(1, Mathf.RoundToInt(pick.price * ssrSaleDiscount));
                    results.Add(new DrawResult { item = pick, finalPrice = salePrice, isOnSale = true });
                    if (!allowDuplicates) candidates.Remove(pick);
                    remaining--;
                }
            }

            while (remaining > 0 && candidates.Count > 0)
            {
                int idx = rng.Next(candidates.Count);
                var pick = candidates[idx];
                results.Add(new DrawResult { item = pick, finalPrice = pick.price, isOnSale = false });
                if (!allowDuplicates) candidates.RemoveAt(idx);
                remaining--;
            }

            return results;
        }
    }
}
