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
        }

        void OnDisable()
        {
            GameEvents.OnWaveSurvivalClear -= HandleWaveSurvivalClear;
        }

        void HandleWaveSurvivalClear()
        {
            if (currentState != GameState.Playing) return;
            OnStageClear();
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
            score = 0;
            GameEvents.TriggerScoreChanged(score);
            comboSystem?.ResetCombo();
            ChangeState(GameState.Playing);
            stageManager.StartStage(1);
        }

        public void ChangeState(GameState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case GameState.Playing:
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

        public void ProceedToNextStage(PerkType selectedPerk)
        {
            roguelikeSystem.ApplyPerk(selectedPerk);
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

        public void RestartGame()
        {
            gameStarted = false;
            GameEvents.ClearAll();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
