using UnityEngine;

namespace BlockBlastGame
{
    public class ComboSystem : MonoBehaviour
    {
        [Header("Combo Settings")]
        public int currentComboStreak;
        public float sameColorBonusMultiplier = 1.5f;

        public int ProcessCombo(LineClearResult result)
        {
            if (result.linesCleared <= 0)
            {
                ResetCombo();
                return 0;
            }

            currentComboStreak++;

            int comboCount = 0;

            // Multi-line bonus: clearing 2+ lines at once
            if (result.linesCleared >= 2)
                comboCount += result.linesCleared - 1;

            // Consecutive clear streak bonus
            if (currentComboStreak >= 2)
                comboCount += currentComboStreak - 1;

            // Same-color line bonus
            if (result.hasSameColorLine)
                comboCount += 1;

            return comboCount;
        }

        public void ResetCombo()
        {
            currentComboStreak = 0;
        }

        public float GetComboMultiplier(int comboCount)
        {
            return 1f + comboCount * 0.5f;
        }
    }
}
