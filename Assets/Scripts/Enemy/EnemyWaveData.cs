using System;
using UnityEngine;

namespace BlockBlastGame
{
    [Serializable]
    public class EnemyWaveEntry
    {
        [Tooltip("このウェーブで出現する敵の配列")]
        public EnemyData[] enemies;

        [Tooltip("ウェーブ内の敵同士の生成間隔 (秒)")]
        public float spawnInterval = 1.5f;

        [Tooltip("ステージ開始からの絶対時間 (秒)。0 以上を指定するとその時刻に Wave 開始。\n-1 (デフォルト) なら従来通り、直前 Wave 終了 + intervalBetweenWaves で開始する。\nCSV「出現秒数」列に対応。")]
        public float startTimeSeconds = -1f;
    }

    [CreateAssetMenu(fileName = "EnemyWaveData", menuName = "BlockBlast/Enemy Wave Data")]
    public class EnemyWaveData : ScriptableObject
    {
        [Header("ウェーブ定義")]
        [Tooltip("ウェーブの配列。上から順に実行される")]
        public EnemyWaveEntry[] waves;

        [Header("ウェーブ間隔")]
        [Tooltip("ウェーブ終了後、次のウェーブ開始までの待ち時間 (秒)")]
        public float intervalBetweenWaves = 5f;

        [Header("制限時間")]
        [Tooltip("この秒数を生き残ればクリア。0 = 全ウェーブ完了でクリア")]
        public float survivalTime = 60f;

        [Header("ルートマス配置")]
        [Tooltip("マスごとのイベント設定。配列の長さ = マス数。None ならアイコン無しの空マス")]
        public RouteNodeConfig[] routeNodes;

        public static EnemyWaveData CreateDefault()
        {
            var d = CreateInstance<EnemyWaveData>();
            d.name = "DefaultWave";
            d.intervalBetweenWaves = 5f;
            d.survivalTime = 60f;

            d.routeNodes = new RouteNodeConfig[]
            {
                new RouteNodeConfig { eventType = RouteEventType.None },
                new RouteNodeConfig { eventType = RouteEventType.Shop },
                new RouteNodeConfig { eventType = RouteEventType.Cake, maxCellIncrease = 1 },
                new RouteNodeConfig { eventType = RouteEventType.Boss },
            };

            d.waves = new EnemyWaveEntry[]
            {
                new EnemyWaveEntry
                {
                    enemies = new[] { EnemyData.CreateDefault(0) },
                    spawnInterval = 2f
                },
                new EnemyWaveEntry
                {
                    enemies = new[] { EnemyData.CreateDefault(0), EnemyData.CreateDefault(1) },
                    spawnInterval = 1.5f
                },
                new EnemyWaveEntry
                {
                    enemies = new[] { EnemyData.CreateDefault(0), EnemyData.CreateDefault(1), EnemyData.CreateDefault(2) },
                    spawnInterval = 1f
                },
            };

            return d;
        }
    }
}
