using UnityEngine;

namespace BlockBlastGame
{
    public class ChaseSystem : MonoBehaviour
    {
        [Header("References")]
        public TurnManager turnManager;

        [Header("Visual")]
        public float screenShakeIntensity = 0.1f;

        float currentUrgency;

        void Update()
        {
            if (GameManager.Instance.currentState != GameState.Playing) return;

            float targetUrgency = turnManager.GetUrgencyLevel();
            currentUrgency = Mathf.Lerp(currentUrgency, targetUrgency, Time.deltaTime * 3f);

            if (currentUrgency > 0.8f)
            {
                ApplyScreenShake();
            }
        }

        void ApplyScreenShake()
        {
            if (Camera.main == null) return;
            float shake = screenShakeIntensity * currentUrgency;
            Vector3 offset = new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                0);
            Camera.main.transform.localPosition = Camera.main.transform.localPosition + offset * Time.deltaTime;
        }

        public GameOverType DetermineGameOverType() => GameOverType.PuzzleStuck;

        public string GetGameOverMessage(GameOverType type)
        {
            return type switch
            {
                GameOverType.EnemyCapture => "敵に追いつかれた",
                GameOverType.PuzzleStuck => "置けるところがなくなった",
                _ => "ゲームオーバー"
            };
        }

        public string GetGameOverTitle(GameOverType type) => "ゲームオーバー";

        public float GetCurrentUrgency() => currentUrgency;
    }
}
