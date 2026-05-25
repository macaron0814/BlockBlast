using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// 「ルートマスが VendingMachine に到達 → 自販機 Canvas 表示 → 1 個購入 (または出口) で閉じる」
    /// という自販機フロー全体を担当する。
    ///
    /// ■ ショップとの違い
    ///   ・ステージ途中で発生する (= 次ステージへは進まない、現ステージを続行)
    ///   ・カードは 3 枚ランダム
    ///   ・到着時 (Canvas オープン直前) に GamePauseService.Pause("VendingMachine") で全停止する
    ///   ・閉じるときに GamePauseService.Resume("VendingMachine") で全停止を解除し、ステージを続行する
    ///   ・敵を後方に押し流す演出は無い (ShopArrivalSequence と違いここでは演出しない)
    ///
    /// ■ セットアップ
    ///   1. シーン上の任意の永続 GameObject にアタッチ
    ///   2. vendingMachinePanel … 3 枚カード + 買うボタン + 出口ボタンを含む自販機 Canvas/Panel
    ///   3. cardSelector … vendingMachinePanel 内の ShopCardSelector (カード 3 枚 / defaultPool に Vending 用プール)
    ///   4. exitButton    … 何も購入せずに自販機を出るためのボタン
    ///   5. arrivalSequence … VendingMachineArrivalSequence を割り当て (空なら自動取得)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class VendingMachineFlowController : MonoBehaviour
    {
        [Header("Vending Machine UI")]
        [Tooltip("自販機 Canvas / Panel のルート GameObject (SetActive で開閉)。\n"
               + "中には 3 枚カードと「買う」ボタン、「出口」ボタンが入っている想定。")]
        public GameObject vendingMachinePanel;

        [Tooltip("自販機表示中だけ出す所持金表示のルート GameObject。\n"
               + "雲や背景より前に出したい場合は、vendingMachinePanel とは別 Canvas / 別 Sorting Order にしてここへ割り当てる。")]
        public GameObject walletDisplayRoot;

        [Tooltip("vendingMachinePanel 内の ShopCardSelector (3 枚カード)。\n"
               + "defaultPool に Vending 用 (= 3 ピック想定) の ShopItemPool を割り当てておく。")]
        public ShopCardSelector cardSelector;

        [Header("Purchase")]
        [Tooltip("購入時に PlayerWallet から代金を差し引く")]
        public bool deductMoneyOnPurchase = true;

        [Tooltip("購入後、自販機を閉じるまでの遅延 (秒)。フィードバック演出用。\n"
               + "GamePauseService 解除前に走るので WaitForSecondsRealtime で待つ。")]
        public float closeDelayAfterPurchase = 0.35f;

        [Header("Exit Button")]
        [Tooltip("購入せずに自販機を出るボタン。押すと効果適用なしで Canvas を閉じてステージを続行する。")]
        public Button exitButton;

        [Tooltip("スキップボタンを押してから、自販機 Canvas を非表示にして元の画面へ戻り始めるまでの待ち時間 (秒)。\n"
               + "購入時には使わず、スキップ時だけ適用される。Time.timeScale=0 中でも進む Realtime 待ち。")]
        public float returnDelayAfterExit = 0f;

        [Tooltip("ON にすると onClick (=指を離した瞬間) ではなく PointerDown (=指を押した瞬間) で退出フローを開始する。\n"
               + "押した瞬間に退出アニメーションを始めたい場合は ON のままにする。")]
        public bool triggerExitOnPointerDown = true;

        [Header("Exit Animation")]
        [Tooltip("スキップ待ち時間が終わったタイミングで再生する Animator。未設定なら Trigger 再生はしない。")]
        public Animator exitAnimator;

        [Tooltip("Animator.Play で直接再生するステート名/クリップ名。\n"
               + "ここが空でなければ Trigger より優先して Animator.Play(name) を呼ぶ。")]
        public string exitAnimationStateName = "";

        [Tooltip("Animator.Play で使う Layer。通常は 0。")]
        public int exitAnimationLayer = 0;

        [Tooltip("Animator.Play の開始位置。0 = 先頭から。")]
        [Range(0f, 1f)]
        public float exitAnimationStartNormalizedTime = 0f;

        [Tooltip("exitAnimator に送る Trigger 名。空なら Trigger は送らない。")]
        public string exitAnimationTrigger = "";

        [Tooltip("Trigger / onExitAnimationStarted 発火後、Canvas を非表示にするまで待つ秒数。\n"
               + "0 ならアニメーション開始と同時に Canvas を非表示にする。Time.timeScale=0 中でも進む Realtime 待ち。\n"
               + "waitForExitAnimationToFinish が ON のときはこの値ではなく Animator の完了を待つ。")]
        public float exitAnimationDurationBeforeHide = 0f;

        [Tooltip("ON にすると exitAnimationDurationBeforeHide ではなく、Animator のステートが終わるまで待ってから Canvas を非表示にする。\n"
               + "exitAnimator + exitAnimationStateName が両方設定されているときに有効。")]
        public bool waitForExitAnimationToFinish = true;

        [Tooltip("Animator 完了待ちの安全タイマー (秒)。何らかの理由で normalizedTime が 1.0 まで進まなくても、この秒数で強制的に閉じる。")]
        public float exitAnimationMaxWaitSeconds = 5f;

        [Tooltip("ON にすると exitAnimator の UpdateMode を UnscaledTime に切り替えてから再生する。\n"
               + "自販機表示中は Time.timeScale=0 になっているため、これを ON にしないとアニメは進まない。\n"
               + "再生後は元の UpdateMode に戻す。")]
        public bool forceUnscaledTimeOnExitAnimator = true;

        [Tooltip("スキップ待ち時間が終わったタイミングで発火。Animation / Timeline / SE などをここに接続できる。")]
        public UnityEvent onExitAnimationStarted;

        [Header("Open Behavior")]
        [Tooltip("起動時 (Awake) に自販機を非表示にする")]
        public bool hideOnAwake = true;

        [Tooltip("ON: OpenVendingMachine 時に cardSelector.FillCardsFromPool() でカード抽選を呼び出す。\n"
               + "OFF: cardSelector に既に入っているカード設定をそのまま使う。")]
        public bool autoFillCardsOnOpen = true;

        [Tooltip("抽選に使う ShopItemPool。null なら cardSelector.defaultPool を使う。\n"
               + "Vending 用は 3 ピックの設定にしておくこと。")]
        public ShopItemPool vendingPool;

        [Tooltip("ステージ番号 + そのステージ内の自販機到達回数 → ShopItemPool のマッピング。\n"
               + "ShopFlowController.stagePools とは別枠。複数自販機があるステージでは occurrence を 1,2,3... で指定。\n"
               + "occurrence=0 は、そのステージのフォールバックとして使われる。")]
        public List<VendingStagePoolEntry> vendingStagePools = new List<VendingStagePoolEntry>();

        [System.Serializable]
        public class VendingStagePoolEntry
        {
            [Tooltip("対応するステージ番号 (1..N)")]
            public int stage = 1;

            [Tooltip("そのステージ内で何回目の自販機か。1=最初、2=2回目。0=そのステージのフォールバック")]
            public int occurrence = 0;

            [Tooltip("この自販機で使う抽出プール")]
            public ShopItemPool pool;
        }

        [Header("Arrival Sequence")]
        [Tooltip("自販機到着演出の参照。OnVendingMachineRouteNodeReached の受信で自販機を開くかどうかの判定に使う。\n"
               + "空なら自動取得 (FindObjectOfType)")]
        public VendingMachineArrivalSequence arrivalSequence;

        [Tooltip("ON: 到着演出 (VendingMachineArrivalSequence) が走っている間は、ここから直接 Open しない\n"
               + "(演出側が中央到着時に自前で OpenVendingMachine を呼ぶ)\n"
               + "OFF: 到着演出を使わずに、ルートマス到達と同時に Canvas を開く")]
        public bool deferOpenWhenArrivalSequenceActive = true;

        [Header("Pause Integration")]
        [Tooltip("ON: Canvas を開いた瞬間に GamePauseService.Pause で全停止する。\n"
               + "通常は ON 推奨。OFF にすると到着後も世界が動き続ける。")]
        public bool pauseWorldWhileOpen = true;

        [Tooltip("GamePauseService.Pause/Resume に渡すハンドル名 (= 衝突しないユニーク名)")]
        public string pauseHandle = "VendingMachine";

        [Header("Effect Application")]
        [Tooltip("ON: 購入時に PlayerEffectState.ApplyPurchase を呼び、アイテム効果を実反映する。\n"
               + "(弾サイズ / 弾速 / 弾数 / 貫通 / プレイヤースピード / 盤面リセット など)")]
        public bool applyEffectsOnPurchase = true;

        [Tooltip("空なら PlayerEffectState.Instance を自動取得")]
        public PlayerEffectState playerEffectState;

        [Header("Events")]
        [Tooltip("自販機 Canvas が開いたとき発火")]
        public UnityEvent onVendingOpened;

        [Tooltip("購入が成立したとき発火 (代金差し引き後)")]
        public UnityEvent<ShopCard> onPurchaseCompleted;

        [Tooltip("自販機 Canvas が閉じたとき発火 (= Pause 解除直前)")]
        public UnityEvent onVendingClosed;

        [Header("Runtime (read only)")]
        [SerializeField] bool _vendingOpen;
        [SerializeField] bool _purchaseInFlight;
        [SerializeField] bool _pauseActive;
        [SerializeField] int _currentStageForCount = -1;
        [SerializeField] int _vendingOpenCountInStage;

        AnimatorUpdateMode _exitAnimatorOriginalUpdateMode;
        bool _exitAnimatorUpdateModeSaved;

        public bool IsVendingOpen => _vendingOpen;

        // ─────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────

        void Awake()
        {
            if (hideOnAwake && vendingMachinePanel != null)
                vendingMachinePanel.SetActive(false);
            if (hideOnAwake && walletDisplayRoot != null)
                walletDisplayRoot.SetActive(false);

            if (arrivalSequence == null)
                arrivalSequence = FindObjectOfType<VendingMachineArrivalSequence>();
        }

        void OnEnable()
        {
            GameEvents.OnVendingMachineRouteNodeReached += HandleRouteNodeReached;
            GameEvents.OnStageChanged += HandleStageChanged;
            BindSelectorEvents(true);
            BindExitButton(true);
        }

        void OnDisable()
        {
            GameEvents.OnVendingMachineRouteNodeReached -= HandleRouteNodeReached;
            GameEvents.OnStageChanged -= HandleStageChanged;
            BindSelectorEvents(false);
            BindExitButton(false);
        }

        void HandleStageChanged(int stageNumber)
        {
            _currentStageForCount = stageNumber;
            _vendingOpenCountInStage = 0;
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

        void BindExitButton(bool subscribe)
        {
            if (exitButton == null) return;

            // PointerDown と onClick 両方を確実に外してから、有効モードのリスナーだけ付け直す。
            // (Inspector で triggerExitOnPointerDown を切り替えても二重発火しないようにするため)
            exitButton.onClick.RemoveListener(HandleExitButton);

            var trigger = exitButton.GetComponent<EventTrigger>();
            if (trigger != null)
            {
                if (trigger.triggers != null)
                {
                    for (int i = trigger.triggers.Count - 1; i >= 0; i--)
                    {
                        var entry = trigger.triggers[i];
                        if (entry != null && entry.eventID == EventTriggerType.PointerDown)
                            trigger.triggers.RemoveAt(i);
                    }
                }
            }

            if (!subscribe) return;

            if (triggerExitOnPointerDown)
            {
                if (trigger == null)
                    trigger = exitButton.gameObject.AddComponent<EventTrigger>();
                if (trigger.triggers == null)
                    trigger.triggers = new List<EventTrigger.Entry>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entry.callback.AddListener(_ => HandleExitButton());
                trigger.triggers.Add(entry);
            }
            else
            {
                exitButton.onClick.AddListener(HandleExitButton);
            }
        }

        // ─────────────────────────────────────
        //  Route node reached → 到着演出 or 即時オープン
        // ─────────────────────────────────────

        void HandleRouteNodeReached()
        {
            if (vendingMachinePanel == null)
            {
                Debug.LogWarning("[VendingMachineFlowController] vendingMachinePanel が未設定です。");
                return;
            }

            // 到着演出を使う場合は、演出側が中央到着時に OpenVendingMachine() を呼ぶ
            if (deferOpenWhenArrivalSequenceActive)
            {
                if (arrivalSequence == null)
                    arrivalSequence = FindObjectOfType<VendingMachineArrivalSequence>();
                if (arrivalSequence != null)
                {
                    Debug.Log("[VendingMachineFlowController] 到着演出に Open を委任 (中央到達時に OpenVendingMachine が呼ばれる)");
                    return;
                }
                Debug.LogWarning("[VendingMachineFlowController] VendingMachineArrivalSequence が見つかりません。即時 Open します。");
            }

            OpenVendingMachine();
        }

        public void OpenVendingMachine()
        {
            if (_vendingOpen) return;
            _vendingOpen = true;
            _purchaseInFlight = false;

            // 全体一時停止 (敵 / 回転 / Wave / タイマー など全部)。
            // ※ Time.timeScale = 0 になるので UI 系コルーチンは WaitForSecondsRealtime を使うこと。
            if (pauseWorldWhileOpen)
            {
                GamePauseService.Pause(pauseHandle);
                _pauseActive = true;
            }

            // カード自動抽選
            if (autoFillCardsOnOpen && cardSelector != null)
            {
                var pool = ResolveVendingPool();
                cardSelector.FillCardsFromPool(pool);
            }
            else if (cardSelector != null)
            {
                cardSelector.DeselectAll();
            }

            vendingMachinePanel.SetActive(true);
            SetWalletDisplayVisible(true);
            onVendingOpened?.Invoke();
        }

        // ─────────────────────────────────────
        //  Purchase
        // ─────────────────────────────────────

        void HandlePurchaseAffordable(ShopCard card)
        {
            if (!_vendingOpen || _purchaseInFlight) return;
            if (card == null) return;
            _purchaseInFlight = true;

            if (deductMoneyOnPurchase)
            {
                var wallet = PlayerWallet.Instance;
                if (wallet != null)
                    wallet.TrySpend(card.cost);
            }

            // アイテム効果を実反映 (ショップと同じ PlayerEffectState を共有)
            if (applyEffectsOnPurchase && card.shopItemData != null)
            {
                var pes = playerEffectState != null ? playerEffectState : PlayerEffectState.Instance;
                if (pes == null) pes = FindObjectOfType<PlayerEffectState>();
                if (pes != null)
                {
                    ShopItemEffectTable et = cardSelector != null ? cardSelector.effectTable : null;
                    pes.ApplyPurchase(card.shopItemData, et);
                }
                else
                {
                    Debug.LogWarning("[VendingMachineFlowController] PlayerEffectState が見つからないためアイテム効果を適用できません。");
                }
            }

            onPurchaseCompleted?.Invoke(card);

            StartCoroutine(CloseAfterDelay(closeDelayAfterPurchase));
        }

        void HandlePurchaseInsufficient(ShopCard card)
        {
            // 「お金が足りません」演出はあとで繋ぐ。ここは何もせず Canvas を開いたまま。
        }

        void HandleExitButton()
        {
            if (!_vendingOpen || _purchaseInFlight) return;

            Debug.Log("[VendingMachineFlowController] 購入せずに自販機を退出");
            _purchaseInFlight = true;

            StartCoroutine(CloseExitAfterDelay(returnDelayAfterExit));
        }

        IEnumerator CloseAfterDelay(float delay)
        {
            // timeScale=0 のときも経過する Realtime で待つ
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            CloseVendingMachine();
        }

        IEnumerator CloseExitAfterDelay(float delay)
        {
            // スキップ時は Canvas を表示したまま指定秒数待ち、その後に任意アニメーション → 非表示 + 戻り演出へ入る。
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            PlayExitAnimation();

            // Animator のステート完了を優先する設定なら、normalizedTime が 1 になるまで待つ。
            bool waited = false;
            if (waitForExitAnimationToFinish
                && exitAnimator != null
                && exitAnimator.runtimeAnimatorController != null
                && !string.IsNullOrEmpty(exitAnimationStateName))
            {
                yield return WaitForAnimatorStateFinish(
                    exitAnimator,
                    exitAnimationStateName,
                    exitAnimationLayer,
                    exitAnimationMaxWaitSeconds);
                waited = true;
            }

            if (!waited && exitAnimationDurationBeforeHide > 0f)
                yield return new WaitForSecondsRealtime(exitAnimationDurationBeforeHide);

            CloseVendingMachine();
        }

        // Animator の特定ステートが終わる (normalizedTime >= 1) まで Realtime で待つ。
        // Time.timeScale = 0 中でも待てる。万一遷移が起きないときの保険として maxWait で打ち切る。
        IEnumerator WaitForAnimatorStateFinish(Animator animator, string stateName, int layer, float maxWait)
        {
            float started = Time.realtimeSinceStartup;

            // 1F だけ待ってステートが切り替わるのを許容する
            yield return null;

            int targetHash = Animator.StringToHash(stateName);

            while (true)
            {
                if (animator == null || !animator.isActiveAndEnabled)
                    yield break;

                var info = animator.GetCurrentAnimatorStateInfo(layer);

                // 目的ステートに入っている間は normalizedTime が 1.0 を超えたら終了
                bool isTarget = info.shortNameHash == targetHash || info.fullPathHash == targetHash;
                if (isTarget && !animator.IsInTransition(layer) && info.normalizedTime >= 1f)
                    yield break;

                // 目的ステートに入る前で時間切れになるケースに備え、保険タイマーで強制終了
                if (Time.realtimeSinceStartup - started >= maxWait)
                {
                    Debug.LogWarning($"[VendingMachineFlowController] WaitForAnimatorStateFinish: '{stateName}' の完了待ちが {maxWait:F2}s を超えたので打ち切ります。");
                    yield break;
                }

                yield return null;
            }
        }

        void PlayExitAnimation()
        {
            if (exitAnimator != null)
            {
                if (!exitAnimator.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[VendingMachineFlowController] exitAnimator '{exitAnimator.name}' は非アクティブです。アニメは再生されません。");
                }
                if (exitAnimator.runtimeAnimatorController == null)
                {
                    Debug.LogWarning($"[VendingMachineFlowController] exitAnimator '{exitAnimator.name}' に AnimatorController が割り当てられていません。");
                }

                // Time.timeScale=0 でもアニメを進めるため UnscaledTime に切り替える。
                if (forceUnscaledTimeOnExitAnimator)
                {
                    if (!_exitAnimatorUpdateModeSaved)
                    {
                        _exitAnimatorOriginalUpdateMode = exitAnimator.updateMode;
                        _exitAnimatorUpdateModeSaved = true;
                    }
                    exitAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    if (!exitAnimator.enabled) exitAnimator.enabled = true;
                    exitAnimator.speed = 1f;
                }

                if (!string.IsNullOrEmpty(exitAnimationStateName))
                {
                    Debug.Log($"[VendingMachineFlowController] PlayExitAnimation: Play '{exitAnimationStateName}' (layer={exitAnimationLayer}, t={exitAnimationStartNormalizedTime}, updateMode={exitAnimator.updateMode})");
                    exitAnimator.Play(exitAnimationStateName, exitAnimationLayer, exitAnimationStartNormalizedTime);
                    exitAnimator.Update(0f);
                }
                else if (!string.IsNullOrEmpty(exitAnimationTrigger))
                {
                    Debug.Log($"[VendingMachineFlowController] PlayExitAnimation: SetTrigger '{exitAnimationTrigger}' (updateMode={exitAnimator.updateMode})");
                    exitAnimator.ResetTrigger(exitAnimationTrigger);
                    exitAnimator.SetTrigger(exitAnimationTrigger);
                    exitAnimator.Update(0f);
                }
                else
                {
                    Debug.Log("[VendingMachineFlowController] PlayExitAnimation: ステート名 / Trigger 名どちらも未設定のため Animator は呼び出しません。");
                }
            }
            else
            {
                Debug.Log("[VendingMachineFlowController] PlayExitAnimation: exitAnimator 未設定。onExitAnimationStarted のみ発火します。");
            }

            onExitAnimationStarted?.Invoke();
        }

        void RestoreExitAnimatorUpdateMode()
        {
            if (exitAnimator == null) return;
            if (!_exitAnimatorUpdateModeSaved) return;

            exitAnimator.updateMode = _exitAnimatorOriginalUpdateMode;
            _exitAnimatorUpdateModeSaved = false;
        }

        ShopItemPool ResolveVendingPool()
        {
            int currentStage = ResolveCurrentStage();
            if (_currentStageForCount != currentStage)
            {
                _currentStageForCount = currentStage;
                _vendingOpenCountInStage = 0;
            }

            _vendingOpenCountInStage++;
            int occurrence = _vendingOpenCountInStage;

            ShopItemPool stageFallback = null;
            for (int i = 0; i < vendingStagePools.Count; i++)
            {
                var entry = vendingStagePools[i];
                if (entry == null || entry.stage != currentStage || entry.pool == null)
                    continue;

                if (entry.occurrence == occurrence)
                    return entry.pool;

                if (entry.occurrence == 0)
                    stageFallback = entry.pool;
            }

            if (stageFallback != null) return stageFallback;
            if (vendingPool != null) return vendingPool;
            return cardSelector != null ? cardSelector.defaultPool : null;
        }

        int ResolveCurrentStage()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.stageManager != null)
                return gm.stageManager.currentStageNumber;
            return _currentStageForCount > 0 ? _currentStageForCount : 1;
        }

        public void CloseVendingMachine()
        {
            if (!_vendingOpen) return;
            _vendingOpen = false;
            _purchaseInFlight = false;

            if (cardSelector != null)
                cardSelector.DeselectAll();

            if (vendingMachinePanel != null)
                vendingMachinePanel.SetActive(false);
            SetWalletDisplayVisible(false);

            RestoreExitAnimatorUpdateMode();

            onVendingClosed?.Invoke();

            RestoreAndResumeWorld();
        }

        void RestoreAndResumeWorld()
        {
            // 戻り演出中に地面の流れも見せたいので、先に Pause を解除する。
            // ArrivalSequence 側で scrollSpeed を 0 → saved へ補間し、プレイヤー/敵の戻りと同期させる。
            if (arrivalSequence != null)
            {
                ResumeWorldAfterClose();
                arrivalSequence.RestoreAfterVendingMachine();
                return;
            }

            ResumeWorldAfterClose();
        }

        void ResumeWorldAfterClose()
        {
            if (_pauseActive)
            {
                GamePauseService.Resume(pauseHandle);
                _pauseActive = false;
            }
        }

        void SetWalletDisplayVisible(bool visible)
        {
            if (walletDisplayRoot == null) return;
            walletDisplayRoot.SetActive(visible);

            if (visible)
            {
                var walletDisplay = walletDisplayRoot.GetComponentInChildren<WalletTMPDisplay>(includeInactive: true);
                if (walletDisplay != null)
                    walletDisplay.Refresh();
            }
        }
    }
}
