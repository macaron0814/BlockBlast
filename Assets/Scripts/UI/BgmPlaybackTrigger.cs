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

        [Tooltip("SoundManagerが存在しないTitleシーンなどで使うAudioSource。")]
        public AudioSource fallbackAudioSource;

        [Tooltip("フォールバック再生時の楽曲個別音量。最終音量 = BGM設定値 × この値。")]
        [Range(0f, 1f)]
        public float fallbackTrackVolume = 1f;

        void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        public void Play()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.PlayBgm(bgmCue);
                return;
            }

            if (fallbackAudioSource == null)
                fallbackAudioSource = GetComponent<AudioSource>();
            if (fallbackAudioSource == null)
                return;

            fallbackAudioSource.volume = Mathf.Clamp01(
                SoundManager.GetSavedBGMVolume() * fallbackTrackVolume);
            fallbackAudioSource.loop = true;
            fallbackAudioSource.Play();
        }

        public void Stop()
        {
            if (SoundManager.Instance != null)
                SoundManager.StopBgm();
            else if (fallbackAudioSource != null)
                fallbackAudioSource.Stop();
        }
    }
}
