using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// UI Button から指定シーンへ遷移するための小さい共通コンポーネント。
    /// Title -> Main など、シンプルな画面遷移に使う。
    /// </summary>
    public class SceneTransitionButton : MonoBehaviour
    {
        [Tooltip("押下を監視するボタン。未設定なら同じ GameObject から自動取得する。")]
        public Button button;

        [Tooltip("遷移先シーン名。Build Settings に登録されている必要がある。")]
        public string targetSceneName = "Main";

        [Tooltip("ON: シーン遷移前に GamePauseService と Time.timeScale をリセットする。")]
        public bool resetPauseBeforeLoad = true;

        void Reset()
        {
            button = GetComponent<Button>();
        }

        void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (button == null) return;

            button.onClick.RemoveListener(LoadTargetScene);
            button.onClick.AddListener(LoadTargetScene);
        }

        void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(LoadTargetScene);
        }

        public void LoadTargetScene()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning("[SceneTransitionButton] targetSceneName が未設定です。");
                return;
            }

            if (resetPauseBeforeLoad)
            {
                GamePauseService.ResetAll();
                Time.timeScale = 1f;
            }

            SceneManager.LoadScene(targetSceneName);
        }
    }
}
