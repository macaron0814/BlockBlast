using UnityEngine;

namespace BlockBlastGame
{
    public class ChaseSystem : MonoBehaviour
    {
        [Header("References")]
        public TurnManager turnManager;

        [Header("Chase Settings")]
        public GameOverType[] possibleGameOverTypes = {
            GameOverType.FanChase,
            GameOverType.EnemyCapture
        };

        [Header("Happy Ending Messages")]
        public string[] fanChaseMessages = {
            "ファンに囲まれた！\nあなたは超人気者になった！",
            "追いかけてきたのは熱狂的なファンだった！\nサイン会が始まった！",
            "逃げ切れなかった...でも\n世界一のアイドルに認定された！"
        };

        public string[] enemyCaptureMessages = {
            "敵軍に捕まった！\nでもあなたの魅力で敵軍のアイドルに！",
            "捕獲された...はずが\n敵の将軍があなたのファンだった！",
            "敵の基地に連行された！\nでもそこでライブを開催することに！"
        };

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

        public GameOverType DetermineGameOverType()
        {
            return possibleGameOverTypes[Random.Range(0, possibleGameOverTypes.Length)];
        }

        public string GetGameOverMessage(GameOverType type)
        {
            return type switch
            {
                GameOverType.FanChase => fanChaseMessages[Random.Range(0, fanChaseMessages.Length)],
                GameOverType.EnemyCapture => enemyCaptureMessages[Random.Range(0, enemyCaptureMessages.Length)],
                _ => "ゲームオーバー！"
            };
        }

        public string GetGameOverTitle(GameOverType type)
        {
            return type switch
            {
                GameOverType.FanChase => "超人気者エンド！",
                GameOverType.EnemyCapture => "敵軍アイドルエンド！",
                _ => "ゲームオーバー"
            };
        }

        public float GetCurrentUrgency() => currentUrgency;
    }
}
