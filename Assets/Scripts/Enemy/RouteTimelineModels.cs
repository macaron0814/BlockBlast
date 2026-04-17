using System;
using UnityEngine;

namespace BlockBlastGame
{
    public enum RouteEventType
    {
        None = 0,
        Shop = 1,
        VendingMachine = 2,
        Cake = 3,
        Boss = 4
    }

    [Serializable]
    public class RouteNodeConfig
    {
        [Tooltip("このマスに表示するイベントアイコン")]
        public RouteEventType eventType = RouteEventType.None;

        [Tooltip("Cake: ランダムブロック形状の追加数")]
        public int randomShapeIncrease = 1;

        [Tooltip("Boss: スポーンする敵データ")]
        public EnemyData spawnEnemy;
    }

    public sealed class RouteNodeRuntime
    {
        public int nodeIndex;
        public RouteEventType eventType;
        public int randomShapeIncrease;
        public EnemyData spawnEnemy;
        public bool eventTriggered;

        public RouteEventType GetDisplayEventType() => eventType;
    }

    public static class RouteTimelineMath
    {
        /// <summary>
        /// N マスを survivalTime で消化する場合の 1 マスあたりの秒数。
        /// </summary>
        public static float GetStepDuration(float totalDuration, int nodeCount)
        {
            return nodeCount <= 0 ? 0f : totalDuration / nodeCount;
        }

        /// <summary>
        /// 現在の経過時間でいくつのマスを消化済みか (0〜nodeCount)。
        /// </summary>
        public static int GetConsumedCount(float elapsed, float totalDuration, int nodeCount)
        {
            if (nodeCount <= 0) return 0;
            float step = GetStepDuration(totalDuration, nodeCount);
            if (step <= Mathf.Epsilon) return nodeCount;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed / step), 0, nodeCount);
        }

        /// <summary>
        /// ステップ末尾の moveDuration 秒間だけ 0→1 で進むアニメーション進行度。
        /// moveDuration=3 なら、各ステップの残り 3 秒でスライド移動する。
        /// </summary>
        public static float GetMoveTravelProgress(
            float elapsed, float totalDuration, int nodeCount, float moveDuration)
        {
            if (nodeCount <= 0) return 0f;
            float step = GetStepDuration(totalDuration, nodeCount);
            if (step <= Mathf.Epsilon) return 0f;
            int consumed = GetConsumedCount(elapsed, totalDuration, nodeCount);
            if (consumed >= nodeCount) return 0f;
            float timeIntoStep = elapsed - consumed * step;
            float clampedMove = Mathf.Clamp(moveDuration, 0f, step);
            float moveStart = step - clampedMove;
            if (timeIntoStep < moveStart) return 0f;
            return clampedMove <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01((timeIntoStep - moveStart) / clampedMove);
        }
    }
}
