using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace BlockBlastGame
{
    /// <summary>
    /// 「ステージクリア → ショップ表示 → 1 個購入で閉じて次ステージへ」というフロー全体を担当する。
    ///
    /// ■ 動作
    ///   ・GameEvents.OnStageClear を購読
    ///       └ shopPanel を Active に
    ///       └ ShopCardSelector の選択状態を初期化
    ///   ・ShopCardSelector.onPurchaseAffordable で 1 個購入完了
    ///       └ PlayerWallet.TrySpend(card.cost)
    ///       └ shopPanel を Inactive に
    ///       └ GameManager.ProceedToNextStage() で次ステージへ
    ///   ・OnSpaceshipBuild (ステージ 5 クリアなど) は購読しないので、
    ///     既存のスペースシップ Build フローはそのまま動く。
    ///
    /// ■ セットアップ
    ///   1. シーン上の任意の永続 GameObject にこのコンポーネントをアタッチ
    ///   2. shopPanel … 6 枚カードと買うボタンを含むショップ Canvas / Panel をアサイン
    ///   3. cardSelector … shopPanel 上の ShopCardSelector をアサイン
    ///   4. UIManager の useShopInsteadOfPerkPanel が true になっていることを確認
    ///      (このコンポーネントの autoDisablePerkPanel が true なら自動で設定する)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ShopFlowController : MonoBehaviour
    {
        [Header("Shop UI")]
        [Tooltip("ショップ Canvas / Panel のルート GameObject (SetActive で開閉)")]
        public GameObject shopPanel;

        [Tooltip("shopPanel 内の ShopCardSelector を割り当て")]
        public ShopCardSelector cardSelector;

        [Header("Purchase")]
        [Tooltip("購入時に PlayerWallet から代金を差し引く")]
        public bool deductMoneyOnPurchase = true;

        [Tooltip("購入後、ショップを閉じるまでの遅延 (秒)。フィードバック演出用")]
        public float closeDelayAfterPurchase = 0.35f;

        [Tooltip("購入後、自動で次ステージへ進める")]
        public bool autoProceedAfterPurchase = true;

        [Header("Open Behavior")]
        [Tooltip("起動時 (Awake) にショップを非表示にする")]
        public bool hideShopOnAwake = true;

        [Tooltip("UIManager の useShopInsteadOfPerkPanel を自動で true にする (パーク 3 択 UI を抑制)")]
        public bool autoDisablePerkPanel = true;

        [Tooltip("ON: ShopArrivalSequence の演出が走っているとき、OnStageClear で直接 OpenShop しない\n" +
                 "(Sequence が中央到着時に自前で OpenShop を呼ぶ)\n" +
                 "Sequence を使わない場合 (パークパネルの完全代替として shop を即時表示したい場合) は OFF")]
        public bool deferOpenWhenArrivalSequenceActive = true;

        [Tooltip("オプション。明示的にアサインすれば検索コストが減る。空なら FindObjectOfType で自動取得")]
        public ShopArrivalSequence arrivalSequence;

        [Header("Events")]
        [Tooltip("ショップが開いたとき発火")]
        public UnityEvent onShopOpened;

        [Tooltip("購入が成立したとき発火 (代金差し引き後)")]
        public UnityEvent<ShopCard> onPurchaseCompleted;

        [Tooltip("ショップが閉じたとき発火 (次ステージに進む直前)")]
        public UnityEvent onShopClosed;

        [Header("Runtime (read only)")]
        [SerializeField] bool _shopOpen;
        [SerializeField] bool _purchaseInFlight;

        public bool IsShopOpen => _shopOpen;

        // ─────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────

        void Awake()
        {
            if (hideShopOnAwake && shopPanel != null)
                shopPanel.SetActive(false);

            if (autoDisablePerkPanel)
            {
                var ui = FindObjectOfType<UIManager>();
                if (ui != null)
                    ui.useShopInsteadOfPerkPanel = true;
            }
        }

        void OnEnable()
        {
            GameEvents.OnStageClear += HandleStageClear;
            BindSelectorEvents(true);
        }

        void OnDisable()
        {
            GameEvents.OnStageClear -= HandleStageClear;
            BindSelectorEvents(false);
        }

        void BindSelectorEvents(bool subscribe)
        {
            if (cardSelector == null) return;
            cardSelector.onPurchaseAffordable.RemoveListener(HandlePurchaseAffordable);
            cardSelector.onPurchaseInsufficientFunds.RemoveListener(HandlePurchaseInsufficient);
            if (subscribe)
            {
                cardSelector.onPurchaseAffordable.AddListener(HandlePurchaseAffordable);
                cardSelector.onPurchaseInsufficientFunds.AddListener(HandlePurchaseInsufficient);
            }
        }

        // ─────────────────────────────────────
        //  Stage clear → open shop
        // ─────────────────────────────────────

        void HandleStageClear()
        {
            if (shopPanel == null)
            {
                Debug.LogWarning("[ShopFlowController] shopPanel が未設定です。");
                ProceedToNextStageImmediate();
                return;
            }

            // ShopArrivalSequence による到着演出中は、OpenShop は演出側に任せる。
            // ここで OpenShop してしまうと敵退場・ショップ画像スライドが一切再生されない。
            if (deferOpenWhenArrivalSequenceActive)
            {
                if (arrivalSequence == null)
                    arrivalSequence = FindObjectOfType<ShopArrivalSequence>();
                if (arrivalSequence != null && arrivalSequence.IsSequenceRunning)
                {
                    Debug.Log("[ShopFlowController] ShopArrivalSequence 実行中のため OpenShop を保留 (演出側で開く)");
                    return;
                }
            }

            OpenShop();
        }

        public void OpenShop()
        {
            if (_shopOpen) return;
            _shopOpen = true;
            _purchaseInFlight = false;

            if (cardSelector != null)
                cardSelector.DeselectAll();

            shopPanel.SetActive(true);
            onShopOpened?.Invoke();
        }

        // ─────────────────────────────────────
        //  Purchase
        // ─────────────────────────────────────

        void HandlePurchaseAffordable(ShopCard card)
        {
            if (!_shopOpen || _purchaseInFlight) return;
            if (card == null) return;
            _purchaseInFlight = true;

            if (deductMoneyOnPurchase)
            {
                var wallet = PlayerWallet.Instance;
                if (wallet != null)
                    wallet.TrySpend(card.cost);
            }

            // ※ カードに紐づく実際の効果適用 (アイテム付与・パーク発動 etc.) は
            //   onPurchaseCompleted の購読側で後から実装する想定。
            onPurchaseCompleted?.Invoke(card);

            StartCoroutine(CloseAfterDelay(closeDelayAfterPurchase));
        }

        void HandlePurchaseInsufficient(ShopCard card)
        {
            // 「お金が足りません」演出は ShopCardSelector.onPurchaseInsufficientFunds で
            // 後から繋ぐ予定。ここでは何もせずショップを開いたままにする。
        }

        IEnumerator CloseAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            CloseShop();

            if (autoProceedAfterPurchase)
                ProceedToNextStageImmediate();
        }

        public void CloseShop()
        {
            if (!_shopOpen) return;
            _shopOpen = false;
            _purchaseInFlight = false;

            if (cardSelector != null)
                cardSelector.DeselectAll();

            if (shopPanel != null)
                shopPanel.SetActive(false);

            onShopClosed?.Invoke();
        }

        void ProceedToNextStageImmediate()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.ProceedToNextStage();
        }
    }
}
