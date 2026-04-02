using UnityEngine;

namespace BlockBlastGame
{
    public class TurnManager : MonoBehaviour
    {
        [Header("Turn Settings")]
        [Tooltip("Initial turn count. 0 = use StageData default")]
        public int initialTurns = 0;

        [Tooltip("Current remaining turns (runtime)")]
        public int remainingTurns = 20;

        [Tooltip("Base turns recovered per single line clear")]
        public int baseTurnsPerLineClear = 3;

        [Header("Recovery Balance")]
        [Tooltip("Multiplier for simultaneous multi-line clears")]
        [Range(1f, 5f)]
        public float simultaneousMultiplier = 1.5f;

        [Tooltip("Bonus per combo streak level")]
        public int comboStreakBonus = 1;

        [Header("Perk Modifiers")]
        public int turnRecoveryBonus;
        public int startingTurnsBonus;

        public void InitializeForStage(int stageDefaultTurns)
        {
            int baseTurns = initialTurns > 0 ? initialTurns : stageDefaultTurns;
            remainingTurns = baseTurns + startingTurnsBonus;
            GameEvents.TriggerTurnChanged(remainingTurns);
            Debug.Log($"[Turn] Stage start: {remainingTurns} turns (base:{baseTurns}, bonus:{startingTurnsBonus})");
        }

        public void ConsumeTurn()
        {
            remainingTurns--;
            if (remainingTurns < 0) remainingTurns = 0;
            GameEvents.TriggerTurnChanged(remainingTurns);
        }

        public int CalculateTurnRecovery(int linesCleared, int comboCount)
        {
            int triangular = linesCleared * (linesCleared + 1) / 2;
            float multi = linesCleared >= 2 ? simultaneousMultiplier : 1f;
            int simultaneousBase = Mathf.RoundToInt(triangular * multi);
            int comboBonus = comboCount * comboStreakBonus;
            int recovery = simultaneousBase + comboBonus + turnRecoveryBonus;

            Debug.Log($"[Turn] +{recovery} (lines:{linesCleared} tri:{triangular}*{multi:F1}={simultaneousBase} combo:{comboBonus} perk:{turnRecoveryBonus})");
            return recovery;
        }

        public void AddTurns(int amount)
        {
            remainingTurns += amount;
            GameEvents.TriggerTurnChanged(remainingTurns);
            Debug.Log($"[Turn] +{amount} -> remaining: {remainingTurns}");
        }

        public float GetUrgencyLevel()
        {
            if (remainingTurns <= 3) return 1f;
            if (remainingTurns <= 7) return 0.6f;
            if (remainingTurns <= 12) return 0.3f;
            return 0f;
        }
    }
}
