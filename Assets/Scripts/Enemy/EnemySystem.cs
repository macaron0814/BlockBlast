using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class EnemySystem : MonoBehaviour
    {
        [Header("References")]
        public ArchRoadSystem archRoadSystem;

        [Header("Wave Config")]
        [Tooltip("ステージ番号をインデックスにしたウェーブデータ配列")]
        public EnemyWaveData[] stageWaves;

        [Header("Stage Survival Time")]
        [Tooltip("ステージごとの制限時間 (秒)。0 以下なら WaveData の survivalTime を使用")]
        public float[] stageSurvivalTimes;

        [Header("Enemy Movement")]
        [Tooltip("全敵のチェイス速度に掛かる倍率。\n1 = 通常 / 0.5 = 半分の速さ / 2 = 倍速\n個別 EnemyData.chaseSpeed を変えずに全体ペースだけ調整したい時に使用。")]
        [Range(0f, 5f)]
        public float enemyMoveSpeedMultiplier = 1f;

        [Tooltip("ON: 敵が遠いほど速く、プレイヤーに近づくほど遅くなる。\nEnemyData.chaseSpeed は基準速度としてそのまま使う。")]
        public bool useDistanceBasedChaseSpeed = false;

        [Tooltip("プレイヤーに最も近い位置での速度倍率。\n例: 0.5 なら chaseSpeed=4 の敵は近距離で 2 になる。")]
        [Range(0.05f, 5f)]
        public float nearChaseSpeedMultiplier = 0.5f;

        [Tooltip("出現位置付近での速度倍率。\n例: 2 なら chaseSpeed=4 の敵は遠距離で 8 になる。")]
        [Range(0.05f, 5f)]
        public float farChaseSpeedMultiplier = 2f;

        [Tooltip("距離 0=プレイヤー付近 / 1=出現位置付近。\nY=0 で near、Y=1 で far の間を補間する。\n直線なら一定グラデーション、Ease 系なら近距離で粘る/遠距離で一気に来る調整ができる。")]
        public AnimationCurve distanceChaseSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("ON: 遠距離を速く・近距離を遅くしても、ノックバック等が無い場合の到達時間が従来の一定速度と同じになるよう自動補正する。")]
        public bool preserveDistanceChaseArrivalTime = true;

        [Header("Enemy Knockback")]
        [Tooltip("全敵が弾を受けたときのノックバック量に掛かる倍率。\n1 = 通常 / 0 = ノックバックなし / 5 = 5 倍 (派手)\n個別 EnemyData.knockbackPerHit を変えずに全体感を調整したい時に使用。")]
        [Range(0f, 10f)]
        public float enemyKnockbackMultiplier = 3f;

        [Tooltip("ノックバック減衰の強さ (1 秒あたりの減衰率)。\n小さいほど勢いが長く残り、大きいほど即停止する。")]
        [Range(0.5f, 20f)]
        public float enemyKnockbackDamping = 4.5f;

        [Header("Enemy Spawn Height")]
        [Tooltip("敵の出現高さに加える上下方向のランダム振れ幅 (ワールド単位)。\n0 = 全員 EnemyData.hoverHeight ぴったり / 0.5 = ±0.5 の範囲でランダム\nわちゃわちゃ出すならこの値を上げる。EnemyData.hoverHeight が「軸」になる。")]
        [Range(0f, 5f)]
        public float enemyHoverHeightVariance = 0f;

        [Header("Y-Sort (擬似奥行き)")]
        [Tooltip("ON: 敵の Y 座標が低いほど手前に表示する (画面下にいる敵が前)。\nOFF: 旧来の distanceAngle ベースで sortingOrder を決める")]
        public bool sortEnemiesByY = true;

        [Tooltip("Y → sortingOrder への変換倍率。\n大きいほど僅かな Y 差でも順序が大きく変わる。\n例) 100 で Y=0 と Y=0.01 が 1 だけ差が付く")]
        [Range(1f, 1000f)]
        public float ySortScale = 100f;

        [Tooltip("Y ソート時のベース sortingOrder。\n他のレイヤー (UI / 道路など) と被らないようにオフセット")]
        public int ySortBaseOrder = 0;

        // ─── 全敵共通パラメータの static 公開 (EnemyController から参照) ──────────
        /// <summary>全敵のチェイス速度に掛かる倍率。</summary>
        public static float CurrentMoveSpeedMultiplier { get; private set; } = 1f;
        /// <summary>true: 距離に応じてチェイス速度倍率を変える。</summary>
        public static bool CurrentUseDistanceBasedChaseSpeed { get; private set; } = false;
        /// <summary>プレイヤー付近での速度倍率。</summary>
        public static float CurrentNearChaseSpeedMultiplier { get; private set; } = 0.5f;
        /// <summary>出現位置付近での速度倍率。</summary>
        public static float CurrentFarChaseSpeedMultiplier { get; private set; } = 2f;
        /// <summary>距離別速度の補間カーブ。</summary>
        public static AnimationCurve CurrentDistanceChaseSpeedCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        /// <summary>到達時間を従来と揃えるための補正倍率。</summary>
        public static float CurrentDistanceChaseTimeNormalizeMultiplier { get; private set; } = 1f;
        /// <summary>全敵が受けるノックバック量に掛かる倍率。</summary>
        public static float CurrentKnockbackMultiplier { get; private set; } = 1f;
        /// <summary>ノックバック減衰の強さ (1 秒あたり)。</summary>
        public static float CurrentKnockbackDamping    { get; private set; } = 4.5f;
        /// <summary>敵の出現高さの上下振れ幅 (Initialize 時に消費)。</summary>
        public static float CurrentHoverHeightVariance { get; private set; } = 0f;
        /// <summary>true: 敵の sortingOrder を Y 座標基準で決める。</summary>
        public static bool   CurrentSortByY            { get; private set; } = true;
        /// <summary>Y → sortingOrder の変換倍率。</summary>
        public static float  CurrentYSortScale         { get; private set; } = 100f;
        /// <summary>Y ソート時のベース sortingOrder。</summary>
        public static int    CurrentYSortBaseOrder     { get; private set; } = 0;

        /// <summary>
        /// プレイヤースピード倍率。1.0 = 通常、1.25 = 25% 速い感覚に見せる。
        /// 敵側にこの逆数が掛かることで「敵が遅くなる = プレイヤーが速くなった」感覚を作る。
        /// PlayerEffectState.PlayerSpeedMultiplier と同期される。
        /// </summary>
        public static float CurrentPlayerSpeedMultiplier { get; private set; } = 1f;

        /// <summary>
        /// ルートイベント演出中だけ敵全体に乗せる見た目オフセット。
        /// 自販機到着などでプレイヤー UI と敵を同じ方向へスライドさせるために使う。
        /// </summary>
        public static Vector3 CurrentEventVisualOffset { get; private set; } = Vector3.zero;

        [Header("Bullet Spawn")]
        [Tooltip("弾の発射起点。未設定時はアーチ角度 0")]
        public Transform bulletSpawnPoint;

        [Header("Bullet Settings")]
        public float bulletSpeed = 80f;
        public float bulletBounceHeight = 0.7f;
        public float bulletBounceFrequency = 3.5f;
        public float bulletSpawnInterval = 0.1f;
        [Tooltip("画面外と判定するビューポート余白。大きいほど画面外に出てから消える")]
        public float bulletViewportExitMargin = 0.05f;

        [Header("Bullet Appearance")]
        public Sprite bulletSprite;
        public Color bulletColor = new Color(1f, 0.95f, 0.35f);
        public float bulletScale = 0.35f;

        [Header("Bullet Hit Detection")]
        public float bulletHitAngleOffset = 0f;
        public float bulletHitAngleRadius = 2f;

        [Header("Game Over Line")]
        [Tooltip("敵がこの角度以下に到達するとゲームオーバー (度)。値が大きいほどプレイヤーから遠い")]
        public float gameOverAngle = 5f;

        [Header("Line Clear Multiplier (同時消し倍率)")]
        [Tooltip("同時に消したライン数 (= rows + columns の合計) に対する攻撃倍率の表。\n" +
                 "・index 0 = 1 ライン同時消し (= 通常)\n" +
                 "・index 1 = 2 ライン同時消し\n" +
                 "・index 2 = 3 ライン同時消し ...\n" +
                 "末尾を超える数は末尾の値が cap として使われる。\n" +
                 "デフォルト [1,2,3,4,5] = ライン数そのまま。\n" +
                 "この倍率は\n" +
                 " ・敵の HP 減少量 (damage) ※ Mathf.RoundToInt で切り捨て\n" +
                 " ・敵のノックバック量\n" +
                 "の両方に同じ値で乗る。")]
        public List<float> lineClearMultiplierTable = new List<float> { 1f, 2f, 3f, 4f, 5f };

        readonly List<EnemyController> _enemies = new List<EnemyController>();
        readonly List<PlayerBullet> _activeBullets = new List<PlayerBullet>();
        readonly List<BulletHitCandidate> _bulletHitCandidates = new List<BulletHitCandidate>();

        struct BulletHitCandidate
        {
            public EnemyController enemy;
            public int enemyId;
            public Vector3 hitPosition;
            public float distanceToPlayerX;
        }

        // 同時消しライン数から計算される倍率 (damage + knockback 両方に効く)
        float _lineClearMultiplier = 1f;

        // --- Wave state ---
        EnemyWaveData _currentWaveData;
        int _currentWaveIndex;
        Coroutine _waveCoroutine;
        bool _wavesRunning;
        readonly List<RouteNodeRuntime> _routeNodes = new List<RouteNodeRuntime>();
        int _consumedNodeCount;

        // --- Survival timer ---
        float _survivalTimer;
        float _survivalTimeLimit;
        bool _survivalActive;

        public float SurvivalTimeRemaining => Mathf.Max(0f, _survivalTimeLimit - _survivalTimer);
        public float SurvivalTimeLimit => _survivalTimeLimit;
        public float SurvivalElapsed => _survivalTimer;
        public int CurrentWaveIndex => _currentWaveIndex;
        public int TotalWaves => _currentWaveData != null ? _currentWaveData.waves.Length : 0;
        public bool IsSurvivalActive => _survivalActive;
        public EnemyWaveData CurrentWaveData => _currentWaveData;
        public IReadOnlyList<RouteNodeRuntime> RouteNodes => _routeNodes;
        public int ConsumedNodeCount => _consumedNodeCount;

        // ────────────────────────────────────────
        //  Lifecycle
        // ────────────────────────────────────────

        void OnEnable()
        {
            GameEvents.OnLineClearWithCells += HandleLineClearWithCells;
            GameEvents.OnStageChanged += HandleStageChanged;
        }

        void OnDisable()
        {
            GameEvents.OnLineClearWithCells -= HandleLineClearWithCells;
            GameEvents.OnStageChanged -= HandleStageChanged;
        }

        void Update()
        {
            // 全敵共通パラメータを毎フレーム同期 (インスペクタの変更が即反映)
            CurrentMoveSpeedMultiplier = Mathf.Max(0f, enemyMoveSpeedMultiplier);
            CurrentUseDistanceBasedChaseSpeed = useDistanceBasedChaseSpeed;
            CurrentNearChaseSpeedMultiplier = Mathf.Max(0.01f, nearChaseSpeedMultiplier);
            CurrentFarChaseSpeedMultiplier = Mathf.Max(0.01f, farChaseSpeedMultiplier);
            CurrentDistanceChaseSpeedCurve = distanceChaseSpeedCurve != null
                ? distanceChaseSpeedCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            CurrentDistanceChaseTimeNormalizeMultiplier = preserveDistanceChaseArrivalTime
                ? CalculateDistanceChaseTimeNormalizeMultiplier()
                : 1f;
            CurrentKnockbackMultiplier = Mathf.Max(0f, enemyKnockbackMultiplier);
            CurrentKnockbackDamping    = Mathf.Max(0.01f, enemyKnockbackDamping);
            CurrentHoverHeightVariance = Mathf.Max(0f, enemyHoverHeightVariance);
            CurrentSortByY             = sortEnemiesByY;
            CurrentYSortScale          = Mathf.Max(1f, ySortScale);
            CurrentYSortBaseOrder      = ySortBaseOrder;

            // PlayerEffectState の倍率を毎フレーム同期 (ショップで購入したら即反映)
            CurrentPlayerSpeedMultiplier = PlayerEffectState.Instance != null
                ? Mathf.Max(0.01f, PlayerEffectState.Instance.PlayerSpeedMultiplier)
                : 1f;

            if (archRoadSystem == null) return;

            float radius = archRoadSystem.archRadius;
            Vector3 center = archRoadSystem.transform.position;
            float scroll = archRoadSystem.scrollSpeed;

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e == null) { _enemies.RemoveAt(i); continue; }

                e.UpdateArchSettings(radius, center, scroll);

                if (e.HasReachedPlayer(gameOverAngle))
                {
                    if (GameManager.Instance != null
                        && GameManager.Instance.currentState == GameState.Playing)
                    {
                        GameManager.Instance.OnEnemyReachedPlayer();
                    }
                }
            }

            CheckBulletCollisions();
            UpdateSurvivalTimer();
        }

        // ────────────────────────────────────────
        //  Survival Timer
        // ────────────────────────────────────────

        void UpdateSurvivalTimer()
        {
            if (!_survivalActive) return;
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing)
                return;

            _survivalTimer += Time.deltaTime;
            GameEvents.TriggerSurvivalTimerUpdate(_survivalTimer, _survivalTimeLimit);

            if (_routeNodes.Count > 0)
            {
                UpdateRouteProgress();
            }
            else if (_survivalTimer >= _survivalTimeLimit)
            {
                _survivalActive = false;
                StopWaves();
                ClearAllEnemies();
                GameEvents.TriggerWaveSurvivalClear();
            }
        }

        // ────────────────────────────────────────
        //  Events
        // ────────────────────────────────────────

        void HandleLineClearWithCells(int linesCleared, int cellsCleared, int comboCount)
        {
            if (cellsCleared <= 0 || _enemies.Count == 0) return;
            _lineClearMultiplier = ResolveLineClearMultiplier(linesCleared);

            // ショップの "弾数+" 効果を加算
            int bulletCountBonus = PlayerEffectState.Instance != null
                ? PlayerEffectState.Instance.BulletCountBonus
                : 0;
            int totalBullets = Mathf.Max(0, cellsCleared + bulletCountBonus);

            StartCoroutine(FireBulletBurst(totalBullets));
        }

        /// <summary>
        /// 同時消しライン数 → 攻撃倍率の解決。
        /// lineClearMultiplierTable[linesCleared - 1] を返す。末尾以上は末尾値で cap。
        /// </summary>
        public float ResolveLineClearMultiplier(int linesCleared)
        {
            if (linesCleared <= 0) return 0f;
            if (lineClearMultiplierTable == null || lineClearMultiplierTable.Count == 0)
                return Mathf.Max(1f, linesCleared);
            int idx = Mathf.Clamp(linesCleared - 1, 0, lineClearMultiplierTable.Count - 1);
            return Mathf.Max(0f, lineClearMultiplierTable[idx]);
        }

        /// <summary>
        /// 敵の残り距離に応じたチェイス速度倍率。
        /// normalizedDistance: 0 = プレイヤー付近 / 1 = 出現位置付近。
        /// </summary>
        public static float ResolveDistanceChaseSpeedMultiplier(float distanceAngle, float spawnDistance)
        {
            if (!CurrentUseDistanceBasedChaseSpeed) return 1f;
            if (spawnDistance <= 0.01f) return 1f;

            float normalizedDistance = Mathf.Clamp01(distanceAngle / spawnDistance);
            float baseMultiplier = EvaluateDistanceChaseBaseMultiplier(normalizedDistance);
            return Mathf.Max(0.01f, baseMultiplier * CurrentDistanceChaseTimeNormalizeMultiplier);
        }

        static float EvaluateDistanceChaseBaseMultiplier(float normalizedDistance)
        {
            float t = CurrentDistanceChaseSpeedCurve != null
                ? Mathf.Clamp01(CurrentDistanceChaseSpeedCurve.Evaluate(normalizedDistance))
                : normalizedDistance;

            return Mathf.Lerp(CurrentNearChaseSpeedMultiplier, CurrentFarChaseSpeedMultiplier, t);
        }

        static float CalculateDistanceChaseTimeNormalizeMultiplier()
        {
            if (!CurrentUseDistanceBasedChaseSpeed) return 1f;

            const int samples = 32;
            float inverseSpeedArea = 0f;
            float previous = 1f / Mathf.Max(0.01f, EvaluateDistanceChaseBaseMultiplier(0f));

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                float current = 1f / Mathf.Max(0.01f, EvaluateDistanceChaseBaseMultiplier(t));
                inverseSpeedArea += (previous + current) * 0.5f / samples;
                previous = current;
            }

            // 到達時間 = 従来時間 * inverseSpeedArea / normalize。
            // normalize を inverseSpeedArea にすると、総到達時間が従来と揃う。
            return Mathf.Max(0.01f, inverseSpeedArea);
        }

        void HandleStageChanged(int stageNumber)
        {
            StartWavesForStage(stageNumber);
        }

        // ────────────────────────────────────────
        //  Wave Management
        // ────────────────────────────────────────

        public void StartWavesForStage(int stageNumber)
        {
            StopWaves();
            ClearAllEnemies();

            int idx = Mathf.Clamp(stageNumber - 1, 0,
                stageWaves != null && stageWaves.Length > 0 ? stageWaves.Length - 1 : 0);

            _currentWaveData = (stageWaves != null && stageWaves.Length > 0 && stageWaves[idx] != null)
                ? stageWaves[idx]
                : EnemyWaveData.CreateDefault();

            _currentWaveIndex = 0;

            _survivalTimeLimit = ResolveSurvivalTime(stageNumber);
            _survivalTimer = 0f;
            _survivalActive = _survivalTimeLimit > 0f;
            BuildRouteTimeline();

            _wavesRunning = true;
            _waveCoroutine = StartCoroutine(RunWaves());
        }

        float ResolveSurvivalTime(int stageNumber)
        {
            int idx = stageNumber - 1;

            if (stageSurvivalTimes != null
                && idx >= 0
                && idx < stageSurvivalTimes.Length
                && stageSurvivalTimes[idx] > 0f)
            {
                return stageSurvivalTimes[idx];
            }

            return _currentWaveData != null ? _currentWaveData.survivalTime : 0f;
        }

        void StopWaves()
        {
            _wavesRunning = false;
            if (_waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }
        }

        // ────────────────────────────────────────
        //  Route Timeline (マス消化ベース)
        // ────────────────────────────────────────

        void BuildRouteTimeline()
        {
            _routeNodes.Clear();
            _consumedNodeCount = 0;

            if (_currentWaveData == null || _survivalTimeLimit <= 0f)
                return;

            RouteNodeConfig[] configs = _currentWaveData.routeNodes;
            if (configs == null || configs.Length == 0)
                return;

            for (int i = 0; i < configs.Length; i++)
            {
                var cfg = configs[i] ?? new RouteNodeConfig();
                _routeNodes.Add(new RouteNodeRuntime
                {
                    nodeIndex = i,
                    eventType = cfg.eventType,
                    maxCellIncrease = Mathf.Max(0, cfg.maxCellIncrease),
                    unlockAllShapes = cfg.unlockAllShapes,
                    randomShapeIncrease = Mathf.Max(0, cfg.randomShapeIncrease),
                    spawnEnemy = cfg.spawnEnemy
                });
            }
        }

        void UpdateRouteProgress()
        {
            if (_routeNodes.Count == 0 || _survivalTimeLimit <= 0f)
                return;

            int newConsumed = RouteTimelineMath.GetConsumedCount(
                _survivalTimer, _survivalTimeLimit, _routeNodes.Count);

            for (int i = _consumedNodeCount; i < newConsumed && i < _routeNodes.Count; i++)
                TriggerRouteNodeEvents(_routeNodes[i]);

            _consumedNodeCount = newConsumed;

            // 最終ノードまで消化した時点でステージクリア (= WaveSurvivalClear) を発火する。
            // ただし最終ノードが Shop で PauseSurvivalForShop() により _survivalActive=false
            // になっている場合は、ShopArrivalSequence → ShopFlowController.OpenShop の流れで
            // 次ステージに進めるので、ここでは発火しない (二重起動防止)。
            if (_consumedNodeCount >= _routeNodes.Count && _survivalActive)
            {
                _survivalActive = false;
                StopWaves();
                ClearAllEnemies();
                GameEvents.TriggerWaveSurvivalClear();
            }
        }

        void TriggerRouteNodeEvents(RouteNodeRuntime node)
        {
            if (node == null || node.eventTriggered)
                return;

            node.eventTriggered = true;

            switch (node.eventType)
            {
                case RouteEventType.Cake:
                {
                    var spawner = GameManager.Instance?.blockSpawner;
                    if (spawner == null) break;

                    // 「全ブロック解放」マスの場合
                    if (node.unlockAllShapes)
                    {
                        spawner.UnlockAllShapes();
                        break;
                    }

                    // 新フィールド優先: maxCellIncrease (CSV「ブロック増加 +N」に対応)
                    int delta = node.maxCellIncrease;
                    if (delta <= 0) delta = node.randomShapeIncrease; // 旧フィールド フォールバック
                    if (delta <= 0) delta = 1;

                    spawner.IncreaseMaxCells(delta);
                    break;
                }

                case RouteEventType.Boss:
                    if (node.spawnEnemy != null)
                        SpawnSpecialEnemy(node.spawnEnemy);
                    break;

                case RouteEventType.Shop:
                    // ショップ到来演出は ShopArrivalSequence が担当する。
                    // EnemySystem はサバイバルタイマー停止 + Wave 停止までを行い、
                    // 演出側からの BeginEnemyExitToShop() でフィールドの敵を整理する。
                    Debug.Log("[EnemySystem] Shop ルートノードを消費 → OnShopRouteNodeReached を発火");
                    PauseSurvivalForShop();
                    GameEvents.TriggerShopRouteNodeReached();
                    break;

                case RouteEventType.VendingMachine:
                    // 自販機到来演出は VendingMachineArrivalSequence が担当する。
                    // こちらはステージ途中 (= ステージ続行) なので Wave は停めず、
                    // サバイバルタイマーのカウントだけ一時的に止める。
                    // 到着 → Canvas オープン中は GamePauseService.Pause で全停止される。
                    Debug.Log("[EnemySystem] VendingMachine ルートノードを消費 → OnVendingMachineRouteNodeReached を発火");
                    PauseSurvivalTickOnly();
                    GameEvents.TriggerVendingMachineRouteNodeReached();
                    break;

                case RouteEventType.Clear:
                    // ゲーム全体のクリアマス。ショップのように通常ステージ進行へは戻らず、
                    // Wave / survival を止めてリザルト表示へ遷移する。
                    Debug.Log("[EnemySystem] Clear ルートノードを消費 → OnGameClearRouteNodeReached を発火");
                    PauseSurvivalForShop();
                    ClearAllEnemies();
                    GameEvents.TriggerGameClearRouteNodeReached();
                    break;
            }
        }

        /// <summary>
        /// ショップ演出中はサバイバルタイマーと Wave スポーンを止める。
        /// ステージ再開 / 次ステージ開始時に StartStage 経由で再初期化される。
        /// </summary>
        public void PauseSurvivalForShop()
        {
            _survivalActive = false;
            StopWaves();
        }

        /// <summary>
        /// サバイバルタイマーの「ティック (= 経過時間更新)」だけを止める。
        /// Wave スポーンや既存敵の動きには干渉しない。
        ///
        /// ・自販機演出中などステージ途中で時間経過を一時止めたいケース用。
        /// ・全体一時停止 (GamePauseService.Pause) と組み合わせて、
        ///   到着シーケンス開始 → 全停止 → Canvas → 再開、の流れを通すための土台。
        /// </summary>
        public void PauseSurvivalTickOnly()
        {
            _survivalActive = false;
            Debug.Log("[EnemySystem] PauseSurvivalTickOnly: survival tick OFF (waves は継続)");
        }

        /// <summary>
        /// PauseSurvivalTickOnly() で止めたサバイバルタイマーを再開する。
        /// _survivalTimeLimit が 0 だと開始しない。
        /// </summary>
        public void ResumeSurvivalTick()
        {
            if (_survivalTimeLimit > 0f)
            {
                _survivalActive = true;
                Debug.Log("[EnemySystem] ResumeSurvivalTick: survival tick ON");
            }
            else
            {
                Debug.Log("[EnemySystem] ResumeSurvivalTick: survivalTimeLimit=0 のため再開しない");
            }
        }

        /// <summary>
        /// ショップ到来演出からの呼び出し。
        /// 既存の敵を distanceAngle 増加方向に押し流して後方へ退場させ、
        /// duration 秒後に全消去する。
        /// </summary>
        public Coroutine BeginEnemyExitToShop(float exitSpeed, float duration, bool clearOnFinish = true)
        {
            return StartCoroutine(SlideEnemiesAway(exitSpeed, duration, clearOnFinish));
        }

        IEnumerator SlideEnemiesAway(float exitSpeed, float duration, bool clearOnFinish)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dt = Time.deltaTime;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    var e = _enemies[i];
                    if (e == null) continue;
                    e.distanceAngle += exitSpeed * dt;
                }
                yield return null;
            }

            if (clearOnFinish)
                ClearAllEnemies();
        }

        IEnumerator RunWaves()
        {
            if (_currentWaveData == null || _currentWaveData.waves == null)
                yield break;

            for (int w = 0; w < _currentWaveData.waves.Length; w++)
            {
                if (!_wavesRunning) yield break;

                var wave = _currentWaveData.waves[w];

                // Wave に絶対タイミングが指定されていればその時刻まで待機
                // (CSV「出現秒数」列の値が反映される)
                if (wave != null && wave.startTimeSeconds >= 0f)
                {
                    while (_wavesRunning && _survivalTimer < wave.startTimeSeconds)
                        yield return null;
                }

                if (!_wavesRunning) yield break;

                _currentWaveIndex = w;
                GameEvents.TriggerWaveStarted(w, _currentWaveData.waves.Length);

                if (wave != null && wave.enemies != null)
                {
                    for (int e = 0; e < wave.enemies.Length; e++)
                    {
                        if (!_wavesRunning) yield break;

                        var data = wave.enemies[e] != null
                            ? wave.enemies[e]
                            : EnemyData.CreateDefault(e);
                        SpawnEnemy(data);

                        if (e < wave.enemies.Length - 1)
                            yield return new WaitForSeconds(wave.spawnInterval);
                    }
                }

                // 次 Wave が絶対タイミング指定なら waitForSeconds は不要
                // (絶対タイミングの場合は次 Wave のループ頭で待機する)
                if (w < _currentWaveData.waves.Length - 1)
                {
                    var nextWave = _currentWaveData.waves[w + 1];
                    bool nextHasAbsolute = nextWave != null && nextWave.startTimeSeconds >= 0f;
                    if (!nextHasAbsolute)
                        yield return new WaitForSeconds(_currentWaveData.intervalBetweenWaves);
                }
            }

            _wavesRunning = false;
        }

        // ────────────────────────────────────────
        //  Spawn
        // ────────────────────────────────────────

        void SpawnEnemy(EnemyData data)
        {
            if (archRoadSystem == null) return;

            var obj = new GameObject($"Enemy_{data.name}");
            obj.transform.SetParent(transform);

            var ctrl = obj.AddComponent<EnemyController>();
            ctrl.Initialize(data,
                archRoadSystem.archRadius,
                archRoadSystem.transform.position,
                archRoadSystem.scrollSpeed);

            _enemies.Add(ctrl);
        }

        public void SpawnSpecialEnemy(EnemyData data)
        {
            if (data == null)
                return;

            SpawnEnemy(data);
        }

        // ────────────────────────────────────────
        //  Bullets
        // ────────────────────────────────────────

        IEnumerator FireBulletBurst(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnBullet();
                yield return new WaitForSeconds(bulletSpawnInterval);
            }
        }

        void SpawnBullet()
        {
            if (archRoadSystem == null) return;

            var obj = new GameObject("PlayerBullet");
            obj.transform.SetParent(transform);

            Vector3 archCenter = archRoadSystem.transform.position;
            float radius = archRoadSystem.archRadius;

            float startAngle = 0f;
            Vector3? overrideStartPos = null;

            if (bulletSpawnPoint != null)
            {
                overrideStartPos = bulletSpawnPoint.position;
                Vector3 local = bulletSpawnPoint.position - archCenter;
                startAngle = -Mathf.Atan2(local.x, local.y + radius) * Mathf.Rad2Deg;
                startAngle = Mathf.Max(startAngle, 0f);
            }

            // ショップ効果: BulletSize / BulletSpeed / Penetration を適用
            float sizeMul  = 1f;
            float speedMul = 1f;
            int   penetration = 0;
            if (PlayerEffectState.Instance != null)
            {
                sizeMul     = Mathf.Max(0.01f, PlayerEffectState.Instance.BulletSizeMultiplier);
                speedMul    = Mathf.Max(0.01f, PlayerEffectState.Instance.BulletSpeedMultiplier);
                penetration = PlayerEffectState.Instance.PenetrationBonus;
            }

            var bullet = obj.AddComponent<PlayerBullet>();
            bullet.Initialize(
                bulletSpeed * speedMul,
                bulletBounceHeight,
                bulletBounceFrequency,
                radius,
                archCenter,
                startAngle,
                overrideStartPos,
                bulletSprite,
                bulletColor,
                bulletScale * sizeMul,
                bulletHitAngleOffset,
                bulletHitAngleRadius * sizeMul,
                bulletViewportExitMargin);
            bullet.SetPenetration(penetration);

            _activeBullets.Add(bullet);
        }

        // ────────────────────────────────────────
        //  Collision (sweep test)
        // ────────────────────────────────────────

        void CheckBulletCollisions()
        {
            // ショップ "弾でかくなる" 倍率はダメージにのみ反映する (ノックバックには掛けない)
            float bulletSizeDamageMul = PlayerEffectState.Instance != null
                ? Mathf.Max(0.01f, PlayerEffectState.Instance.BulletSizeMultiplier)
                : 1f;
            float playerX = GetPlayerReferenceX();

            for (int b = _activeBullets.Count - 1; b >= 0; b--)
            {
                var bullet = _activeBullets[b];
                if (bullet == null || !bullet.IsAlive)
                {
                    _activeBullets.RemoveAt(b);
                    continue;
                }

                bool destroyed = false;

                _bulletHitCandidates.Clear();

                // まず「このフレームで当たり判定に入っている敵」を全部集める。
                // その後、プレイヤーの X 座標に近い順に処理することで、
                // 判定範囲が重なったときも手前の敵から当たるようにする。
                for (int e = 0; e < _enemies.Count; e++)
                {
                    var enemy = _enemies[e];
                    if (enemy == null) continue;

                    int enemyId = enemy.GetInstanceID();
                    if (bullet.HasHitEnemy(enemyId)) continue;

                    float combinedRadius = bullet.HitAngleRadius + enemy.HitAngleRadius;
                    float enemyDist = enemy.HitAngleCenter;

                    if (bullet.PrevHitAngle - combinedRadius <= enemyDist
                        && bullet.HitAngle + combinedRadius >= enemyDist)
                    {
                        Vector3 hitPosition = enemy.HitPosition;
                        _bulletHitCandidates.Add(new BulletHitCandidate
                        {
                            enemy = enemy,
                            enemyId = enemyId,
                            hitPosition = hitPosition,
                            distanceToPlayerX = Mathf.Abs(hitPosition.x - playerX)
                        });
                    }
                }

                if (_bulletHitCandidates.Count > 0)
                {
                    _bulletHitCandidates.Sort((a, b2) => a.distanceToPlayerX.CompareTo(b2.distanceToPlayerX));

                    // 同一弾が同フレーム内に複数の敵に当たり得る (貫通)
                    // → 手前順にヒット処理し、ヒットごとに penetration 残数を確認
                    for (int i = 0; i < _bulletHitCandidates.Count; i++)
                    {
                        var candidate = _bulletHitCandidates[i];
                        var enemy = candidate.enemy;
                        if (enemy == null) continue;

                        enemy.TakeSingleHit(_lineClearMultiplier, bulletSizeDamageMul);

                        // 残り貫通数があれば生き残る、なければ消滅
                        bool survives = bullet.ApplyHitAndCheckSurvive(candidate.enemyId, candidate.hitPosition);
                        if (!survives)
                        {
                            bullet.SnapAndKill(candidate.hitPosition);
                            _activeBullets.RemoveAt(b);
                            destroyed = true;
                            break;
                        }
                    }
                }

                if (!destroyed && bullet.CurrentAngle > 180f)
                {
                    bullet.Kill();
                    _activeBullets.RemoveAt(b);
                }
            }
        }

        float GetPlayerReferenceX()
        {
            if (bulletSpawnPoint != null)
                return bulletSpawnPoint.position.x;

            if (archRoadSystem != null)
                return archRoadSystem.transform.position.x;

            return 0f;
        }

        // ────────────────────────────────────────
        //  Cleanup
        // ────────────────────────────────────────

        public void ClearAllEnemies()
        {
            foreach (var e in _enemies)
                if (e != null) Destroy(e.gameObject);
            _enemies.Clear();
            EnemyController.ClearAllHitEffects();

            foreach (var b in _activeBullets)
                if (b != null) Destroy(b.gameObject);
            _activeBullets.Clear();
        }

        public List<EnemyController> GetActiveEnemies() => _enemies;

        public static void SetEventVisualOffset(Vector3 offset)
        {
            CurrentEventVisualOffset = offset;
        }
    }
}
