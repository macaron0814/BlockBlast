using UnityEngine;

namespace BlockBlastGame
{
    [DefaultExecutionOrder(-200)]
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Stage Data (drag & drop here)")]
        [Tooltip("Set StageData assets for each stage. Leave empty to use code defaults.")]
        public StageData[] stageDataAssets;

        void Awake()
        {
            var existing = FindObjectOfType<GameSetup>();
            if (existing == null)
            {
                var setupObj = new GameObject("GameSetup");
                existing = setupObj.AddComponent<GameSetup>();
            }

            // GameSetup.Awake() already fired inside AddComponent,
            // so StageManager exists now. Set data directly.
            if (stageDataAssets != null && stageDataAssets.Length > 0)
            {
                var sm = FindObjectOfType<StageManager>();
                if (sm != null)
                    sm.stageDataAssets = stageDataAssets;
            }
        }
    }
}
