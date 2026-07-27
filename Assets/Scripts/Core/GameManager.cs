using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public BoardManager boardManager;
        public BlockSpawner blockSpawner;
        public TurnManager turnManager;
        public StageManager stageManager;
        public LineClearSystem lineClearSystem;
        public ComboSystem comboSystem;
        public ItemSystem itemSystem;
        public ChaseSystem chaseSystem;
        public RoguelikeSystem roguelikeSystem;
        public SpaceshipBuilder spaceshipBuilder;
        public UIManager uiManager;
        public EnemySystem enemySystem;
        public SuperChatSpawner superChatSpawner;

        [Header("State")]
        public GameState currentState = GameState.Title;

        [Header("Debug")]
        [Tooltip("ON: ゲーム開始時に stage 1 ではなく debugStartStage から開始する。\nビルド確認や途中ステージの調整用。")]
        public bool startFromDebugStage = false;

        [Tooltip("startFromDebugStage が ON のときの開始ステージ番号。")]
        [Min(1)]
        public int debugStartStage = 1;

        public int score;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
        }

        void OnEnable()
        {
            GameEvents.OnWaveSurvivalClear += HandleWaveSurvivalClear;
            GameEvents.OnGameClearRouteNodeReached += HandleGameClearRouteNodeReached;
        }

        void OnDisable()
        {
            GameEvents.OnWaveSurvivalClear -= HandleWaveSurvivalClear;
            GameEvents.OnGameClearRouteNodeReached -= HandleGameClearRouteNodeReached;
        }

        void HandleWaveSurvivalClear()
        {
            if (currentState != GameState.Playing) return;
            OnStageClear();
        }

        void HandleGameClearRouteNodeReached()
        {
            if (currentState != GameState.Playing) return;
            OnGameClear();
        }

        bool gameStarted;

        void Start()
        {
            if (!gameStarted && stageManager != null)
            {
                gameStarted = true;
                StartGame();
            }
        }

        void LateUpdate()
        {
            if (!gameStarted && stageManager != null)
            {
                gameStarted = true;
                StartGame();
            }
        }

        public void StartGame()
        {
            StartGameAtStage(ResolveStartStage());
        }

        public void StartGameAtStage(int stageNumber)
        {
            stageNumber = Mathf.Max(1, stageNumber);

            EnemySystem.ResetLoopProgress();
            score = 0;
            GameEvents.TriggerScoreChanged(score);
            comboSystem?.ResetCombo();
            ChangeState(GameState.Playing);
            SoundManager.Play(SoundCue.GameStart);
            SoundManager.PlayBgm(BgmCue.Game);

            if (startFromDebugStage)
                LogDebugStageReadiness(stageNumber);

            stageManager.StartStage(stageNumber);
        }

        void LogDebugStageReadiness(int stageNumber)
        {
            int idx = stageNumber - 1;

            bool hasStageData = stageManager != null
                && stageManager.stageDataAssets != null
                && idx >= 0
                && idx < stageManager.stageDataAssets.Length
                && stageManager.stageDataAssets[idx] != null;

            bool hasWaveData = enemySystem != null
                && enemySystem.stageWaves != null
                && idx >= 0
                && idx < enemySystem.stageWaves.Length
                && enemySystem.stageWaves[idx] != null;

            bool hasSurvivalTime = enemySystem != null
                && enemySystem.stageSurvivalTimes != null
                && idx >= 0
                && idx < enemySystem.stageSurvivalTimes.Length
                && enemySystem.stageSurvivalTimes[idx] > 0f;

            Debug.Log($"[GameManager] Debug start: stage {stageNumber} | StageData={hasStageData} / EnemyWaveData={hasWaveData} / SurvivalTime={hasSurvivalTime}");

            if (!hasStageData)
                Debug.LogWarning($"[GameManager] stage {stageNumber} の StageData が StageManager.stageDataAssets に無いため、ルール (ターン / ブロック増加 / ライン目標) が既定値になります。");
            if (!hasWaveData)
                Debug.LogWarning($"[GameManager] stage {stageNumber} の EnemyWaveData が EnemySystem.stageWaves に無いため、敵出現とミニマップが既定値になります。");
            if (!hasSurvivalTime)
                Debug.LogWarning($"[GameManager] stage {stageNumber} の stageSurvivalTimes が 0 / 未設定です。EnemyWaveData.survivalTime にフォールバックします。");
        }

        int ResolveStartStage()
        {
            if (!startFromDebugStage) return 1;

            // EnemySystem 側 (Wave / ミニマップ) と StageManager 側 (ルール) のどちらか長い方を上限にする。
            // どちらも未設定なら無制限に通す (Clamp しない)。
            int dataStageCount = stageManager != null && stageManager.stageDataAssets != null
                ? stageManager.stageDataAssets.Length
                : 0;
            int totalStages = stageManager != null ? stageManager.totalStages : 0;
            int waveStageCount = enemySystem != null && enemySystem.stageWaves != null
                ? enemySystem.stageWaves.Length
                : 0;

            int maxStage = Mathf.Max(dataStageCount, totalStages, waveStageCount);
            if (maxStage <= 0) return Mathf.Max(1, debugStartStage);

            return Mathf.Clamp(debugStartStage, 1, maxStage);
        }

        [ContextMenu("Debug/Restart From Debug Stage")]
        void RestartFromDebugStage()
        {
            if (stageManager == null) return;

            gameStarted = true;
            StartGameAtStage(ResolveStartStage());
        }

        public void ChangeState(GameState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    if (!GamePauseService.IsPaused)
                        Time.timeScale = 1f;
                    break;
                case GameState.LineClearing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
                case GameState.StageTransition:
                    Time.timeScale = 0f;
                    break;
                case GameState.SpaceshipBuild:
                    Time.timeScale = 0f;
                    break;
            }
        }

        public void AddScore(int amount)
        {
            score += amount;
            GameEvents.TriggerScoreChanged(score);
        }

        public void OnBlockPlacedCallback()
        {
            turnManager.ConsumeTurn();
            itemSystem.TickItemTimers();

            var clearResult = lineClearSystem.CheckAndClearLines();
            if (clearResult.linesCleared > 0)
            {
                ChangeState(GameState.LineClearing);
                lineClearSystem.ExecuteClearWithCallback(clearResult, () => OnLineClearComplete(clearResult));
            }
            else
            {
                comboSystem.ResetCombo();
                PostBlockPlacedChecks();
            }
        }

        void OnLineClearComplete(LineClearResult clearResult)
        {
            int comboCount = comboSystem.ProcessCombo(clearResult);
            int turnRecovery = turnManager.CalculateTurnRecovery(clearResult.linesCleared, comboCount);
            turnManager.AddTurns(turnRecovery);

            int lineScore = clearResult.linesCleared * 100 * (1 + comboCount);
            AddScore(lineScore);

            var collectedItems = itemSystem.CollectItemsFromClearedCells(clearResult.clearedCells);
            foreach (var item in collectedItems)
            {
                GameEvents.TriggerItemCollected(item);
            }

            GameEvents.TriggerLineClear(clearResult.linesCleared, comboCount);
            GameEvents.TriggerLineClearWithCells(clearResult.linesCleared, clearResult.clearedCells.Count, comboCount);

            itemSystem.TrySpawnRandomItem(clearResult.linesCleared);

            stageManager.AddLinesCleared(clearResult.linesCleared);

            if (currentState == GameState.StageTransition || currentState == GameState.SpaceshipBuild)
                return;

            ChangeState(GameState.Playing);
            PostBlockPlacedChecks();
        }

        void PostBlockPlacedChecks()
        {
            if (turnManager.remainingTurns <= 0)
            {
                TriggerGameOver();
                return;
            }

            if (!blockSpawner.HasAvailableBlocks())
            {
                blockSpawner.SpawnNewSet();
            }

            if (!boardManager.CanPlaceAnyBlock(blockSpawner.GetCurrentBlocks()))
            {
                TriggerGameOver();
            }
        }

        void TriggerGameOver()
        {
            ChangeState(GameState.GameOver);
            GameEvents.TriggerGameOver(GameOverType.PuzzleStuck);
        }

        public void OnStageClear()
        {
            EnemyController.ClearAllHitEffects();

            if (stageManager.currentStageNumber >= 5)
            {
                var parts = itemSystem.GetCollectedSpaceshipParts();
                spaceshipBuilder.Initialize(parts);
                ChangeState(GameState.SpaceshipBuild);
                GameEvents.TriggerSpaceshipBuild(parts);
            }
            else
            {
                ChangeState(GameState.StageTransition);
                GameEvents.TriggerStageClear();
            }
        }

        public void OnGameClear()
        {
            EnemyController.ClearAllHitEffects();
            ChangeState(GameState.Ending);
            GameEvents.TriggerGameClear();
        }

        /// <summary>
        /// 周回開始時にラン内パラメータを初期状態へ戻す。
        /// 累積カロリー (CalorieDisplay) と所持金 (PlayerWallet) には触れない。
        /// </summary>
        public void ResetForNewLoopPreservingKcalAndMoney()
        {
            score = 0;
            GameEvents.TriggerScoreChanged(score);

            boardManager?.ClearBoard();
            if (blockSpawner != null)
            {
                blockSpawner.ClearCurrentPieces();
                blockSpawner.ClearCellCountWeights();
                blockSpawner.defaultWeightForUnlistedCellCount = 1f;
            }
            itemSystem?.ClearAll();
            comboSystem?.ResetCombo();
            roguelikeSystem?.Reset();
            EnemySystem.SetEventVisualOffset(Vector3.zero);

            var playerEffects = PlayerEffectState.Instance;
            if (playerEffects == null)
                playerEffects = FindObjectOfType<PlayerEffectState>();
            playerEffects?.ResetAll();

            GamePauseService.ResetAll();
        }

        public void ProceedToNextStage(PerkType selectedPerk)
        {
            roguelikeSystem.ApplyPerk(selectedPerk);
            int nextStage = stageManager.currentStageNumber + 1;
            ChangeState(GameState.Playing);
            stageManager.StartStage(nextStage);
        }

        /// <summary>
        /// パーク選択無しで次ステージへ進める。ショップ購入完了時など、
        /// パーク 3 択に依存しないフローから呼ばれる想定。
        /// </summary>
        public void ProceedToNextStage()
        {
            int nextStage = stageManager.currentStageNumber + 1;
            ChangeState(GameState.Playing);
            stageManager.StartStage(nextStage);
        }

        public void OnEnemyReachedPlayer()
        {
            if (currentState != GameState.Playing) return;
            ChangeState(GameState.GameOver);
            GameEvents.TriggerGameOver(GameOverType.EnemyCapture);
        }

        /// <summary>
        /// リワード広告視聴後、ゲームオーバー地点から再開する。
        /// 敵のHP・ステージ進行・kcal・所持金は維持し、盤面だけ空にする。
        /// </summary>
        public bool ContinueAfterReward(
            float minimumEnemyDistanceAngle = 40f,
            float additionalEnemyDistanceAngle = 25f,
            int minimumRemainingTurns = 3)
        {
            if (currentState != GameState.GameOver)
                return false;

            boardManager?.ClearBoard();
            itemSystem?.ClearBoardItemsForContinue();
            comboSystem?.ResetCombo();

            if (blockSpawner != null)
                blockSpawner.SpawnNewSet();

            if (turnManager != null && turnManager.remainingTurns < minimumRemainingTurns)
                turnManager.AddTurns(minimumRemainingTurns - turnManager.remainingTurns);

            enemySystem?.PrepareContinue(
                Mathf.Max(0f, minimumEnemyDistanceAngle),
                Mathf.Max(0f, additionalEnemyDistanceAngle));

            ChangeState(GameState.Playing);
            return true;
        }

        public void RestartGame()
        {
            gameStarted = false;
            GameEvents.ClearAll();
            GamePauseService.ResetAll();
            EnemySystem.ResetLoopProgress();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
