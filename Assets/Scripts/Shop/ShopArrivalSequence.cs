using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 「道中ルートマスが Shop に切り替わった瞬間」の到着演出を統括するコンポーネント。
    ///
    /// 1. 敵が Wave スポーン停止 & 後方へスライドアウト
    /// 2. ショップ画像 (Prefab 優先 / Sprite フォールバック) をアーチ遠方に生成
    /// 3. ショップ画像をアーチに沿って中央 (プレイヤー位置) へスライド
    /// 4. 同時にプレイヤー UI も画面中央へ「アーチに乗って流れる」アニメで合流
    /// 5. ショップ画像が中央到達 → ArchRoadSystem.scrollSpeed = 0 (急停止)
    /// 6. 少し待ってから ShopFlowController.OpenShop() を呼び Canvas に遷移
    /// 7. 購入完了 → ShopFlowController が ProceedToNextStage() を呼ぶ
    /// 8. OnStageChanged で本コンポーネントが後片付け (ショップ画像破棄、scrollSpeed 復帰、プレイヤー UI 復帰)
    ///
    /// ■ セットアップ
    ///   1. 任意の永続 GameObject にアタッチ (GameManager 周りに同居でも可)
    ///   2. ArchRoadSystem / EnemySystem / ShopFlowController をアサイン
    ///   3. ショップ Prefab か Sprite のどちらかをアサイン
    ///   4. プレイヤー UI (CharacterAnimator のある画像) の RectTransform をアサイン
    ///   5. playerCenterAnchored に「アーチに合流したときのプレイヤー UI 位置」を入力
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ShopArrivalSequence : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("回転している道路。空のとき FindObjectOfType で自動取得")]
        public ArchRoadSystem archRoadSystem;

        [Tooltip("敵管理。空のとき FindObjectOfType で自動取得")]
        public EnemySystem enemySystem;

        [Tooltip("ショップ Canvas のフロー管理。空のとき FindObjectOfType で自動取得")]
        public ShopFlowController shopFlow;

        [Tooltip("背景パララックス。空のとき FindObjectOfType で自動取得 (シーンに無くても可)")]
        public ParallaxBackground parallaxBackground;

        [Header("Shop Visual (Prefab 優先 / Sprite フォールバック)")]
        [Tooltip("ショップを表現するプレハブ。子に SpriteRenderer / Spinner2D 等好きに組める。")]
        public GameObject shopPrefab;

        [Tooltip("プレハブが無い場合に使うショップ Sprite (簡易表示)")]
        public Sprite shopSprite;

        [Tooltip("Sprite フォールバック時のスケール")]
        public float shopSpriteScale = 1f;

        [Tooltip("ON: ArchRoadSystem の SortingLayer / roadSortingOrder を基準に、ショップを道路の 1 つ後ろへ自動配置する。\n" +
                 "通常は ON 推奨。ParallaxBackground(-10) < Shop(-1) < ArchRoadSystem Road(0) のような並びになる。")]
        public bool autoPlaceShopBehindRoad = true;

        [Tooltip("autoPlaceShopBehindRoad=OFF のときに使うショップビジュアル用 Sorting Layer 名")]
        public string shopSortingLayer = "UI";

        [Tooltip("autoPlaceShopBehindRoad=OFF のときに使うショップビジュアル用 Sorting Order")]
        public int shopSortingOrder = -1;

        [Header("Shop Fade In (ポップイン防止)")]
        [Tooltip("ON: スポーン時 alpha=0 → 接近に合わせて 1 に上げる (急に現れるのを防ぐ)")]
        public bool enableShopFadeIn = true;

        [Tooltip("フェードイン所要秒数 (シーケンス開始からこの秒数で alpha 1 まで上がる)")]
        public float shopFadeInDuration = 1.2f;

        [Header("Arch 位置パラメータ")]
        [Tooltip("ショップ画像の到着 distanceAngle (度)。0 でプレイヤー手前 (画面中央)。\n" +
                 "ここを基準にスポーン位置が逆算される (= 減速終了時にぴったりここに来る)。")]
        public float shopArriveDistance = 0f;

        [Tooltip("ON: shopApproachDuration からスポーン距離を自動で逆算する。\n" +
                 "= 等速アプローチ + 減速で必ず shopArriveDistance に到着するよう、startDist を計算で決定する。\n" +
                 "(スナップ補正によるテレポートが発生しない、推奨)\n" +
                 "OFF: 旧来通り shopStartDistance を直接使う (互換用)")]
        public bool autoComputeStartDistance = true;

        [Tooltip("等速アプローチの所要秒数 (ショップが見え始めて加速/減速開始までの時間)。\n" +
                 "0 にすると、シーケンス開始 (敵退場と同時) に即加速フェーズへ突入する。\n" +
                 "autoComputeStartDistance=ON のとき、この時間分だけ余分にスポーン位置を後ろへ取る。\n" +
                 "演出全体の秒数 ≒ shopApproachDuration + boostDuration + shopArriveDuration となる。")]
        public float shopApproachDuration = 0f;

        [Tooltip("autoComputeStartDistance=OFF のときに使う、ショップ画像の初期 distanceAngle (度)。\n" +
                 "大きいほど遠方からスタート。")]
        public float shopStartDistance = 110f;

        [Tooltip("ON: 距離角の符号を反転 (= 画面右から手前に流れて来る)\n" +
                 "OFF: 画面左から (敵と同じ方向 / 道路スクロールと一致するので推奨)\n" +
                 "※ 道路は scrollSpeed>0 のとき distanceAngle を + → 0 方向へ動かすので、\n" +
                 "  OFF にしておくとショップが「世界に置かれた静止物」として自然に流れて来る。\n" +
                 "  ON にすると道路の流れと逆向きに動くため違和感が出る。")]
        public bool comesFromOppositeSide = false;

        [Tooltip("通常は OFF。ON にすると演出中だけ道路と背景の scrollSpeed の符号を反転する。\n" +
                 "(プレイ中と違う方向に道路を流したい特殊演出用)")]
        public bool reverseRoadDirectionDuringSequence = false;

        [Tooltip("ショップ画像の地面からの浮き高さ")]
        public float shopHoverHeight = 0f;

        [Header("Timing")]
        [Tooltip("敵が退場するスピード (degree/sec)。distanceAngle 増加方向に押し流す")]
        public float enemyExitSpeed = 30f;

        [Tooltip("敵退場フェーズの所要秒数")]
        public float enemyExitDuration = 1.0f;

        [Tooltip("【減速フェーズの所要秒数】道路スクロール速度が saved → 0 に落ちきるまでの時間。\n" +
                 "ショップは scrollSpeed と同じ速度 (scaleFactor=1) で接近 → 残り距離が saved*duration/2 まで縮んだら減速開始。\n" +
                 "したがって演出全体の所要秒数 = (shopStartDistance / savedScrollSpeed - duration/2) + duration となる。")]
        public float shopArriveDuration = 1.6f;

        [Tooltip("ショップ到着後、Canvas を開くまでの停止演出の秒数")]
        public float shopArrivePauseSeconds = 0.4f;

        [Tooltip("ON: ショップ移動と並行で道路スクロール速度を 0 まで落とす\n" +
                 "(= ショップが中心に来た瞬間にちょうどプレイヤーが止まる)\n" +
                 "OFF: 従来通りショップ到着後に急停止")]
        public bool decelerateRoadWithShop = true;

        [Header("Speed Boost (踏ん張る演出)")]
        [Tooltip("ON: マス到着時にいったん scrollSpeed を boostPeakMultiplier 倍まで上げてから\n" +
                 "減速して 0 にする。「ぐっと加速してから停まる」感じの演出が付く。\n" +
                 "ショップは加速・減速とも同期して動くので、最終的にぴったり 0° に到着する。\n" +
                 "※ OFF だと boostPeakMultiplier / boostDuration を変えても何も起きません。")]
        public bool enableSpeedBoost = true;

        [Tooltip("加速のピーク倍率。V_peak = V0 × multiplier (1.0 = 加速なし)")]
        public float boostPeakMultiplier = 1.6f;

        [Tooltip("V0 → V_peak まで加速する所要秒数")]
        public float boostDuration = 0.4f;

        /// <summary>到着時のイージングモード。物理的に揃えた MatchedLinearDecel が推奨。</summary>
        public enum ArrivalEaseMode
        {
            /// <summary>道路=線形減速 / ショップ=二次イーズアウト。物理的に等減速運動と一致するため最も自然。</summary>
            MatchedLinearDecel,
            /// <summary>道路・ショップとも smoothstep。両端で 0 速度なので「中心でしばらく止まって見える」感じになる。</summary>
            SmoothStepBoth,
            /// <summary>道路・ショップとも線形。動きが直線的になる。</summary>
            LinearBoth,
        }

        [Tooltip("到着時のイージング種類。\n" +
                 "・MatchedLinearDecel: 道路=線形減速 + ショップ=二次イーズアウト。完全同期 (推奨)\n" +
                 "・SmoothStepBoth: 両方 smoothstep (中央でショップが最速になり、減速とずれる)\n" +
                 "・LinearBoth: 両方とも線形")]
        public ArrivalEaseMode arrivalEaseMode = ArrivalEaseMode.MatchedLinearDecel;

        [Header("Player Visual")]
        [Tooltip("プレイヤー UI (CharacterAnimator のあるオブジェクト) の RectTransform")]
        public RectTransform playerVisualRect;

        [Tooltip("ショップ到着時にプレイヤーが流れて来る anchoredPosition (= 画面中央付近)。\n" +
                 "preserveOriginalY が ON のときは Y 成分は無視されます。")]
        public Vector2 playerCenterAnchored = Vector2.zero;

        [Tooltip("ON: 移動中も元の anchoredPosition.y を維持し、X だけアニメする (=道路に乗ったまま横にスライドする見え方)\n" +
                 "OFF: playerCenterAnchored の Y もそのまま使う")]
        public bool preserveOriginalY = true;

        [Tooltip("プレイヤー UI の移動所要秒数。空(<=0) で shopArriveDuration と同期")]
        public float playerMoveDuration = -1f;

        [Tooltip("ON: 演出中はプレイヤーの道路揺れ (CharacterAnimator.enableRoadBump) を一時 OFF にする。\n" +
                 "OFF だと揺れと移動が同じ anchoredPosition を奪い合ってガタガタになる")]
        public bool suppressPlayerRoadBumpDuringSequence = true;

        [Header("Behavior")]
        [Tooltip("ステージ進行中 (currentState == Playing) でなければ演出を始めない")]
        public bool requirePlayingState = true;

        [Tooltip("OnStageChanged 受信で復帰処理を行う")]
        public bool autoRestoreOnStageChanged = true;

        [Header("Debug")]
        [Tooltip("ON: フロー各段階の Debug.Log を出力する")]
        public bool verboseLog = true;

        [Tooltip("ON: 再生中に指定キーで手動トリガー (ルートノード設定無しでもテスト可能)")]
        public bool enableDebugKey = true;

        [Tooltip("手動トリガー用キー (enableDebugKey が ON のとき有効)")]
        public KeyCode debugTriggerKey = KeyCode.S;

        [Tooltip("演出開始時に archRoadSystem.scrollSpeed が 0 (または非常に小さい) だった場合の代替値。\n" +
                 "S キーで連打テストするときなど、前回の演出で 0 に落ちたままになっているケースを救う。")]
        public float debugFallbackScrollSpeed = 15f;

        // ─── runtime ───
        bool _sequenceRunning;
        bool _shopArrivedAtCenter;

        /// <summary>到着演出が現在走っているか (ShopFlowController が OpenShop の二重起動を防ぐために参照)</summary>
        public bool IsSequenceRunning => _sequenceRunning;
        GameObject _shopVisualInstance;
        Coroutine _sequenceCoroutine;
        Coroutine _playerMoveCoroutine;
        Coroutine _decelerateCoroutine;
        float _savedScrollSpeed;             // 復帰用 (演出開始前の値)
        float _workingScrollSpeed;           // 演出中の道路 scrollSpeed (符号反転を含む)
        float _savedBackgroundScrollSpeed;
        float _workingBackgroundScrollSpeed; // 演出中の背景 scrollSpeed (符号反転を含む)
        bool  _backgroundScrollSaved;
        SpriteRenderer[] _shopRenderers;     // フェードイン対象 (Shop visual 配下の全 SR)
        Coroutine _fadeInCoroutine;
        Vector2 _savedPlayerAnchored;
        bool _playerPositionSaved;
        CharacterAnimator _playerCharacterAnimator;
        bool _playerBumpWasSuppressed;

        // ─────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────

        void Awake()
        {
            if (archRoadSystem      == null) archRoadSystem      = FindObjectOfType<ArchRoadSystem>();
            if (enemySystem         == null) enemySystem         = FindObjectOfType<EnemySystem>();
            if (shopFlow            == null) shopFlow            = FindObjectOfType<ShopFlowController>();
            if (parallaxBackground  == null) parallaxBackground  = FindObjectOfType<ParallaxBackground>();
        }

        void OnEnable()
        {
            GameEvents.OnShopRouteNodeReached += HandleShopRouteNodeReached;
            GameEvents.OnStageChanged         += HandleStageChanged;
        }

        void OnDisable()
        {
            GameEvents.OnShopRouteNodeReached -= HandleShopRouteNodeReached;
            GameEvents.OnStageChanged         -= HandleStageChanged;
        }

        // ─────────────────────────────────────
        //  Entry
        // ─────────────────────────────────────

        void HandleShopRouteNodeReached()
        {
            if (verboseLog) Debug.Log("[ShopArrivalSequence] OnShopRouteNodeReached 受信");

            if (_sequenceRunning)
            {
                if (verboseLog) Debug.Log("[ShopArrivalSequence] 既に演出中のためスキップ");
                return;
            }
            if (requirePlayingState
                && GameManager.Instance != null
                && GameManager.Instance.currentState != GameState.Playing)
            {
                if (verboseLog)
                    Debug.LogWarning($"[ShopArrivalSequence] requirePlayingState で中断 (現在の state = {GameManager.Instance.currentState})");
                return;
            }

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(RunArrivalSequence());
        }

        void HandleStageChanged(int _)
        {
            if (autoRestoreOnStageChanged)
                RestoreAfterShop();
        }

        void Update()
        {
            if (!enableDebugKey) return;
            if (Input.GetKeyDown(debugTriggerKey))
                ForceTriggerForDebug();
        }

        // ─────────────────────────────────────
        //  Public debug API
        // ─────────────────────────────────────

        /// <summary>
        /// インスペクタ右クリック / S キー / 外部スクリプトから演出を強制再生してテストするためのフック。
        /// </summary>
        [ContextMenu("Test: Trigger Shop Arrival")]
        public void ForceTriggerForDebug()
        {
            Debug.Log($"<color=#00ffff>[ShopArrivalSequence] ★★ ForceTriggerForDebug() 呼び出し ★★</color>\n" +
                      $"  archRoadSystem={(archRoadSystem != null ? archRoadSystem.name : "<null>")}\n" +
                      $"  enemySystem={(enemySystem != null ? enemySystem.name : "<null>")}\n" +
                      $"  shopFlow={(shopFlow != null ? shopFlow.name : "<null>")}\n" +
                      $"  parallaxBackground={(parallaxBackground != null ? parallaxBackground.name : "<null>")}\n" +
                      $"  shopPrefab={(shopPrefab != null ? shopPrefab.name : "<null>")}\n" +
                      $"  shopSprite={(shopSprite != null ? shopSprite.name : "<null>")}\n" +
                      $"  playerVisualRect={(playerVisualRect != null ? playerVisualRect.name : "<null>")}\n" +
                      $"  _sequenceRunning={_sequenceRunning}\n" +
                      $"  GameState={(GameManager.Instance != null ? GameManager.Instance.currentState.ToString() : "<no GameManager>")}");

            if (enemySystem != null)
                enemySystem.PauseSurvivalForShop();

            // 既に走っている演出をクリーンに停止
            if (_sequenceCoroutine     != null) { StopCoroutine(_sequenceCoroutine);     _sequenceCoroutine     = null; }
            if (_playerMoveCoroutine   != null) { StopCoroutine(_playerMoveCoroutine);   _playerMoveCoroutine   = null; }
            if (_decelerateCoroutine   != null) { StopCoroutine(_decelerateCoroutine);   _decelerateCoroutine   = null; }
            _sequenceRunning = false;

            _sequenceCoroutine = StartCoroutine(RunArrivalSequence());
        }

        // ─────────────────────────────────────
        //  Sequence
        // ─────────────────────────────────────

        IEnumerator RunArrivalSequence()
        {
            _sequenceRunning = true;
            _shopArrivedAtCenter = false;

            if (verboseLog) Debug.Log("[ShopArrivalSequence] === Phase 1: 敵退場 & 道路速度保存 ===");

            // 参照の健全性チェック (verboseLog ON のときだけログ)
            if (verboseLog)
            {
                if (archRoadSystem == null) Debug.LogError("[ShopArrivalSequence] archRoadSystem が null");
                if (enemySystem    == null) Debug.LogError("[ShopArrivalSequence] enemySystem が null");
                if (shopFlow       == null) Debug.LogError("[ShopArrivalSequence] shopFlow が null");
                if (shopPrefab == null && shopSprite == null)
                    Debug.LogError("[ShopArrivalSequence] shopPrefab / shopSprite どちらも未設定");
            }

            // 道路スクロール速度を保存 (購入後に復帰させる) & 演出中の作業値を決定
            if (archRoadSystem != null)
            {
                _savedScrollSpeed = Mathf.Abs(archRoadSystem.scrollSpeed) > 0.0001f
                    ? archRoadSystem.scrollSpeed
                    : 0f;

                // 連打テストなどで前回の演出後に scrollSpeed=0 のまま残っているケースを救う
                if (Mathf.Abs(_savedScrollSpeed) < 0.0001f && debugFallbackScrollSpeed > 0.0001f)
                {
                    Debug.LogWarning($"[ShopArrivalSequence] road scrollSpeed が 0 だったので debugFallbackScrollSpeed={debugFallbackScrollSpeed} で復帰させます (前回の演出が中断していた可能性)");
                    _savedScrollSpeed = debugFallbackScrollSpeed;
                }

                // 符号反転 (= 演出中だけ道路を逆向きに流す)
                _workingScrollSpeed = reverseRoadDirectionDuringSequence ? -_savedScrollSpeed : _savedScrollSpeed;
                archRoadSystem.scrollSpeed = _workingScrollSpeed;

                if (verboseLog)
                    Debug.Log($"[ShopArrivalSequence] road scroll speed saved={_savedScrollSpeed}, working={_workingScrollSpeed} (reverse={reverseRoadDirectionDuringSequence})");
            }

            // 背景パララックスも同じ規則で
            if (parallaxBackground != null)
            {
                _savedBackgroundScrollSpeed = parallaxBackground.baseScrollSpeed;
                _backgroundScrollSaved = true;

                if (Mathf.Abs(_savedBackgroundScrollSpeed) < 0.0001f && debugFallbackScrollSpeed > 0.0001f)
                {
                    Debug.LogWarning($"[ShopArrivalSequence] background scrollSpeed も 0 → debugFallbackScrollSpeed で復帰");
                    _savedBackgroundScrollSpeed = debugFallbackScrollSpeed;
                }

                _workingBackgroundScrollSpeed = reverseRoadDirectionDuringSequence ? -_savedBackgroundScrollSpeed : _savedBackgroundScrollSpeed;
                parallaxBackground.baseScrollSpeed = _workingBackgroundScrollSpeed;

                if (verboseLog)
                    Debug.Log($"[ShopArrivalSequence] background scroll speed saved={_savedBackgroundScrollSpeed}, working={_workingBackgroundScrollSpeed}");
            }

            // プレイヤー UI の元位置を保存 & 道路揺れを一時 OFF
            //
            // ★ 注意: ここで SetRoadBumpEnabled(false, resetToBase=true) にすると
            //   揺れ中の Y 位置が基準位置に「ポン」と戻り、急停止に見える。
            //   resetToBase=false で「今いる位置で揺れを止める」+ 続けて MoveRectTransform を
            //   その位置から開始することで、視覚的に連続した滑らかな移動になる。
            if (playerVisualRect != null)
            {
                // 揺れを止める「前」の現在位置をベースに保存しても良いが、CharacterAnimator が
                // 内部で _basePosition を保持しているので、そちらを真値として使う。
                _savedPlayerAnchored = playerVisualRect.anchoredPosition;
                _playerPositionSaved = true;

                if (suppressPlayerRoadBumpDuringSequence)
                {
                    if (_playerCharacterAnimator == null)
                        _playerCharacterAnimator = playerVisualRect.GetComponentInParent<CharacterAnimator>();
                    if (_playerCharacterAnimator == null)
                        _playerCharacterAnimator = playerVisualRect.GetComponentInChildren<CharacterAnimator>(includeInactive: true);

                    if (_playerCharacterAnimator != null)
                    {
                        if (verboseLog) Debug.Log("[ShopArrivalSequence] CharacterAnimator の道路揺れを一時 OFF (位置はキープ)");
                        // 位置をスナップさせない (= 急停止の原因)。今いる Y のまま揺れだけ停止。
                        _playerCharacterAnimator.SetRoadBumpEnabled(false, resetToBase: false);
                        _playerBumpWasSuppressed = true;
                    }
                    else if (verboseLog)
                    {
                        Debug.LogWarning("[ShopArrivalSequence] CharacterAnimator が見つからないため揺れ抑制をスキップ");
                    }
                }
            }

            // 敵を後方退場させる (並行実行: 待たない)
            if (enemySystem != null)
                enemySystem.BeginEnemyExitToShop(enemyExitSpeed, enemyExitDuration, clearOnFinish: true);

            if (verboseLog) Debug.Log("[ShopArrivalSequence] === Phase 2: ショップ生成 ===");
            _shopVisualInstance = SpawnShopVisual();
            if (verboseLog && _shopVisualInstance != null)
                Debug.Log($"[ShopArrivalSequence] Shop instance spawned: {_shopVisualInstance.name}");

            // フェードイン (ポップイン防止): 生成直後は alpha=0 に落として、コルーチンで 1 まで上げる
            if (_shopVisualInstance != null && enableShopFadeIn && shopFadeInDuration > 0f)
            {
                _shopRenderers = _shopVisualInstance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
                if (_shopRenderers != null && _shopRenderers.Length > 0)
                {
                    SetShopAlpha(0f);
                    if (_fadeInCoroutine != null) StopCoroutine(_fadeInCoroutine);
                    _fadeInCoroutine = StartCoroutine(FadeShopInCo(shopFadeInDuration));
                    if (verboseLog)
                        Debug.Log($"[ShopArrivalSequence] Shop fade-in 開始 (duration={shopFadeInDuration:F2}s, renderers={_shopRenderers.Length})");
                }
            }

            // プレイヤー UI の移動を並行で開始
            // - 未指定 (<=0) のときは「ショップが見え始めてから止まるまで」の演出全体に合わせる
            //   (= approach + boost + decel)。
            if (playerVisualRect != null)
            {
                float autoTotal = shopApproachDuration
                                + (enableSpeedBoost && boostPeakMultiplier > 1.0001f ? boostDuration : 0f)
                                + shopArriveDuration;
                float dur = playerMoveDuration > 0f
                    ? playerMoveDuration
                    : Mathf.Max(0.0001f, autoTotal);
                Vector2 target = playerCenterAnchored;
                if (preserveOriginalY)
                    target.y = _savedPlayerAnchored.y;
                _playerMoveCoroutine = StartCoroutine(MoveRectTransform(
                    playerVisualRect, _savedPlayerAnchored, target, dur));
            }

            float sign = comesFromOppositeSide ? -1f : 1f;
            float endD = shopArriveDistance * sign;

            // === スポーン距離の決定 ===
            //  - autoComputeStartDistance=ON: endDist から逆算する (= 物理的に止まる位置から spawn を決める)
            //      shop は 3 フェーズで移動する:
            //        approach: V0 × approachDur                            (等速)
            //        boost   : (V0 + Vpeak) × boostDur / 2                 (V0 → Vpeak 線形加速)
            //        decel   : Vpeak × decelDur / 2                        (Vpeak → 0  線形減速)
            //      合計距離を endDist から逆向きに引いた位置がスポーン地点。
            //      boost OFF のとき Vpeak=V0, boostDur=0 で boostDist=0 になり、旧来動作と同等。
            //  - OFF: 旧来通り shopStartDistance を使う (互換用)
            float V0 = Mathf.Abs(_workingScrollSpeed);
            float approachDuration = Mathf.Max(0f, shopApproachDuration);
            float decelDuration    = Mathf.Max(0.0001f, shopArriveDuration);

            bool boost = enableSpeedBoost && boostPeakMultiplier > 1.0001f && boostDuration > 0f;
            float Vpeak    = boost ? V0 * boostPeakMultiplier : V0;
            float boostDur = boost ? boostDuration : 0f;

            float approachDist = V0 * approachDuration;
            float boostDist    = (V0 + Vpeak) * 0.5f * boostDur;
            float decelDist    = Vpeak * decelDuration * 0.5f;
            float totalDist    = approachDist + boostDist + decelDist;

            // shopDirection: endDist へ寄せる向き (= 道路の流れと同じ向きでなければならない)
            //   道路は scrollSpeed>0 のとき distanceAngle を + → 0 (decrease) へ動かすので、
            //   普通は sign=+1, endD=0 → shopDirection=-1 (= 大きい+から 0 へ近づく)
            float shopDirection = (sign >= 0f) ? -1f : +1f;

            float startD;
            if (autoComputeStartDistance)
            {
                startD = endD - shopDirection * totalDist;
                if (verboseLog)
                    Debug.Log($"[ShopArrivalSequence] === Phase 3 (auto): start={startD:F1}°, end={endD:F1}°, " +
                              $"V0={V0:F1}, Vpeak={Vpeak:F1}, " +
                              $"approach={approachDuration:F2}s ({approachDist:F1}°), " +
                              $"boost={boostDur:F2}s ({boostDist:F1}°), " +
                              $"decel={decelDuration:F2}s ({decelDist:F1}°) ===");
            }
            else
            {
                startD = shopStartDistance * sign;
                if (verboseLog)
                    Debug.Log($"[ShopArrivalSequence] === Phase 3 (manual): start={startD:F1}°, end={endD:F1}°, " +
                              $"V0={V0:F1}, Vpeak={Vpeak:F1}, decel={decelDuration:F2}s ===");
            }

            // 解析計算でショップ + 道路 + 背景を同一プロファイルで駆動する。
            // (DecelerateScroll は内部統合済みなので別 coroutine 起動は不要)
            yield return MoveShopAnalytic(_shopVisualInstance, startD, endD, shopDirection,
                                          V0, Vpeak, approachDuration, boostDur, decelDuration);

            _shopArrivedAtCenter = true;

            // 安全のため到着時に厳密に 0 で固定 (減速コルーチンの誤差吸収)
            if (archRoadSystem != null)
                archRoadSystem.scrollSpeed = 0f;
            if (parallaxBackground != null)
                parallaxBackground.baseScrollSpeed = 0f;

            if (verboseLog) Debug.Log("[ShopArrivalSequence] === Phase 4: 道路停止 & Canvas オープン待機 ===");

            // 一呼吸おいて Canvas へ
            if (shopArrivePauseSeconds > 0f)
                yield return new WaitForSeconds(shopArrivePauseSeconds);

            if (shopFlow != null)
            {
                if (verboseLog) Debug.Log("[ShopArrivalSequence] ShopFlowController.OpenShop() 呼び出し");
                shopFlow.OpenShop();
            }
            else if (verboseLog)
            {
                Debug.LogWarning("[ShopArrivalSequence] shopFlow が null のため Canvas を開けません");
            }

            // 以降は ShopFlowController.onShopClosed → ProceedToNextStage → OnStageChanged で復帰
            _sequenceCoroutine = null;
        }

        // ─────────────────────────────────────
        //  Shop visual
        // ─────────────────────────────────────

        GameObject SpawnShopVisual()
        {
            GameObject go;
            SpriteRenderer createdSR = null;

            if (shopPrefab != null)
            {
                go = Instantiate(shopPrefab);
                go.name = "ShopVisualInstance";
                ApplyShopSorting(go);
                if (verboseLog) Debug.Log("[ShopArrivalSequence] shopPrefab を Instantiate");
            }
            else
            {
                go = new GameObject("ShopVisualSprite");
                if (shopSprite != null)
                {
                    createdSR = go.AddComponent<SpriteRenderer>();
                    createdSR.sprite = shopSprite;
                    ApplyShopSorting(go);
                    if (verboseLog)
                        Debug.Log($"[ShopArrivalSequence] shopSprite SpriteRenderer 生成: sprite='{shopSprite.name}', layer='{createdSR.sortingLayerName}', order={createdSR.sortingOrder}");
                }
                else if (verboseLog)
                {
                    Debug.LogError("[ShopArrivalSequence] shopSprite が null。Prefab も Sprite も無いので空の GameObject になります。");
                }
                go.transform.localScale = Vector3.one * Mathf.Max(0.01f, shopSpriteScale);
            }

            // ★ 親を付けない (top-level に置く) ことで、ArchRoadSystem のスケール/座標と切り離す。
            //    transform.position をワールド座標で直接書き込めば敵と同じ見え方になる。
            go.transform.SetParent(null, worldPositionStays: true);

            return go;
        }

        void ApplyShopSorting(GameObject root)
        {
            if (root == null) return;

            string layerName = ResolveShopSortingLayer();
            int order = ResolveShopSortingOrder();

            if (!SortingLayerExists(layerName))
            {
                Debug.LogWarning($"[ShopArrivalSequence] Sorting Layer '{layerName}' が未登録です。'Default' にフォールバックします。Project Settings > Tags and Layers で登録してください。");
                layerName = "Default";
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;
                sr.sortingLayerName = layerName;
                sr.sortingOrder = order;
            }

            if (verboseLog)
                Debug.Log($"[ShopArrivalSequence] Shop sorting 適用: layer='{layerName}', order={order}, renderers={renderers.Length}");
        }

        string ResolveShopSortingLayer()
        {
            if (autoPlaceShopBehindRoad && archRoadSystem != null)
                return archRoadSystem.sortingLayer;
            return shopSortingLayer;
        }

        int ResolveShopSortingOrder()
        {
            if (autoPlaceShopBehindRoad && archRoadSystem != null)
                return archRoadSystem.roadSortingOrder - 1;
            return shopSortingOrder;
        }

        bool SortingLayerExists(string layerName)
        {
            if (layerName == "Default") return true;
            foreach (var l in SortingLayer.layers)
                if (l.name == layerName) return true;
            return false;
        }

        /// <summary>
        /// 解析計算ベースでショップを動かす + 道路・背景の scrollSpeed も同一プロファイルで駆動する。
        ///
        /// 速度プロファイル v(t):
        ///   approach (0 .. tA)               : v = V0                                (等速)
        ///   boost    (tA .. tA+tB)           : v = Lerp(V0, Vpeak, τ/tB)             (線形加速)
        ///   decel    (tA+tB .. tA+tB+tC)     : v = Lerp(Vpeak, 0, τ/tC)              (線形減速)
        ///
        /// 各フェーズの距離 (= 面積):
        ///   approachDist = V0 * tA
        ///   boostDist    = (V0 + Vpeak) / 2 * tB
        ///   decelDist    = Vpeak * tC / 2
        ///
        /// 終端 t = tA+tB+tC で必ず s = totalDist = approachDist + boostDist + decelDist
        /// となり、ショップは startDist + shopDir * totalDist = endDist にぴったり到達する。
        ///
        /// 道路 + 背景の scrollSpeed もこの v(t) で更新するので、世界の流れと完全に同期する。
        /// </summary>
        IEnumerator MoveShopAnalytic(GameObject shopGO, float startDist, float endDist,
                                     float shopDirection,
                                     float V0, float Vpeak,
                                     float approachDuration, float boostDuration,
                                     float decelDuration)
        {
            if (shopGO == null)
            {
                if (verboseLog) Debug.LogError("[ShopArrivalSequence] MoveShopAnalytic: shopGO が null");
                yield break;
            }
            if (archRoadSystem == null)
            {
                if (verboseLog) Debug.LogError("[ShopArrivalSequence] MoveShopAnalytic: archRoadSystem が null");
                yield break;
            }

            PlaceOnArch(shopGO.transform, startDist);
            if (verboseLog)
                Debug.Log($"[ShopArrivalSequence] Shop 初期配置: distance={startDist:F1}°, pos={shopGO.transform.position}");

            // 速度・距離 0 のときは即終端
            if (V0 < 0.0001f || (approachDuration <= 0f && boostDuration <= 0f && decelDuration <= 0f))
            {
                PlaceOnArch(shopGO.transform, endDist);
                ApplyScrollSpeed(0f);
                yield break;
            }

            float approachDist = V0 * approachDuration;
            float boostDist    = (V0 + Vpeak) * 0.5f * boostDuration;
            float logTimer = 0f;

            // === Phase A: 等速アプローチ (V0) ===
            // 道路速度は元々 V0 なので明示的に再設定 (他からの変更を上書き)
            ApplyScrollSpeed(V0);
            if (approachDuration > 0f)
            {
                float t = 0f;
                while (t < approachDuration)
                {
                    t += Time.deltaTime;
                    logTimer += Time.deltaTime;
                    float clamped = Mathf.Min(t, approachDuration);
                    float traveled = V0 * clamped;
                    float currentD = startDist + shopDirection * traveled;
                    PlaceOnArch(shopGO.transform, currentD);

                    if (verboseLog && logTimer >= 0.4f)
                    {
                        logTimer = 0f;
                        Debug.Log($"[ShopArrivalSequence] Shop 接近 (V0={V0:F1}): d={currentD:F1}°, t={clamped:F2}/{approachDuration:F2}");
                    }
                    yield return null;
                }
            }

            // === Phase B: 加速 (V0 → Vpeak) ===
            //   v(τ)  = V0 + (Vpeak - V0) * (τ / tB)
            //   s(τ)  = V0*τ + (Vpeak - V0) * τ² / (2 * tB)
            //         = V0*τ + ΔV * frac * τ / 2     (ΔV = Vpeak - V0, frac = τ / tB)
            if (boostDuration > 0f && Vpeak > V0 + 0.0001f)
            {
                if (verboseLog)
                    Debug.Log($"[ShopArrivalSequence] 加速フェーズ開始 (V0={V0:F1} → Vpeak={Vpeak:F1}, dur={boostDuration:F2}s)");
                float boostBaseD = startDist + shopDirection * approachDist;
                float dV = Vpeak - V0;
                float tau = 0f;
                while (tau < boostDuration)
                {
                    tau += Time.deltaTime;
                    logTimer += Time.deltaTime;
                    float clampedTau = Mathf.Min(tau, boostDuration);
                    float frac = clampedTau / boostDuration;
                    float v = V0 + dV * frac;
                    float traveled = V0 * clampedTau + dV * clampedTau * frac * 0.5f;
                    float currentD = boostBaseD + shopDirection * traveled;
                    PlaceOnArch(shopGO.transform, currentD);
                    ApplyScrollSpeed(v);

                    if (verboseLog && logTimer >= 0.4f)
                    {
                        logTimer = 0f;
                        Debug.Log($"[ShopArrivalSequence] Shop 加速中: d={currentD:F1}°, v={v:F1}, τ={clampedTau:F2}/{boostDuration:F2}");
                    }
                    yield return null;
                }
            }

            // === Phase C: 減速 (Vpeak → 0) ===
            //   v(τ)  = Vpeak * (1 - τ / tC)
            //   s(τ)  = Vpeak*τ - Vpeak*τ²/(2*tC) = Vpeak*τ*(1 - τ/(2*tC))
            if (verboseLog)
                Debug.Log($"[ShopArrivalSequence] 減速フェーズ開始 (Vpeak={Vpeak:F1} → 0, dur={decelDuration:F2}s)");
            float decelBaseD = startDist + shopDirection * (approachDist + boostDist);
            if (decelDuration > 0f)
            {
                float tau = 0f;
                while (tau < decelDuration)
                {
                    tau += Time.deltaTime;
                    logTimer += Time.deltaTime;
                    float clampedTau = Mathf.Min(tau, decelDuration);
                    float frac = clampedTau / decelDuration;
                    float v = Vpeak * (1f - frac);
                    float traveled = Vpeak * clampedTau * (1f - 0.5f * frac);
                    float currentD = decelBaseD + shopDirection * traveled;
                    PlaceOnArch(shopGO.transform, currentD);
                    ApplyScrollSpeed(decelerateRoadWithShop ? v : V0);

                    if (verboseLog && logTimer >= 0.4f)
                    {
                        logTimer = 0f;
                        Debug.Log($"[ShopArrivalSequence] Shop 減速中: d={currentD:F1}°, v={v:F1}, τ={clampedTau:F2}/{decelDuration:F2}");
                    }
                    yield return null;
                }
            }

            // 終端でぴったり endDist (浮動小数の累積誤差をリセット)
            PlaceOnArch(shopGO.transform, endDist);
            ApplyScrollSpeed(0f);
            if (verboseLog)
                Debug.Log($"[ShopArrivalSequence] Shop 到着完了: distance={endDist:F1}°");
        }

        /// <summary>
        /// 道路と背景の scrollSpeed を一括で更新する。
        /// `_workingScrollSpeed` の符号 (= reverseRoadDirectionDuringSequence) と
        /// 背景側の符号比を保ったまま、振幅 (= 絶対値) だけ書き換える。
        /// </summary>
        void ApplyScrollSpeed(float magnitude)
        {
            if (archRoadSystem != null)
            {
                float roadSign = Mathf.Sign(_workingScrollSpeed != 0f ? _workingScrollSpeed : _savedScrollSpeed);
                if (roadSign == 0f) roadSign = 1f;
                archRoadSystem.scrollSpeed = magnitude * roadSign;
            }
            if (parallaxBackground != null && _backgroundScrollSaved)
            {
                // 背景は (_savedBackgroundScrollSpeed / _savedScrollSpeed) の比率を保つように同期
                float baseAbs = Mathf.Abs(_savedScrollSpeed) > 0.0001f ? Mathf.Abs(_savedScrollSpeed) : 1f;
                float ratio = _savedBackgroundScrollSpeed / baseAbs;
                float bgSign = Mathf.Sign(_workingBackgroundScrollSpeed != 0f ? _workingBackgroundScrollSpeed : _savedBackgroundScrollSpeed);
                if (bgSign == 0f) bgSign = 1f;
                parallaxBackground.baseScrollSpeed = magnitude * Mathf.Abs(ratio) * bgSign;
            }
        }

        /// <summary>
        /// EnemyController.UpdateVisualPosition と同じ式でアーチ上に配置する。
        /// distanceAngle が大きいほど画面奥、0 でプレイヤー手前 (= 画面中央)。
        /// </summary>
        void PlaceOnArch(Transform t, float distanceAngle)
        {
            if (archRoadSystem == null || t == null) return;

            float archRadius = archRoadSystem.archRadius;
            Vector3 archCenter = archRoadSystem.transform.position;

            float visualAngle = -distanceAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            float effectiveR = archRadius + shopHoverHeight;
            float x = Mathf.Sin(rad) * effectiveR;
            float y = -archRadius + Mathf.Cos(rad) * effectiveR;

            t.position = archCenter + new Vector3(x, y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, -visualAngle);

            // 距離による奥行きスケール (EnemyController と同じカーブ、符号無視で遠近を判定)
            float depthScale = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(Mathf.Abs(distanceAngle) / 90f));
            t.localScale = Vector3.one * shopSpriteScale * depthScale;
        }

        // ─────────────────────────────────────
        //  Player UI Tween (アーチに乗って流れる演出)
        // ─────────────────────────────────────

        /// <summary>
        /// 道路 (ArchRoadSystem.scrollSpeed) と 背景パララックス (ParallaxBackground.baseScrollSpeed)
        /// を同じカーブで同時に 0 まで落とす。ショップは scrollSpeed を読んで自動同期する。
        /// </summary>
        IEnumerator DecelerateScroll(float duration)
        {
            // 演出中の作業値 (符号反転を含む) から 0 まで落とす
            float fromRoad = _workingScrollSpeed;
            float fromBG   = _backgroundScrollSaved ? _workingBackgroundScrollSpeed : 0f;

            if (duration <= 0f)
            {
                if (archRoadSystem != null) archRoadSystem.scrollSpeed = 0f;
                if (parallaxBackground != null) parallaxBackground.baseScrollSpeed = 0f;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = SpeedDecelCurve(t);
                if (archRoadSystem != null)
                    archRoadSystem.scrollSpeed = Mathf.Lerp(fromRoad, 0f, curveT);
                if (parallaxBackground != null)
                    parallaxBackground.baseScrollSpeed = Mathf.Lerp(fromBG, 0f, curveT);
                yield return null;
            }
            if (archRoadSystem != null)     archRoadSystem.scrollSpeed = 0f;
            if (parallaxBackground != null) parallaxBackground.baseScrollSpeed = 0f;
        }

        // ─────────────────────────────────────
        //  Easing curves
        // ─────────────────────────────────────

        /// <summary>
        /// 道路速度の減速カーブ。t=0→1 で 0→1 を返す (Lerp(saved, 0, curve)) なので 0→saved・1→0)。
        /// ショップは scrollSpeed の時間積分で動くため、ここで選んだカーブに従って自動同期する。
        /// </summary>
        float SpeedDecelCurve(float t)
        {
            switch (arrivalEaseMode)
            {
                case ArrivalEaseMode.SmoothStepBoth:
                    return t * t * (3f - 2f * t);
                case ArrivalEaseMode.MatchedLinearDecel:
                case ArrivalEaseMode.LinearBoth:
                default:
                    return t; // 線形 = 等減速
            }
        }

        IEnumerator MoveRectTransform(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null) yield break;
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t); // smoothstep
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            rt.anchoredPosition = to;
        }

        // ─────────────────────────────────────
        //  Shop Fade In
        // ─────────────────────────────────────

        void SetShopAlpha(float a)
        {
            if (_shopRenderers == null) return;
            a = Mathf.Clamp01(a);
            for (int i = 0; i < _shopRenderers.Length; i++)
            {
                var sr = _shopRenderers[i];
                if (sr == null) continue;
                var c = sr.color;
                c.a = a;
                sr.color = c;
            }
        }

        IEnumerator FadeShopInCo(float duration)
        {
            if (duration <= 0f)
            {
                SetShopAlpha(1f);
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // smoothstep で滑らかに 0→1
                float a = t * t * (3f - 2f * t);
                SetShopAlpha(a);
                yield return null;
            }
            SetShopAlpha(1f);
            _fadeInCoroutine = null;
        }

        // ─────────────────────────────────────
        //  Restore (次ステージ開始 / 明示呼出し)
        // ─────────────────────────────────────

        public void RestoreAfterShop()
        {
            if (verboseLog) Debug.Log("[ShopArrivalSequence] RestoreAfterShop()");

            // コルーチン停止
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
            if (_playerMoveCoroutine != null)
            {
                StopCoroutine(_playerMoveCoroutine);
                _playerMoveCoroutine = null;
            }
            if (_decelerateCoroutine != null)
            {
                StopCoroutine(_decelerateCoroutine);
                _decelerateCoroutine = null;
            }
            if (_fadeInCoroutine != null)
            {
                StopCoroutine(_fadeInCoroutine);
                _fadeInCoroutine = null;
            }

            // ショップ画像を破棄
            if (_shopVisualInstance != null)
            {
                Destroy(_shopVisualInstance);
                _shopVisualInstance = null;
            }
            _shopRenderers = null;

            // 道路スクロール速度を復帰 (= 元の符号にも戻す)
            if (archRoadSystem != null && Mathf.Abs(_savedScrollSpeed) > 0.0001f)
                archRoadSystem.scrollSpeed = _savedScrollSpeed;

            // 背景スクロール速度を復帰
            if (parallaxBackground != null && _backgroundScrollSaved)
                parallaxBackground.baseScrollSpeed = _savedBackgroundScrollSpeed;

            // プレイヤー UI 位置を復帰
            if (playerVisualRect != null && _playerPositionSaved)
                playerVisualRect.anchoredPosition = _savedPlayerAnchored;

            // 道路揺れを再開 (位置リベース付き)
            if (_playerBumpWasSuppressed && _playerCharacterAnimator != null)
            {
                _playerCharacterAnimator.RebaseRoadBumpToCurrent();
                _playerCharacterAnimator.SetRoadBumpEnabled(true, resetToBase: false);
            }

            _sequenceRunning = false;
            _shopArrivedAtCenter = false;
            _playerPositionSaved = false;
            _playerBumpWasSuppressed = false;
            _backgroundScrollSaved = false;
        }
    }
}
