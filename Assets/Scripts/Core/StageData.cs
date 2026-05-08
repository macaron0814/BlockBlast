using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ステージ単位の構成データ。CSV「ステージ構成資料」のステージ行に相当する。
    /// 制限時間 / Wave 数 / ブロック増加初期値 などを保持する。
    /// </summary>
    [CreateAssetMenu(fileName = "NewStageData", menuName = "BlockBlast/Stage Data")]
    public class StageData : ScriptableObject
    {
        [Header("Stage Identity")]
        public int stageNumber;

        [Tooltip("CSV「ステージの特徴」欄。UI に表示する任意ラベル")]
        [TextArea]
        public string stageFeatureNote;

        [Header("Clear Conditions")]
        [Tooltip("制限時間 (秒)。CSV「制限時間」列。0 以下なら EnemyWaveData.survivalTime を使用")]
        public float timeLimitSeconds = 30f;

        [Tooltip("ライン消去ノルマ (Lines To Clear モード時のみ参照)")]
        public int linesToClear = 9999;

        [Header("Turn Settings (Lines To Clear モード時のみ参照)")]
        [Tooltip("ステージ開始時のターン数 (TurnManager.initialTurns > 0 の場合は無視)")]
        public int initialTurns = 9999;

        [Tooltip("1ライン消去あたりの基礎ターン回復量")]
        public int turnsPerLineClear = 9999;

        [Header("Block Cell Tier (CSV「ブロック増加」)")]
        [Tooltip("ステージ開始時のシェイプ最大セル数。\n3 = 「デフォ(3ブロックまで)」 / 4 = 「+1(4まで)」 / 5 = 「+1(5まで)」 / 0 = 「全ブロック解放(制限なし)」")]
        public int initialMaxBlockCells = BlockShapeLibrary.CellTier.Default;

        [Header("Block Cell Count Weights (出現確率調整)")]
        [Tooltip("セル数ごとの出現重み。\n" +
                 "・空 → 均等抽選 (BlockSpawner.cellCountWeights をそのまま使用)\n" +
                 "・要素あり → ステージ開始時にこの内容で BlockSpawner.cellCountWeights を上書き\n" +
                 "・例: 3セル=10 / 4セル=5 / 5セル=1 → 3:4:5 = 10:5:1 の比で出現")]
        public List<BlockCellCountWeight> blockCellCountWeights = new List<BlockCellCountWeight>();

        [Tooltip("blockCellCountWeights に未掲載のセル数に適用する既定重み。0 = リスト掲載のセル数しか出ない")]
        [Min(0f)]
        public float blockDefaultWeightForUnlistedCellCount = 1f;

        [Tooltip("blockCellCountWeights が空のとき、BlockSpawner.cellCountWeights を強制的にクリアして均等抽選に戻すか")]
        public bool resetWeightsWhenStageWeightsEmpty = false;

        [Header("Difficulty (旧パラメータ・参考値)")]
        [Range(0.1f, 2f)]
        public float difficultyMultiplier = 1f;

        [Header("Items")]
        [Tooltip("このステージで配置されるアイテム数")]
        public int itemCount = 3;

        // ──────────────────────────────────────────────
        //  CSV ベースのデフォルト値生成
        //  「ステージ構成資料 - シート1.csv」の各ステージ行を反映する。
        // ──────────────────────────────────────────────
        public static StageData CreateDefault(int stageNum)
        {
            var data = CreateInstance<StageData>();
            data.stageNumber = stageNum;
            data.linesToClear = 9999;
            data.initialTurns = 9999;
            data.turnsPerLineClear = 9999;

            switch (stageNum)
            {
                case 1:
                    // 30秒 / 1Wave / ザコA×4 / デフォ(3ブロック)
                    data.timeLimitSeconds      = 30f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Default;
                    data.difficultyMultiplier  = 0.6f;
                    data.itemCount             = 2;
                    data.stageFeatureNote      = "敵がプレイヤーに追いつくことはない";
                    break;

                case 2:
                    // 40秒 / 2Wave / 揃えて倒すを学ぶ / 3ブロックまで
                    data.timeLimitSeconds      = 40f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Default;
                    data.difficultyMultiplier  = 0.7f;
                    data.itemCount             = 3;
                    data.stageFeatureNote      = "揃えて倒すを学ぶ / ここから END 有";
                    break;

                case 3:
                    // 60秒 / 4Wave / 道中で +1 (4ブロックまで) を取得
                    data.timeLimitSeconds      = 60f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Default; // 道中ケーキで +1
                    data.difficultyMultiplier  = 0.8f;
                    data.itemCount             = 3;
                    data.stageFeatureNote      = "道中ケーキで 4 ブロックまで解放";
                    break;

                case 4:
                    // 60秒 / 4Wave / 4ブロック解放済み / 報酬に差が出始める
                    data.timeLimitSeconds      = 60f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Plus1;
                    data.difficultyMultiplier  = 0.9f;
                    data.itemCount             = 4;
                    data.stageFeatureNote      = "敵を倒した時とデフォの報酬に差が出始める";
                    break;

                case 5:
                    // 90秒 / 4Wave / 道中で +1 (5ブロックまで)
                    data.timeLimitSeconds      = 90f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Plus1;
                    data.difficultyMultiplier  = 1f;
                    data.itemCount             = 5;
                    data.stageFeatureNote      = "道中ケーキで 5 ブロックまで解放";
                    break;

                case 6:
                    // 90秒 / 4Wave / 5ブロック解放済み (CSV ではステージ番号欠落だが Wave 構成は記載あり)
                    data.timeLimitSeconds      = 90f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Plus2;
                    data.difficultyMultiplier  = 1.1f;
                    data.itemCount             = 5;
                    data.stageFeatureNote      = "中ボス出現";
                    break;

                case 7:
                    // 120秒 / 8Wave / 道中で +2 (全ブロック解放) / コインを稼ぐ場所
                    data.timeLimitSeconds      = 120f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Plus2;
                    data.difficultyMultiplier  = 1.2f;
                    data.itemCount             = 6;
                    data.stageFeatureNote      = "ボス戦に備えてコインを稼ぐ場所 / 道中で全ブロック解放";
                    break;

                case 8:
                    // 160秒 / 12Wave / ボス戦 / 全ブロック解放済み
                    data.timeLimitSeconds      = 160f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Unlocked;
                    data.difficultyMultiplier  = 1.4f;
                    data.itemCount             = 7;
                    data.stageFeatureNote      = "ボス戦";
                    break;

                default:
                    data.timeLimitSeconds      = 60f;
                    data.initialMaxBlockCells  = BlockShapeLibrary.CellTier.Plus1;
                    data.difficultyMultiplier  = 1f;
                    data.itemCount             = 3;
                    break;
            }

            return data;
        }
    }
}
