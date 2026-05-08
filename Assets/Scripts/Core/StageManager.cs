using UnityEngine;

namespace BlockBlastGame
{
    public class StageManager : MonoBehaviour
    {
        [Header("Stage Settings")]
        public int currentStageNumber;
        public int totalStages = 5;

        [Header("Custom Stage Data (optional)")]
        public StageData[] stageDataAssets;

        [Header("Runtime")]
        public StageData currentStageData;
        public bool stageActive;
        public int linesCleared;
        public int linesToClear;

        public void StartStage(int stageNumber)
        {
            currentStageNumber = stageNumber;

            if (stageDataAssets != null && stageNumber - 1 < stageDataAssets.Length && stageDataAssets[stageNumber - 1] != null)
            {
                currentStageData = stageDataAssets[stageNumber - 1];
            }
            else
            {
                currentStageData = StageData.CreateDefault(stageNumber);
            }

            linesCleared = 0;
            linesToClear = currentStageData.linesToClear;
            stageActive = true;

            var gm = GameManager.Instance;
            gm.turnManager.baseTurnsPerLineClear = currentStageData.turnsPerLineClear;
            gm.turnManager.InitializeForStage(currentStageData.initialTurns);

            // CSV「ブロック増加」のステージ初期値を反映。
            // 道中の追加 (+1 など) は EnemySystem の Cake/Route ノードで処理される。
            gm.blockSpawner.SetMaxCells(currentStageData.initialMaxBlockCells);

            // セル数ごとの出現確率重みをステージ単位で反映。
            // ・blockCellCountWeights が指定されていればそれで上書き
            // ・空 + resetWeightsWhenStageWeightsEmpty = true ならクリア
            // ・空 + フラグ false なら BlockSpawner 側の現在値を維持 (前ステージの重みを引き継ぐ)
            if (currentStageData.blockCellCountWeights != null
                && currentStageData.blockCellCountWeights.Count > 0)
            {
                gm.blockSpawner.SetCellCountWeights(currentStageData.blockCellCountWeights);
                gm.blockSpawner.defaultWeightForUnlistedCellCount =
                    currentStageData.blockDefaultWeightForUnlistedCellCount;
            }
            else if (currentStageData.resetWeightsWhenStageWeightsEmpty)
            {
                gm.blockSpawner.ClearCellCountWeights();
            }

            gm.blockSpawner.SpawnNewSet();
            gm.itemSystem.itemsPerStage = currentStageData.itemCount;
            gm.itemSystem.PlaceItemsForStage(stageNumber);

            GameEvents.TriggerStageChanged(stageNumber);
            GameEvents.TriggerLineProgress(linesCleared, linesToClear);
        }

        public void AddLinesCleared(int count)
        {
            if (!stageActive) return;
            linesCleared += count;
            GameEvents.TriggerLineProgress(linesCleared, linesToClear);

            if (linesCleared >= linesToClear)
            {
                stageActive = false;
                GameManager.Instance.OnStageClear();
            }
        }

        public int GetRemainingLines()
        {
            return Mathf.Max(0, linesToClear - linesCleared);
        }

        public bool IsLastStage()
        {
            return currentStageNumber >= totalStages;
        }
    }
}
