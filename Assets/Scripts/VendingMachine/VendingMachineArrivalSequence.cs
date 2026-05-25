using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 「ルートマスが VendingMachine に切り替わった瞬間」の到着演出を統括するコンポーネント。
    ///
    /// ■ ShopArrivalSequence との差分
    ///   ・敵を後方へ退場させる演出は無い (= 既存の敵はそのまま動き続ける / scrollSpeed 同期だけで止まる)
    ///   ・到着時 (中央 0°) に GamePauseService.Pause で世界全体を一時停止する
    ///   ・閉じるときは VendingMachineFlowController.CloseVendingMachine → RestoreAfterVendingMachine で復帰
    ///     ステージは続行 (次ステージへ進まない)
    ///
    /// ■ シーケンス概要
    ///   1. OnVendingMachineRouteNodeReached を受信
    ///   2. 道路スクロール速度 / 背景 / プレイヤー UI 位置を保存
    ///   3. 自販機画像 (Prefab 優先 / Sprite フォールバック) をアーチ遠方に生成
    ///   4. 解析計算ベースで 3 フェーズ (approach / boost / decel) でアーチ中央へ寄せる
    ///   5. 同時にプレイヤー UI も中央へ流れて合流
    ///   6. 中央到着 → scrollSpeed = 0 + GamePauseService.Pause("VendingMachine")
    ///   7. VendingMachineFlowController.OpenVendingMachine() を呼んで Canvas を開く
    ///   8. ユーザーが購入 / 出口 → CloseVendingMachine → RestoreAfterVendingMachine
    ///       └ ビジュアル破棄 / プレイヤー UI 復帰 / scrollSpeed 復帰 / GamePauseService.Resume
    ///
    /// ■ セットアップ
    ///   1. シーン上の任意の永続 GameObject にアタッチ (ShopArrivalSequence と同居でも可)
    ///   2. ArchRoadSystem / EnemySystem / VendingMachineFlowController をアサイン (空なら自動取得)
    ///   3. 自販機 Prefab か Sprite のどちらかをアサイン
    ///   4. プレイヤー UI (CharacterAnimator 付き) の RectTransform をアサイン
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class VendingMachineArrivalSequence : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("回転している道路。空のとき FindObjectOfType で自動取得")]
        public ArchRoadSystem archRoadSystem;

        [Tooltip("敵管理。空のとき FindObjectOfType で自動取得")]
        public EnemySystem enemySystem;

        [Tooltip("自販機 Canvas のフロー管理。空のとき FindObjectOfType で自動取得")]
        public VendingMachineFlowController vendingFlow;

        [Tooltip("背景パララックス。空のとき FindObjectOfType で自動取得 (シーンに無くても可)")]
        public ParallaxBackground parallaxBackground;

        [Header("Vending Machine Visual (Prefab 優先 / Sprite フォールバック)")]
        [Tooltip("自販機を表現するプレハブ。子に SpriteRenderer / Spinner2D 等好きに組める。")]
        public GameObject vendingMachinePrefab;

        [Tooltip("プレハブが無い場合に使う自販機 Sprite (簡易表示)")]
        public Sprite vendingMachineSprite;

        [Tooltip("Sprite フォールバック時のスケール")]
        public float vendingSpriteScale = 1f;

        [Tooltip("ON: ArchRoadSystem の SortingLayer / roadSortingOrder を基準に、自販機を道路の 1 つ後ろへ自動配置")]
        public bool autoPlaceBehindRoad = true;

        [Tooltip("autoPlaceBehindRoad=OFF のときに使う Sorting Layer 名")]
        public string sortingLayer = "UI";

        [Tooltip("autoPlaceBehindRoad=OFF のときに使う Sorting Order")]
        public int sortingOrder = -1;

        [Header("Vending Fade In (ポップイン防止)")]
        [Tooltip("ON: スポーン時 alpha=0 → 接近に合わせて 1 に上げる")]
        public bool enableFadeIn = true;

        [Tooltip("フェードイン所要秒数")]
        public float fadeInDuration = 1.2f;

        [Header("Arch 位置パラメータ")]
        [Tooltip("自販機画像の到着 distanceAngle (度)。0 でプレイヤー手前 (画面中央)")]
        public float arriveDistance = 0f;

        [Tooltip("ON: approachDuration / boostDuration / decelDuration からスポーン位置を自動逆算する。\n"
               + "減速終了時にぴったり arriveDistance に到着する。OFF だと旧来の startDistance を使う。")]
        public bool autoComputeStartDistance = true;

        [Tooltip("等速アプローチの所要秒数。0 で即加速フェーズに突入する。")]
        public float approachDuration = 0f;

        [Tooltip("autoComputeStartDistance=OFF のとき使う初期 distanceAngle (度)")]
        public float startDistance = 110f;

        [Tooltip("ON: 距離角の符号を反転 (= 画面右から手前に流れて来る)\n"
               + "OFF: 道路スクロールと同じ向き (推奨)")]
        public bool comesFromOppositeSide = false;

        [Tooltip("自販機画像の地面からの浮き高さ")]
        public float hoverHeight = 0f;

        [Tooltip("自販機画像の最終的なワールド座標 Y オフセット。\n"
               + "アーチ上の位置はそのままに、見た目だけ上下へずらしたいときに調整する。")]
        public float visualYOffset = 0f;

        [Header("Vending Exit Visual")]
        [Tooltip("ON: 自販機画面を閉じたあと、中央にある自販機ビジュアルを即消しせず、敵がいる奥方向へ流してから消す。")]
        public bool moveVisualAwayOnClose = true;

        [Tooltip("自販機が閉じるときに流れていく先の distanceAngle (度)。\n"
               + "正の値ほど敵がいる奥側へ流れる。\n"
               + "道路同期 ON のときは、この値を上限として、道路 scrollSpeed の積分ぶんだけ奥へ流れる。")]
        public float visualExitDistance = 110f;

        [Tooltip("ON: 自販機ビジュアルの退出速度を、戻り中の道路 scrollSpeed と同期する。\n"
               + "道路が 0 → 元速度へ戻るほど、自販機も同じ速度で敵がいる奥方向へ流れる。")]
        public bool syncVisualExitToRoadSpeed = true;

        [Tooltip("syncVisualExitToRoadSpeed=ON のとき、自販機の流れる速度に掛ける倍率。\n"
               + "1 = 道路と同じ / 2 = 道路の 2 倍 / 0.5 = 半分。")]
        public float visualExitRoadSpeedMultiplier = 1f;

        [Tooltip("ON: 自販機ビジュアルの退出時間を playerReturnDuration に合わせる。\n"
               + "syncVisualExitToRoadSpeed=OFF の場合のみ使用。\n"
               + "syncVisualExitToRoadSpeed=ON の場合は visualExitDuration が消えるまでの時間になる。")]
        public bool matchVisualExitDurationToPlayerReturn = true;

        [Tooltip("自販機ビジュアルが消えるまでの時間。\n"
               + "syncVisualExitToRoadSpeed=ON のときも、この秒数で自販機画像を消す。")]
        public float visualExitDuration = 0.45f;

        [Header("Timing")]
        [Tooltip("【減速フェーズの所要秒数】道路スクロール速度が saved → 0 に落ちきるまでの時間。")]
        public float arriveDuration = 1.6f;

        [Tooltip("自販機到着後、Canvas を開くまでの停止演出の秒数 (Realtime で待つ)")]
        public float pauseSecondsBeforeCanvas = 0.4f;

        [Header("Speed Boost (踏ん張る演出)")]
        [Tooltip("ON: マス到着時にいったん scrollSpeed を boostPeakMultiplier 倍まで上げてから減速して 0 にする。\n"
               + "自販機も加速・減速とも同期して動く。")]
        public bool enableSpeedBoost = true;

        [Tooltip("加速のピーク倍率。V_peak = V0 × multiplier")]
        public float boostPeakMultiplier = 1.6f;

        [Tooltip("V0 → V_peak まで加速する所要秒数")]
        public float boostDuration = 0.4f;

        [Header("Player Visual")]
        [Tooltip("プレイヤー UI (CharacterAnimator のあるオブジェクト) の RectTransform")]
        public RectTransform playerVisualRect;

        [Tooltip("到着時にプレイヤーが流れて来る anchoredPosition (= 画面中央付近)。\n"
               + "preserveOriginalY が ON のときは Y 成分は無視されます。")]
        public Vector2 playerCenterAnchored = Vector2.zero;

        [Tooltip("ON: 移動中も元の anchoredPosition.y を維持する")]
        public bool preserveOriginalY = true;

        [Tooltip("プレイヤー UI の移動所要秒数。空(<=0) で arriveDuration と同期")]
        public float playerMoveDuration = -1f;

        [Tooltip("ON: 自販機を閉じた後、プレイヤー UI を元位置へ滑らかに戻す。\n"
               + "OFF の場合は従来通り即座に元位置へ戻す。")]
        public bool restorePlayerSmoothly = true;

        [Tooltip("自販機を閉じた後、プレイヤー UI が元位置へ戻るまでの秒数。\n"
               + "Canvas 表示中は Time.timeScale=0 なので、内部では unscaledDeltaTime で動かす。")]
        public float playerReturnDuration = 0.45f;

        [Tooltip("ON: 自販機演出中、プレイヤー UI が動いた分に合わせて敵も同じ方向へ見た目移動する。")]
        public bool moveEnemiesWithPlayer = true;

        [Tooltip("プレイヤー UI の anchoredPosition 移動量を、敵のワールド移動量へ変換する倍率。\n"
               + "例: (0.01, 0.01) なら UI が 100 動くと敵はワールド 1 動く。\n"
               + "方向はプレイヤーと同じで、量だけここで調整する。")]
        public Vector2 enemyWorldOffsetPerPlayerAnchoredUnit = new Vector2(0.01f, 0.01f);

        [Tooltip("ON: 演出中はプレイヤーの道路揺れを一時 OFF にする")]
        public bool suppressPlayerRoadBumpDuringSequence = true;

        [Header("Behavior")]
        [Tooltip("ステージ進行中 (currentState == Playing) でなければ演出を始めない")]
        public bool requirePlayingState = true;

        [Header("Debug")]
        [Tooltip("ON: フロー各段階の Debug.Log を出力する")]
        public bool verboseLog = true;

        [Tooltip("ON: 再生中に指定キーで手動トリガー")]
        public bool enableDebugKey = true;

        [Tooltip("手動トリガー用キー")]
        public KeyCode debugTriggerKey = KeyCode.V;

        [Tooltip("演出開始時に archRoadSystem.scrollSpeed が 0 (または非常に小さい) だった場合の代替値。")]
        public float debugFallbackScrollSpeed = 15f;

        // ─── runtime ───
        bool _sequenceRunning;
        bool _arrivedAtCenter;

        /// <summary>到着演出が現在走っているか</summary>
        public bool IsSequenceRunning => _sequenceRunning;

        GameObject _visualInstance;
        Coroutine _sequenceCoroutine;
        Coroutine _playerMoveCoroutine;
        Coroutine _fadeInCoroutine;
        Coroutine _restoreCoroutine;
        float _savedScrollSpeed;
        float _savedBackgroundScrollSpeed;
        bool _backgroundScrollSaved;
        SpriteRenderer[] _visualRenderers;
        Vector2 _savedPlayerAnchored;
        bool _playerPositionSaved;
        CharacterAnimator _playerCharacterAnimator;
        bool _playerBumpWasSuppressed;
        float _currentVisualDistance;

        // ─────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────

        void Awake()
        {
            if (archRoadSystem     == null) archRoadSystem     = FindObjectOfType<ArchRoadSystem>();
            if (enemySystem        == null) enemySystem        = FindObjectOfType<EnemySystem>();
            if (vendingFlow        == null) vendingFlow        = FindObjectOfType<VendingMachineFlowController>();
            if (parallaxBackground == null) parallaxBackground = FindObjectOfType<ParallaxBackground>();
        }

        void OnEnable()
        {
            GameEvents.OnVendingMachineRouteNodeReached += HandleRouteNodeReached;
        }

        void OnDisable()
        {
            GameEvents.OnVendingMachineRouteNodeReached -= HandleRouteNodeReached;
        }

        void Update()
        {
            if (!enableDebugKey) return;
            if (Input.GetKeyDown(debugTriggerKey))
                ForceTriggerForDebug();
        }

        // ─────────────────────────────────────
        //  Entry
        // ─────────────────────────────────────

        void HandleRouteNodeReached()
        {
            if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] OnVendingMachineRouteNodeReached 受信");

            if (_sequenceRunning)
            {
                if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] 既に演出中のためスキップ");
                return;
            }
            if (requirePlayingState
                && GameManager.Instance != null
                && GameManager.Instance.currentState != GameState.Playing)
            {
                if (verboseLog)
                    Debug.LogWarning($"[VendingMachineArrivalSequence] requirePlayingState で中断 (state={GameManager.Instance.currentState})");
                return;
            }

            if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = StartCoroutine(RunArrivalSequence());
        }

        [ContextMenu("Test: Trigger Vending Machine Arrival")]
        public void ForceTriggerForDebug()
        {
            Debug.Log($"<color=#ffcc00>[VendingMachineArrivalSequence] ★★ ForceTriggerForDebug() ★★</color>\n" +
                      $"  archRoadSystem={(archRoadSystem != null ? archRoadSystem.name : "<null>")}\n" +
                      $"  enemySystem={(enemySystem != null ? enemySystem.name : "<null>")}\n" +
                      $"  vendingFlow={(vendingFlow != null ? vendingFlow.name : "<null>")}\n" +
                      $"  vendingMachinePrefab={(vendingMachinePrefab != null ? vendingMachinePrefab.name : "<null>")}\n" +
                      $"  vendingMachineSprite={(vendingMachineSprite != null ? vendingMachineSprite.name : "<null>")}\n" +
                      $"  playerVisualRect={(playerVisualRect != null ? playerVisualRect.name : "<null>")}\n" +
                      $"  _sequenceRunning={_sequenceRunning}");

            if (enemySystem != null)
                enemySystem.PauseSurvivalTickOnly();

            if (_sequenceCoroutine   != null) { StopCoroutine(_sequenceCoroutine);   _sequenceCoroutine   = null; }
            if (_playerMoveCoroutine != null) { StopCoroutine(_playerMoveCoroutine); _playerMoveCoroutine = null; }
            _sequenceRunning = false;

            _sequenceCoroutine = StartCoroutine(RunArrivalSequence());
        }

        // ─────────────────────────────────────
        //  Sequence
        // ─────────────────────────────────────

        IEnumerator RunArrivalSequence()
        {
            _sequenceRunning = true;
            _arrivedAtCenter = false;

            if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] === Phase 1: 道路速度保存 (敵退場は無し) ===");

            // 道路スクロール速度を保存 & 演出中の作業値を決定
            if (archRoadSystem != null)
            {
                _savedScrollSpeed = Mathf.Abs(archRoadSystem.scrollSpeed) > 0.0001f
                    ? archRoadSystem.scrollSpeed
                    : 0f;

                if (Mathf.Abs(_savedScrollSpeed) < 0.0001f && debugFallbackScrollSpeed > 0.0001f)
                {
                    Debug.LogWarning($"[VendingMachineArrivalSequence] road scrollSpeed が 0 だったので debugFallbackScrollSpeed={debugFallbackScrollSpeed} で復帰");
                    _savedScrollSpeed = debugFallbackScrollSpeed;
                    archRoadSystem.scrollSpeed = _savedScrollSpeed;
                }

                if (verboseLog)
                    Debug.Log($"[VendingMachineArrivalSequence] road scroll speed saved={_savedScrollSpeed}");
            }

            // 背景パララックスも保存
            if (parallaxBackground != null)
            {
                _savedBackgroundScrollSpeed = parallaxBackground.baseScrollSpeed;
                _backgroundScrollSaved = true;

                if (Mathf.Abs(_savedBackgroundScrollSpeed) < 0.0001f && debugFallbackScrollSpeed > 0.0001f)
                {
                    Debug.LogWarning($"[VendingMachineArrivalSequence] background scrollSpeed も 0 → debugFallbackScrollSpeed で復帰");
                    _savedBackgroundScrollSpeed = debugFallbackScrollSpeed;
                    parallaxBackground.baseScrollSpeed = _savedBackgroundScrollSpeed;
                }
            }

            // プレイヤー UI 位置保存 & 道路揺れを一時 OFF
            if (playerVisualRect != null)
            {
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
                        if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] CharacterAnimator の道路揺れを一時 OFF");
                        _playerCharacterAnimator.SetRoadBumpEnabled(false, resetToBase: false);
                        _playerBumpWasSuppressed = true;
                    }
                }
            }

            if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] === Phase 2: 自販機生成 ===");
            _visualInstance = SpawnVisual();

            // フェードイン
            if (_visualInstance != null && enableFadeIn && fadeInDuration > 0f)
            {
                _visualRenderers = _visualInstance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
                if (_visualRenderers != null && _visualRenderers.Length > 0)
                {
                    SetVisualAlpha(0f);
                    if (_fadeInCoroutine != null) StopCoroutine(_fadeInCoroutine);
                    _fadeInCoroutine = StartCoroutine(FadeInCo(fadeInDuration));
                }
            }

            // プレイヤー UI の移動を並行で開始
            if (playerVisualRect != null)
            {
                float autoTotal = approachDuration
                                + (enableSpeedBoost && boostPeakMultiplier > 1.0001f ? boostDuration : 0f)
                                + arriveDuration;
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
            float endD = arriveDistance * sign;

            float V0 = Mathf.Abs(_savedScrollSpeed);
            float aDuration   = Mathf.Max(0f, approachDuration);
            float decelDur    = Mathf.Max(0.0001f, arriveDuration);

            bool boost     = enableSpeedBoost && boostPeakMultiplier > 1.0001f && boostDuration > 0f;
            float Vpeak    = boost ? V0 * boostPeakMultiplier : V0;
            float boostDur = boost ? boostDuration : 0f;

            float approachDist = V0 * aDuration;
            float boostDist    = (V0 + Vpeak) * 0.5f * boostDur;
            float decelDist    = Vpeak * decelDur * 0.5f;
            float totalDist    = approachDist + boostDist + decelDist;

            float visualDirection = (sign >= 0f) ? -1f : +1f;

            float startD;
            if (autoComputeStartDistance)
            {
                startD = endD - visualDirection * totalDist;
                if (verboseLog)
                    Debug.Log($"[VendingMachineArrivalSequence] === Phase 3: start={startD:F1}°, end={endD:F1}°, " +
                              $"V0={V0:F1}, Vpeak={Vpeak:F1}, " +
                              $"approach={aDuration:F2}s, boost={boostDur:F2}s, decel={decelDur:F2}s ===");
            }
            else
            {
                startD = startDistance * sign;
            }

            yield return MoveVisualAnalytic(_visualInstance, startD, endD, visualDirection,
                                            V0, Vpeak, aDuration, boostDur, decelDur);

            _arrivedAtCenter = true;

            if (archRoadSystem != null)
                archRoadSystem.scrollSpeed = 0f;
            if (parallaxBackground != null)
                parallaxBackground.baseScrollSpeed = 0f;

            if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] === Phase 4: 到着 → Canvas オープン ===");

            // 一呼吸おく (Realtime: この時点ではまだ Pause 前なので scaled でも OK だが、
            // OpenVendingMachine 後のことを考えて Realtime に統一)
            if (pauseSecondsBeforeCanvas > 0f)
                yield return new WaitForSecondsRealtime(pauseSecondsBeforeCanvas);

            // VendingMachineFlowController に Canvas を開かせる。
            // FlowController 側で GamePauseService.Pause("VendingMachine") が走り、世界が全停止する。
            if (vendingFlow != null)
            {
                if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] VendingMachineFlowController.OpenVendingMachine() 呼び出し");
                vendingFlow.OpenVendingMachine();
            }
            else if (verboseLog)
            {
                Debug.LogWarning("[VendingMachineArrivalSequence] vendingFlow が null のため Canvas を開けません");
            }

            // この時点で _sequenceCoroutine は完了。
            // 復帰 (RestoreAfterVendingMachine) は VendingMachineFlowController.CloseVendingMachine から呼ばれる。
            _sequenceCoroutine = null;
        }

        // ─────────────────────────────────────
        //  Visual spawn / sorting
        // ─────────────────────────────────────

        GameObject SpawnVisual()
        {
            GameObject go;

            if (vendingMachinePrefab != null)
            {
                go = Instantiate(vendingMachinePrefab);
                go.name = "VendingMachineVisualInstance";
                ApplySorting(go);
                if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] vendingMachinePrefab を Instantiate");
            }
            else
            {
                go = new GameObject("VendingMachineVisualSprite");
                if (vendingMachineSprite != null)
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = vendingMachineSprite;
                    ApplySorting(go);
                }
                else if (verboseLog)
                {
                    Debug.LogError("[VendingMachineArrivalSequence] vendingMachineSprite が null。Prefab も Sprite も無いので空オブジェクトになります。");
                }
                go.transform.localScale = Vector3.one * Mathf.Max(0.01f, vendingSpriteScale);
            }

            go.transform.SetParent(null, worldPositionStays: true);
            return go;
        }

        void ApplySorting(GameObject root)
        {
            if (root == null) return;

            string layerName = autoPlaceBehindRoad && archRoadSystem != null
                ? archRoadSystem.sortingLayer
                : sortingLayer;
            int order = autoPlaceBehindRoad && archRoadSystem != null
                ? archRoadSystem.roadSortingOrder - 1
                : sortingOrder;

            if (!SortingLayerExists(layerName))
            {
                Debug.LogWarning($"[VendingMachineArrivalSequence] Sorting Layer '{layerName}' 未登録 → 'Default' にフォールバック");
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
        }

        bool SortingLayerExists(string layerName)
        {
            if (layerName == "Default") return true;
            foreach (var l in SortingLayer.layers)
                if (l.name == layerName) return true;
            return false;
        }

        // ─────────────────────────────────────
        //  Analytic move (ShopArrivalSequence と同じ手法)
        // ─────────────────────────────────────

        IEnumerator MoveVisualAnalytic(GameObject visualGO, float startDist, float endDist,
                                       float direction,
                                       float V0, float Vpeak,
                                       float approachDur, float boostDur,
                                       float decelDur)
        {
            if (visualGO == null || archRoadSystem == null)
                yield break;

            PlaceOnArch(visualGO.transform, startDist);
            _currentVisualDistance = startDist;

            if (V0 < 0.0001f || (approachDur <= 0f && boostDur <= 0f && decelDur <= 0f))
            {
                PlaceOnArch(visualGO.transform, endDist);
                ApplyScrollSpeed(0f);
                yield break;
            }

            float approachDist = V0 * approachDur;
            float boostDist    = (V0 + Vpeak) * 0.5f * boostDur;

            // === Phase A: 等速アプローチ ===
            ApplyScrollSpeed(V0);
            if (approachDur > 0f)
            {
                float t = 0f;
                while (t < approachDur)
                {
                    t += Time.deltaTime;
                    float clamped = Mathf.Min(t, approachDur);
                    float traveled = V0 * clamped;
                    float currentD = startDist + direction * traveled;
                    PlaceOnArch(visualGO.transform, currentD);
                    _currentVisualDistance = currentD;
                    yield return null;
                }
            }

            // === Phase B: 加速 (V0 → Vpeak) ===
            if (boostDur > 0f && Vpeak > V0 + 0.0001f)
            {
                float baseD = startDist + direction * approachDist;
                float dV = Vpeak - V0;
                float tau = 0f;
                while (tau < boostDur)
                {
                    tau += Time.deltaTime;
                    float clampedTau = Mathf.Min(tau, boostDur);
                    float frac = clampedTau / boostDur;
                    float v = V0 + dV * frac;
                    float traveled = V0 * clampedTau + dV * clampedTau * frac * 0.5f;
                    float currentD = baseD + direction * traveled;
                    PlaceOnArch(visualGO.transform, currentD);
                    _currentVisualDistance = currentD;
                    ApplyScrollSpeed(v);
                    yield return null;
                }
            }

            // === Phase C: 減速 (Vpeak → 0) ===
            float decelBaseD = startDist + direction * (approachDist + boostDist);
            if (decelDur > 0f)
            {
                float tau = 0f;
                while (tau < decelDur)
                {
                    tau += Time.deltaTime;
                    float clampedTau = Mathf.Min(tau, decelDur);
                    float frac = clampedTau / decelDur;
                    float v = Vpeak * (1f - frac);
                    float traveled = Vpeak * clampedTau * (1f - 0.5f * frac);
                    float currentD = decelBaseD + direction * traveled;
                    PlaceOnArch(visualGO.transform, currentD);
                    _currentVisualDistance = currentD;
                    ApplyScrollSpeed(v);
                    yield return null;
                }
            }

            PlaceOnArch(visualGO.transform, endDist);
            _currentVisualDistance = endDist;
            ApplyScrollSpeed(0f);
        }

        void ApplyScrollSpeed(float magnitude)
        {
            if (archRoadSystem != null)
            {
                float roadSign = Mathf.Sign(_savedScrollSpeed != 0f ? _savedScrollSpeed : 1f);
                if (roadSign == 0f) roadSign = 1f;
                archRoadSystem.scrollSpeed = magnitude * roadSign;
            }
            if (parallaxBackground != null && _backgroundScrollSaved)
            {
                float baseAbs = Mathf.Abs(_savedScrollSpeed) > 0.0001f ? Mathf.Abs(_savedScrollSpeed) : 1f;
                float ratio = _savedBackgroundScrollSpeed / baseAbs;
                float bgSign = Mathf.Sign(_savedBackgroundScrollSpeed != 0f ? _savedBackgroundScrollSpeed : 1f);
                if (bgSign == 0f) bgSign = 1f;
                parallaxBackground.baseScrollSpeed = magnitude * Mathf.Abs(ratio) * bgSign;
            }
        }

        void PlaceOnArch(Transform t, float distanceAngle)
        {
            if (archRoadSystem == null || t == null) return;

            float archRadius = archRoadSystem.archRadius;
            Vector3 archCenter = archRoadSystem.transform.position;

            float visualAngle = -distanceAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            float effectiveR = archRadius + hoverHeight;
            float x = Mathf.Sin(rad) * effectiveR;
            float y = -archRadius + Mathf.Cos(rad) * effectiveR;

            t.position = archCenter + new Vector3(x, y + visualYOffset, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, -visualAngle);

            float depthScale = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(Mathf.Abs(distanceAngle) / 90f));
            t.localScale = Vector3.one * vendingSpriteScale * depthScale;
        }

        // ─────────────────────────────────────
        //  Player UI Tween
        // ─────────────────────────────────────

        IEnumerator MoveRectTransform(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null) yield break;
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                ApplyEnemyOffsetFromPlayerAnchored(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t); // smoothstep
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
                ApplyEnemyOffsetFromPlayerAnchored(rt.anchoredPosition);
                yield return null;
            }
            rt.anchoredPosition = to;
            ApplyEnemyOffsetFromPlayerAnchored(to);
        }

        IEnumerator MoveRectTransformUnscaled(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null) yield break;
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                ApplyEnemyOffsetFromPlayerAnchored(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t); // smoothstep
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
                ApplyEnemyOffsetFromPlayerAnchored(rt.anchoredPosition);
                yield return null;
            }
            rt.anchoredPosition = to;
            ApplyEnemyOffsetFromPlayerAnchored(to);
        }

        void ApplyEnemyOffsetFromPlayerAnchored(Vector2 playerAnchored)
        {
            if (!moveEnemiesWithPlayer || !_playerPositionSaved)
                return;

            Vector2 delta = playerAnchored - _savedPlayerAnchored;
            EnemySystem.SetEventVisualOffset(new Vector3(
                delta.x * enemyWorldOffsetPerPlayerAnchoredUnit.x,
                delta.y * enemyWorldOffsetPerPlayerAnchoredUnit.y,
                0f));
        }

        // ─────────────────────────────────────
        //  Fade In
        // ─────────────────────────────────────

        void SetVisualAlpha(float a)
        {
            if (_visualRenderers == null) return;
            a = Mathf.Clamp01(a);
            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                var sr = _visualRenderers[i];
                if (sr == null) continue;
                var c = sr.color;
                c.a = a;
                sr.color = c;
            }
        }

        IEnumerator FadeInCo(float duration)
        {
            if (duration <= 0f)
            {
                SetVisualAlpha(1f);
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float a = t * t * (3f - 2f * t);
                SetVisualAlpha(a);
                yield return null;
            }
            SetVisualAlpha(1f);
            _fadeInCoroutine = null;
        }

        void DestroyVisualInstance()
        {
            if (_visualInstance != null)
            {
                Destroy(_visualInstance);
                _visualInstance = null;
            }
            _visualRenderers = null;
        }

        // ─────────────────────────────────────
        //  Restore (自販機 Canvas が閉じたとき VendingMachineFlowController から呼ばれる)
        // ─────────────────────────────────────

        public Coroutine RestoreAfterVendingMachine(System.Action onComplete = null)
        {
            if (verboseLog) Debug.Log("[VendingMachineArrivalSequence] RestoreAfterVendingMachine()");

            if (_restoreCoroutine != null)
            {
                StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }

            _restoreCoroutine = StartCoroutine(RestoreAfterVendingMachineCo(onComplete));
            return _restoreCoroutine;
        }

        IEnumerator RestoreAfterVendingMachineCo(System.Action onComplete)
        {
            if (_sequenceCoroutine != null)   { StopCoroutine(_sequenceCoroutine);   _sequenceCoroutine = null; }
            if (_playerMoveCoroutine != null) { StopCoroutine(_playerMoveCoroutine); _playerMoveCoroutine = null; }
            if (_fadeInCoroutine != null)     { StopCoroutine(_fadeInCoroutine);     _fadeInCoroutine = null; }

            float playerRestoreDuration = restorePlayerSmoothly && playerReturnDuration > 0f
                ? playerReturnDuration
                : 0f;
            float visualRestoreDuration = moveVisualAwayOnClose && _visualInstance != null
                ? (syncVisualExitToRoadSpeed
                    ? Mathf.Max(0f, visualExitDuration)
                    : (matchVisualExitDurationToPlayerReturn ? Mathf.Max(0f, playerReturnDuration) : Mathf.Max(0f, visualExitDuration)))
                : 0f;
            float returnDuration = Mathf.Max(playerRestoreDuration, visualRestoreDuration);
            if (returnDuration <= 0f && moveVisualAwayOnClose && _visualInstance != null)
                returnDuration = Mathf.Max(0f, visualExitDuration);

            if (returnDuration > 0f)
            {
                Vector2 playerFrom = playerVisualRect != null ? playerVisualRect.anchoredPosition : _savedPlayerAnchored;
                float roadFrom = archRoadSystem != null ? archRoadSystem.scrollSpeed : 0f;
                float bgFrom = parallaxBackground != null ? parallaxBackground.baseScrollSpeed : 0f;
                float visualFromD = _currentVisualDistance;
                float visualToD = Mathf.Abs(visualExitDistance);
                float elapsed = 0f;

                while (elapsed < returnDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / returnDuration);
                    float smoothT = t * t * (3f - 2f * t);
                    float playerT = playerRestoreDuration > 0f ? Mathf.Clamp01(elapsed / playerRestoreDuration) : 1f;
                    playerT = playerT * playerT * (3f - 2f * playerT);
                    float visualT = visualRestoreDuration > 0f ? Mathf.Clamp01(elapsed / visualRestoreDuration) : 1f;
                    visualT = visualT * visualT * (3f - 2f * visualT);

                    if (playerVisualRect != null && _playerPositionSaved)
                    {
                        playerVisualRect.anchoredPosition = Vector2.Lerp(playerFrom, _savedPlayerAnchored, playerT);
                        ApplyEnemyOffsetFromPlayerAnchored(playerVisualRect.anchoredPosition);
                    }

                    float currentRoadSpeed = Mathf.Lerp(roadFrom, _savedScrollSpeed, smoothT);

                    if (archRoadSystem != null)
                        archRoadSystem.scrollSpeed = currentRoadSpeed;

                    if (parallaxBackground != null && _backgroundScrollSaved)
                        parallaxBackground.baseScrollSpeed = Mathf.Lerp(bgFrom, _savedBackgroundScrollSpeed, smoothT);

                    if (moveVisualAwayOnClose && _visualInstance != null)
                    {
                        float d;
                        if (visualRestoreDuration <= 0f)
                        {
                            d = visualToD;
                        }
                        else if (syncVisualExitToRoadSpeed)
                        {
                            float syncedDelta = Mathf.Abs(currentRoadSpeed) * Mathf.Max(0f, visualExitRoadSpeedMultiplier) * Time.unscaledDeltaTime;
                            d = Mathf.Min(visualToD, _currentVisualDistance + syncedDelta);
                        }
                        else
                        {
                            d = Mathf.Lerp(visualFromD, visualToD, visualT);
                        }

                        PlaceOnArch(_visualInstance.transform, d);
                        _currentVisualDistance = d;

                        if (visualRestoreDuration > 0f && elapsed >= visualRestoreDuration)
                            DestroyVisualInstance();
                    }

                    yield return null;
                }
            }

            if (moveVisualAwayOnClose && _visualInstance != null)
            {
                float exitD = Mathf.Abs(visualExitDistance);
                PlaceOnArch(_visualInstance.transform, exitD);
                _currentVisualDistance = exitD;
            }

            DestroyVisualInstance();

            // 最終値を厳密に復帰
            if (playerVisualRect != null && _playerPositionSaved)
                playerVisualRect.anchoredPosition = _savedPlayerAnchored;
            EnemySystem.SetEventVisualOffset(Vector3.zero);

            if (archRoadSystem != null && Mathf.Abs(_savedScrollSpeed) > 0.0001f)
                archRoadSystem.scrollSpeed = _savedScrollSpeed;

            if (parallaxBackground != null && _backgroundScrollSaved)
                parallaxBackground.baseScrollSpeed = _savedBackgroundScrollSpeed;

            if (returnDuration <= 0f)
            {
                if (playerVisualRect != null && _playerPositionSaved)
                    ApplyEnemyOffsetFromPlayerAnchored(_savedPlayerAnchored);
                EnemySystem.SetEventVisualOffset(Vector3.zero);
            }

            // 道路揺れを再開
            if (_playerBumpWasSuppressed && _playerCharacterAnimator != null)
            {
                _playerCharacterAnimator.RebaseRoadBumpToCurrent();
                _playerCharacterAnimator.SetRoadBumpEnabled(true, resetToBase: false);
            }

            // EnemySystem のサバイバルタイマーを再開 (= ステージ続行)
            if (enemySystem != null)
                enemySystem.ResumeSurvivalTick();

            _sequenceRunning = false;
            _arrivedAtCenter = false;
            _playerPositionSaved = false;
            _playerBumpWasSuppressed = false;
            _backgroundScrollSaved = false;
            _restoreCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
