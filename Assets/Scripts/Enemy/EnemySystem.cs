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

        readonly List<EnemyController> _enemies = new List<EnemyController>();
        readonly List<PlayerBullet> _activeBullets = new List<PlayerBullet>();

        float _knockbackMultiplier = 1f;

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
            CurrentKnockbackMultiplier = Mathf.Max(0f, enemyKnockbackMultiplier);
            CurrentKnockbackDamping    = Mathf.Max(0.01f, enemyKnockbackDamping);
            CurrentHoverHeightVariance = Mathf.Max(0f, enemyHoverHeightVariance);
            CurrentSortByY             = sortEnemiesByY;
            CurrentYSortScale          = Mathf.Max(1f, ySortScale);
            CurrentYSortBaseOrder      = ySortBaseOrder;

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
            _knockbackMultiplier = Mathf.Max(1f, linesCleared);
            StartCoroutine(FireBulletBurst(cellsCleared));
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

            var bullet = obj.AddComponent<PlayerBullet>();
            bullet.Initialize(
                bulletSpeed,
                bulletBounceHeight,
                bulletBounceFrequency,
                radius,
                archCenter,
                startAngle,
                overrideStartPos,
                bulletSprite,
                bulletColor,
                bulletScale,
                bulletHitAngleOffset,
                bulletHitAngleRadius,
                bulletViewportExitMargin);

            _activeBullets.Add(bullet);
        }

        // ────────────────────────────────────────
        //  Collision (sweep test)
        // ────────────────────────────────────────

        void CheckBulletCollisions()
        {
            for (int b = _activeBullets.Count - 1; b >= 0; b--)
            {
                var bullet = _activeBullets[b];
                if (bullet == null || !bullet.IsAlive)
                {
                    _activeBullets.RemoveAt(b);
                    continue;
                }

                bool hit = false;
                for (int e = 0; e < _enemies.Count; e++)
                {
                    var enemy = _enemies[e];
                    if (enemy == null) continue;

                    float combinedRadius = bullet.HitAngleRadius + enemy.HitAngleRadius;
                    float enemyDist = enemy.HitAngleCenter;

                    if (bullet.PrevHitAngle - combinedRadius <= enemyDist
                        && bullet.HitAngle + combinedRadius >= enemyDist)
                    {
                        enemy.TakeSingleHit(_knockbackMultiplier);
                        bullet.SnapAndKill(enemy.HitPosition);
                        _activeBullets.RemoveAt(b);
                        hit = true;
                        break;
                    }
                }

                if (!hit && bullet.CurrentAngle > 180f)
                {
                    bullet.Kill();
                    _activeBullets.RemoveAt(b);
                }
            }
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
    }
}
