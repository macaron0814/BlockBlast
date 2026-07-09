using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// Title / Game / Shop BGM を任意のタイミングで再生するための薄いトリガー。
    /// ボタンの OnClick やシーン上の GameObject の OnEnable から呼び出せる。
    /// </summary>
    public class BgmPlaybackTrigger : MonoBehaviour
    {
        public BgmCue bgmCue = BgmCue.Title;
        public bool playOnEnable = true;

        void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        public void Play()
        {
            SoundManager.PlayBgm(bgmCue);
        }

        public void Stop()
        {
            SoundManager.StopBgm();
        }
    }
}
