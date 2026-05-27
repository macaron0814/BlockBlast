using System.Linq;
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

        [Tooltip("ON: stageDataAssets が空 / 不足のとき Resources.LoadAll<StageData>(autoLoadResourcesPath) で自動補完する。")]
        public bool autoLoadStageDataAssets = true;

        [Tooltip("Resources フォルダ配下から StageData をロードするときの相対パス。\n空文字なら Resources 直下を全検索する。\n例: \"Stages\" → Assets/Resources/Stages/Stage1.asset 等")]
        public string autoLoadResourcesPath = "Stages";

        [Header("Runtime")]
        public StageData currentStageData;
        public bool stageActive;
        public int linesCleared;
        public int linesToClear;

        bool _autoLoadAttempted;

        public void StartStage(int stageNumber)
        {
            currentStageNumber = stageNumber;

            EnsureStageDataAssetsPopulated();

            int idx = stageNumber - 1;
            if (stageDataAssets != null && idx >= 0 && idx < stageDataAssets.Length && stageDataAssets[idx] != null)
            {
                currentStageData = stageDataAssets[idx];
            }
            else
            {
                currentStageData = StageData.CreateDefault(stageNumber);
                Debug.LogWarning($"[StageManager] stage {stageNumber} の StageData アセットが見つからないため、" +
                                 "コード既定値で開始します。 StageManager.stageDataAssets に Stage1〜N の StageData をアサインしてください。");
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

        // ────────────────────────────────────────
        //  StageData auto-load (Inspector が空のとき用)
        // ────────────────────────────────────────

        void EnsureStageDataAssetsPopulated()
        {
            if (!autoLoadStageDataAssets) return;
            if (_autoLoadAttempted) return;
            _autoLoadAttempted = true;

            // 既にステージ番号付きで埋まっているなら何もしない
            int existing = stageDataAssets != null ? stageDataAssets.Count(a => a != null) : 0;
            if (existing > 0 && stageDataAssets.Length >= totalStages) return;

            string path = autoLoadResourcesPath ?? string.Empty;
            StageData[] loaded = Resources.LoadAll<StageData>(path);
            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning($"[StageManager] stageDataAssets が空でしたが、Resources/{path} に StageData が見つかりませんでした。\n" +
                                 "・Inspector の StageManager.stageDataAssets に Stage1〜N の StageData を割り当てる、または\n" +
                                 "・Assets/Resources/Stages/ 配下に Stage1〜N.asset を配置してください。");
                return;
            }

            // stageNumber 昇順に並べ替えてから index = stageNumber-1 に格納
            var ordered = loaded
                .Where(a => a != null)
                .OrderBy(a => a.stageNumber > 0 ? a.stageNumber : 999)
                .ToArray();

            int maxStage = ordered.Length;
            foreach (var a in ordered)
                if (a.stageNumber > maxStage) maxStage = a.stageNumber;

            var filled = new StageData[maxStage];
            foreach (var a in ordered)
            {
                int slot = a.stageNumber > 0 ? a.stageNumber - 1 : System.Array.IndexOf(ordered, a);
                if (slot >= 0 && slot < filled.Length && filled[slot] == null)
                    filled[slot] = a;
            }

            stageDataAssets = filled;
            Debug.Log($"[StageManager] stageDataAssets を Resources/{path} から自動ロードしました (count={ordered.Length})。");
        }
    }
}
