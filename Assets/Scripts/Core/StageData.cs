using UnityEngine;

namespace BlockBlastGame
{
    [CreateAssetMenu(fileName = "NewStageData", menuName = "BlockBlast/Stage Data")]
    public class StageData : ScriptableObject
    {
        public int stageNumber;

        [Tooltip("Lines to clear to complete this stage")]
        public int linesToClear = 10;

        [Tooltip("Starting turns for this stage (ignored if TurnManager.initialTurns > 0)")]
        public int initialTurns = 20;

        [Tooltip("Base turns recovered per single line clear")]
        public int turnsPerLineClear = 3;

        [Tooltip("Block difficulty multiplier (higher = harder shapes)")]
        [Range(0.1f, 2f)]
        public float difficultyMultiplier = 1f;

        [Tooltip("Number of items available this stage")]
        public int itemCount = 3;

        public static StageData CreateDefault(int stageNum)
        {
            var data = CreateInstance<StageData>();
            data.stageNumber = stageNum;

            switch (stageNum)
            {
                case 1:
                    data.linesToClear = 8;
                    data.initialTurns = 25;
                    data.turnsPerLineClear = 3;
                    data.difficultyMultiplier = 0.6f;
                    data.itemCount = 2;
                    break;
                case 2:
                    data.linesToClear = 10;
                    data.initialTurns = 22;
                    data.turnsPerLineClear = 3;
                    data.difficultyMultiplier = 0.7f;
                    data.itemCount = 3;
                    break;
                case 3:
                    data.linesToClear = 12;
                    data.initialTurns = 20;
                    data.turnsPerLineClear = 2;
                    data.difficultyMultiplier = 0.8f;
                    data.itemCount = 3;
                    break;
                case 4:
                    data.linesToClear = 15;
                    data.initialTurns = 18;
                    data.turnsPerLineClear = 2;
                    data.difficultyMultiplier = 0.9f;
                    data.itemCount = 4;
                    break;
                case 5:
                    data.linesToClear = 18;
                    data.initialTurns = 15;
                    data.turnsPerLineClear = 2;
                    data.difficultyMultiplier = 1f;
                    data.itemCount = 5;
                    break;
                default:
                    data.linesToClear = 10;
                    data.initialTurns = 20;
                    data.turnsPerLineClear = 3;
                    data.difficultyMultiplier = 1f;
                    data.itemCount = 3;
                    break;
            }

            return data;
        }
    }
}
