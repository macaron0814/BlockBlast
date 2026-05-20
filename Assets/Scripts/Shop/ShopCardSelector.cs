using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップ UI のカード群と「買う」ボタンを束ねるコントローラ。
    ///
    /// ■ 動作仕様
    ///   ・cards に並べた ShopCard をタップすると 1 枚だけがピックアップされる
    ///     (タップしたカード = idleScale → selectedScale, 既存の選択は idleScale に戻る)
    ///   ・カードが選択されていないとき: 「買う」ボタンは押せない (interactable=false)
    ///     さらに HueShiftFilter があれば saturation=0 にしてグレースケール表示
    ///   ・カードが選択されているとき: 「買う」ボタンは押せる (彩度復帰)
    ///   ・「買う」ボタン押下時に PlayerWallet.CanAfford(card.cost) で判定し
    ///       - 買えるパターン → onPurchaseAffordable
    ///       - 買えないパターン → onPurchaseInsufficientFunds
    ///     をそれぞれ発火する。実際の購入 (在庫消費 / アイテム付与) や
    ///     「お金が足りません」演出は、これらのコールバックに後から繋ぐ想定。
    ///
    /// ■ セットアップ
    ///   1. シーン上の任意の親 GameObject にこのスクリプトをアタッチ
    ///   2. cards に 6 枚の ShopCard をドラッグ
    ///   3. buyButton に「買う」ボタンを割り当て
    ///   4. (任意) buyButtonFilter にボタン Image の HueShiftFilter を割り当てると
    ///      ちゃんとグレースケール化される。無ければ disabledTint の単色で代用。
    /// </summary>
    public class ShopCardSelector : MonoBehaviour
    {
        [Header("Cards (6 枚想定)")]
        [Tooltip("並んでいるカードを順番に登録。各カード GameObject の ShopCard を割り当てる。")]
        public List<ShopCard> cards = new List<ShopCard>();

        [Header("Buy Button")]
        [Tooltip("「買う」ボタン")]
        public Button buyButton;

        [Tooltip("ボタンの見た目 (グレースケール用)。HueShiftFilter があればそちらが優先")]
        public Image buyButtonImage;

        [Tooltip("HueShift シェーダーで彩度を 0 にしてグレースケール化する用 (任意)")]
        public HueShiftFilter buyButtonFilter;

        [Header("Color Tint Fallback (HueShiftFilter 未指定時のみ使用)")]
        [Tooltip("非アクティブ時の色 (グレースケール代替)")]
        public Color disabledTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("有効時の色")]
        public Color enabledTint = Color.white;

        [Header("Selected Price Display")]
        [Tooltip("選択中カードの価格表示ルート。選択なしのとき非表示にしたい場合に指定")]
        public GameObject selectedPriceRoot;

        [Tooltip("選択中カードの価格表示 (uGUI Text)。任意")]
        public Text selectedPriceText;

        [Tooltip("選択中カードの価格表示 (TMP)。任意")]
        public TMP_Text selectedPriceTextTMP;

        [Tooltip("価格表示フォーマット。{0} に価格が入る")]
        public string selectedPriceFormat = "{0}";

        [Tooltip("選択解除時に表示する文字。空なら空欄")]
        public string noSelectionPriceText = "";

        [Header("Wallet Source")]
        [Tooltip("参照するウォレット (空のとき PlayerWallet.Instance を使用)")]
        public PlayerWallet wallet;

        [Header("Item Data Source")]
        [Tooltip("ShopItemData の集約。Open 時にここから抽選してカードに反映する。")]
        public ShopItemDatabase itemDatabase;

        [Tooltip("効果値の中央テーブル (倍率/個数)。null だと説明文の {value} は素の文字列のまま。")]
        public ShopItemEffectTable effectTable;

        [Tooltip("レアリティの見た目テーブル (枠色 / バッジ等)")]
        public ShopRarityVisualTable rarityVisualTable;

        [Tooltip("デフォルト抽出プール。FillCardsFromPool() で明示指定しなければこれが使われる。\n" +
                 "(例: Stage1Shop_Pool / Stage2Shop_Pool / Vending3_Pool)")]
        public ShopItemPool defaultPool;

        [Header("Behavior")]
        [Tooltip("同じカードを再タップしたとき選択を解除する (true: トグル動作, false: 維持)")]
        public bool toggleSameCardDeselects = false;

        [Header("Events")]
        [Tooltip("選択中で、所持金が足りているとき発火 (= 買えるパターン)。実際の購入処理はここに後から繋ぐ。")]
        public UnityEvent<ShopCard> onPurchaseAffordable;

        [Tooltip("選択中だが所持金が足りないとき発火 (= 買えないパターン)。'お金が足りません' 演出を後で繋ぐ。")]
        public UnityEvent<ShopCard> onPurchaseInsufficientFunds;

        [Tooltip("選択中のカードが変わるたび発火 (null = 選択解除)")]
        public UnityEvent<ShopCard> onSelectionChanged;

        ShopCard _selected;

        public ShopCard SelectedCard => _selected;
        public bool HasSelection => _selected != null;

        // ─────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────

        void OnEnable()
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                c.OnClicked -= HandleCardClicked;
                c.OnClicked += HandleCardClicked;
                c.ResetVisualToIdle();
                c.RefreshAffordability();
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(HandleBuyButton);
                buyButton.onClick.AddListener(HandleBuyButton);
            }

            _selected = null;
            UpdateBuyButtonVisual();
            UpdateSelectedPriceVisual();
            onSelectionChanged?.Invoke(null);
        }

        void OnDisable()
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                c.OnClicked -= HandleCardClicked;
            }

            if (buyButton != null)
                buyButton.onClick.RemoveListener(HandleBuyButton);
        }

        // ─────────────────────────────────────
        //  Card click
        // ─────────────────────────────────────

        void HandleCardClicked(ShopCard card)
        {
            if (card == null) return;

            // 所持金不足のカードはピックアップさせない (二重ガード)
            if (!card.IsAffordable) return;

            if (_selected == card)
            {
                if (toggleSameCardDeselects)
                {
                    _selected.SetSelected(false);
                    _selected = null;
                    UpdateBuyButtonVisual();
                    UpdateSelectedPriceVisual();
                    onSelectionChanged?.Invoke(null);
                }
                return;
            }

            if (_selected != null)
                _selected.SetSelected(false);

            _selected = card;
            _selected.SetSelected(true);

            UpdateBuyButtonVisual();
            UpdateSelectedPriceVisual();
            onSelectionChanged?.Invoke(_selected);
        }

        // ─────────────────────────────────────
        //  Buy button
        // ─────────────────────────────────────

        void HandleBuyButton()
        {
            if (_selected == null) return;

            var w = ResolveWallet();
            int cost = _selected.cost;

            // 買えないパターン (所持金不足)
            if (w != null && !w.CanAfford(cost))
            {
                Debug.Log($"[ShopCardSelector] 所持金不足: cost={cost} / total={w.TotalAssets}");
                onPurchaseInsufficientFunds?.Invoke(_selected);
                return;
            }

            // 買えるパターン (実際の購入処理は後でここに繋ぐ)
            Debug.Log($"[ShopCardSelector] 購入可能: card='{_selected.displayName}' cost={cost}");
            onPurchaseAffordable?.Invoke(_selected);
        }

        PlayerWallet ResolveWallet()
        {
            if (wallet != null) return wallet;
            return PlayerWallet.Instance;
        }

        // ─────────────────────────────────────
        //  Visual
        // ─────────────────────────────────────

        void UpdateBuyButtonVisual()
        {
            bool hasSelection = HasSelection;
            if (buyButton != null)
                buyButton.interactable = hasSelection;

            ApplyButtonGrayscale(!hasSelection);
        }

        void ApplyButtonGrayscale(bool grayscale)
        {
            // 優先: HueShiftFilter (彩度 0 で本来のグレースケール)
            if (buyButtonFilter != null)
            {
                buyButtonFilter.saturation = grayscale ? 0f : 1f;
                buyButtonFilter.brightness = grayscale ? 0.85f : 1f;
                buyButtonFilter.Apply();
                return;
            }

            // フォールバック: Image.color の単色 tint
            if (buyButtonImage != null)
                buyButtonImage.color = grayscale ? disabledTint : enabledTint;
        }

        // ─────────────────────────────────────
        //  Public API (外部から制御したい場合用)
        // ─────────────────────────────────────

        public void DeselectAll()
        {
            if (_selected != null)
                _selected.SetSelected(false);
            _selected = null;
            UpdateBuyButtonVisual();
            UpdateSelectedPriceVisual();
            onSelectionChanged?.Invoke(null);
        }

        void UpdateSelectedPriceVisual()
        {
            bool hasSelection = _selected != null;
            if (selectedPriceRoot != null)
                selectedPriceRoot.SetActive(hasSelection);

            string text = hasSelection
                ? string.Format(selectedPriceFormat, _selected.cost)
                : noSelectionPriceText;

            if (selectedPriceText != null) selectedPriceText.text = text;
            if (selectedPriceTextTMP != null) selectedPriceTextTMP.text = text;
        }

        public void RebuildBindings()
        {
            OnDisable();
            OnEnable();
        }

        // ─────────────────────────────────────
        //  アイテム抽選 → カード反映
        // ─────────────────────────────────────

        /// <summary>
        /// ShopItemPool を使ってカードに ShopItemData を流し込む。
        /// pool=null のときは defaultPool を使う。database が無いときは何もしない。
        /// </summary>
        public void FillCardsFromPool(ShopItemPool pool = null)
        {
            var usePool = pool != null ? pool : defaultPool;
            if (usePool == null || itemDatabase == null)
            {
                Debug.LogWarning("[ShopCardSelector] FillCardsFromPool: pool または itemDatabase が未設定");
                return;
            }

            var picks = usePool.Draw(itemDatabase);
            ApplyPicksToCards(picks);
        }

        /// <summary>抽選結果リストをカード群に反映する。</summary>
        public void ApplyPicksToCards(List<ShopItemPool.DrawResult> picks)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                if (i < picks.Count && picks[i] != null && picks[i].item != null)
                {
                    var p = picks[i];
                    card.gameObject.SetActive(true);
                    card.Apply(p.item, effectTable, rarityVisualTable, p.finalPrice, p.isOnSale);
                }
                else
                {
                    // 抽選結果が枚数に満たない場合は余ったカードを非表示
                    card.ClearItem();
                    card.gameObject.SetActive(false);
                }
            }

            DeselectAll();
        }
    }
}
