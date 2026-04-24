using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public static class GameEvents
    {
        public static event Action<int> OnTurnChanged;
        public static event Action<int, int> OnLineClear;       // linesCleared, comboCount
        public static event Action<int, int, int> OnLineClearWithCells; // linesCleared, cellsCleared, comboCount
        public static event Action<int, int> OnLineProgress; // clearedSoFar, targetLines
        public static event Action<ItemData> OnItemCollected;
        public static event Action<GameOverType> OnGameOver;
        public static event Action<int> OnStageChanged;
        public static event Action OnBlockPlaced;
        public static event Action<int> OnScoreChanged;
        public static event Action OnStageClear;
        public static event Action<List<ItemData>> OnSpaceshipBuild;

        // Calorie events
        public static event Action<int> OnCalorieChanged; // totalCalories

        // Wave events
        public static event Action<int, int> OnWaveStarted;            // waveIndex, totalWaves
        public static event Action<float, float> OnSurvivalTimerUpdate; // elapsed, limit
        public static event Action OnWaveSurvivalClear;
        public static event Action<Vector3, int> OnEnemyDefeated;       // 敵の位置, ボーナス金額

        public static void TriggerTurnChanged(int remainingTurns) => OnTurnChanged?.Invoke(remainingTurns);
        public static void TriggerLineClear(int linesCleared, int comboCount) => OnLineClear?.Invoke(linesCleared, comboCount);
        public static void TriggerLineClearWithCells(int linesCleared, int cellsCleared, int comboCount) => OnLineClearWithCells?.Invoke(linesCleared, cellsCleared, comboCount);
        public static void TriggerLineProgress(int cleared, int target) => OnLineProgress?.Invoke(cleared, target);
        public static void TriggerItemCollected(ItemData item) => OnItemCollected?.Invoke(item);
        public static void TriggerGameOver(GameOverType type) => OnGameOver?.Invoke(type);
        public static void TriggerStageChanged(int stageNumber) => OnStageChanged?.Invoke(stageNumber);
        public static void TriggerBlockPlaced() => OnBlockPlaced?.Invoke();
        public static void TriggerScoreChanged(int score) => OnScoreChanged?.Invoke(score);
        public static void TriggerStageClear() => OnStageClear?.Invoke();
        public static void TriggerSpaceshipBuild(List<ItemData> parts) => OnSpaceshipBuild?.Invoke(parts);

        public static void TriggerCalorieChanged(int totalCalories) => OnCalorieChanged?.Invoke(totalCalories);

        public static void TriggerWaveStarted(int waveIndex, int totalWaves) => OnWaveStarted?.Invoke(waveIndex, totalWaves);
        public static void TriggerSurvivalTimerUpdate(float elapsed, float limit) => OnSurvivalTimerUpdate?.Invoke(elapsed, limit);
        public static void TriggerWaveSurvivalClear() => OnWaveSurvivalClear?.Invoke();
        public static void TriggerEnemyDefeated(Vector3 worldPosition, int bonusAmount) => OnEnemyDefeated?.Invoke(worldPosition, bonusAmount);

        public static void ClearAll()
        {
            OnTurnChanged = null;
            OnLineClear = null;
            OnLineClearWithCells = null;
            OnLineProgress = null;
            OnItemCollected = null;
            OnGameOver = null;
            OnStageChanged = null;
            OnBlockPlaced = null;
            OnScoreChanged = null;
            OnStageClear = null;
            OnSpaceshipBuild = null;
            OnCalorieChanged = null;
            OnWaveStarted = null;
            OnSurvivalTimerUpdate = null;
            OnWaveSurvivalClear = null;
            OnEnemyDefeated = null;
        }
    }
}
