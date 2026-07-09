using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// ポーズ画面などで、BGM / SE 音量を 10 段階表示・調整する。
    /// iconRoot の子 0..9 を「音量 1..10」のバーとして扱い、現在レベル以下を Active にする。
    /// </summary>
    public class AudioVolumeIconController : MonoBehaviour
    {
        [Header("Icon Roots")]
        [Tooltip("BGMIcon。子オブジェクト 1〜10 が音量表示になる。")]
        public Transform bgmIconRoot;

        [Tooltip("SEIcon。子オブジェクト 1〜10 が音量表示になる。")]
        public Transform seIconRoot;

        [Header("Optional Buttons")]
        public Button bgmVolumeDownButton;
        public Button bgmVolumeUpButton;
        public Button seVolumeDownButton;
        public Button seVolumeUpButton;

        [Header("Behavior")]
        [Tooltip("ON: ボタン押下時に SoundManager の VolumeAdjust SE を鳴らす。SoundManager 側でも鳴るので通常 ON のままで OK。")]
        public bool bindButtonsOnEnable = true;

        void OnEnable()
        {
            AutoFindIconRoots();
            SoundManager.OnVolumeLevelsChanged += HandleVolumeLevelsChanged;
            BindButtons(true);
            Refresh();
        }

        void OnDisable()
        {
            SoundManager.OnVolumeLevelsChanged -= HandleVolumeLevelsChanged;
            BindButtons(false);
        }

        void BindButtons(bool subscribe)
        {
            if (!bindButtonsOnEnable) return;

            BindButton(bgmVolumeDownButton, DecreaseBgm, subscribe);
            BindButton(bgmVolumeUpButton, IncreaseBgm, subscribe);
            BindButton(seVolumeDownButton, DecreaseSe, subscribe);
            BindButton(seVolumeUpButton, IncreaseSe, subscribe);
        }

        static void BindButton(Button button, UnityEngine.Events.UnityAction action, bool subscribe)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            if (subscribe)
                button.onClick.AddListener(action);
        }

        public void IncreaseBgm() => SoundManager.AddBGMVolumeLevel(1);
        public void DecreaseBgm() => SoundManager.AddBGMVolumeLevel(-1);
        public void IncreaseSe() => SoundManager.AddSEVolumeLevel(1);
        public void DecreaseSe() => SoundManager.AddSEVolumeLevel(-1);

        public void SetBgmLevel(int level) => SoundManager.SetBGMVolumeLevel(level);
        public void SetSeLevel(int level) => SoundManager.SetSEVolumeLevel(level);

        public void Refresh()
        {
            var sm = SoundManager.Instance;
            int seLevel = sm != null ? sm.seVolumeLevel : 10;
            int bgmLevel = sm != null ? sm.bgmVolumeLevel : 10;
            ApplyIconLevel(bgmIconRoot, bgmLevel);
            ApplyIconLevel(seIconRoot, seLevel);
        }

        void HandleVolumeLevelsChanged(int seLevel, int bgmLevel)
        {
            ApplyIconLevel(bgmIconRoot, bgmLevel);
            ApplyIconLevel(seIconRoot, seLevel);
        }

        static void ApplyIconLevel(Transform root, int level)
        {
            if (root == null) return;

            level = Mathf.Clamp(level, 0, 10);
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;
                child.gameObject.SetActive(i < level);
            }
        }

        void AutoFindIconRoots()
        {
            if (bgmIconRoot == null)
                bgmIconRoot = FindChildByName("BGMIcon");
            if (seIconRoot == null)
                seIconRoot = FindChildByName("SEIcon");
        }

        Transform FindChildByName(string targetName)
        {
            var children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == targetName)
                    return children[i];
            }

            return null;
        }
    }
}
