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
                    randomShapeIncrease = Mathf.Max(1, cfg.randomShapeIncrease),
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

            if (_consumedNodeCount >= _routeNodes.Count)
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
                    GameManager.Instance?.blockSpawner?.IncreaseAvailableShapeCount(node.randomShapeIncrease);
                    break;

                case RouteEventType.Boss:
                    if (node.spawnEnemy != null)
                        SpawnSpecialEnemy(node.spawnEnemy);
                    break;
            }
        }

        IEnumerator RunWaves()
        {
            if (_currentWaveData == null || _currentWaveData.waves == null)
                yield break;

            for (int w = 0; w < _currentWaveData.waves.Length; w++)
            {
                if (!_wavesRunning) yield break;

                _currentWaveIndex = w;
                var wave = _currentWaveData.waves[w];
                GameEvents.TriggerWaveStarted(w, _currentWaveData.waves.Length);

                if (wave.enemies != null)
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

                if (w < _currentWaveData.waves.Length - 1)
                    yield return new WaitForSeconds(_currentWaveData.intervalBetweenWaves);
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
