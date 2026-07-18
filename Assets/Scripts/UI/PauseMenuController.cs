using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// ゲーム中の設定/ポーズ Canvas を開閉する。
    /// 一時停止自体は VendingMachine と同じ GamePauseService を使う。
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        const string PauseHandle = "PauseMenu";

        [Header("References")]
        [Tooltip("設定画面/ポーズ画面のルート Canvas または Panel。開いている間だけ Active になる。")]
        public GameObject pauseCanvas;

        [Tooltip("ゲーム中 HUD 側の設定/ポーズボタン。押すと pauseCanvas を表示してゲームを停止する。")]
        public Button openButton;

        [Tooltip("pauseCanvas 内の閉じるボタン。押すと pauseCanvas を非表示にしてゲームを再開する。")]
        public Button closeButton;

        [Header("Haptics Toggle")]
        [Tooltip("PauseMenu 内の振動ON/OFFボタン。")]
        public Button hapticsToggleButton;

        [Tooltip("振動ONのときだけ表示するチェック画像（hapticsToggleButtonの子）。")]
        public GameObject hapticsCheckObject;

        [Header("Audio Volume Sliders")]
        [Tooltip("BGM共通音量を0〜1で調整するスライダー。")]
        public Slider bgmVolumeSlider;

        [Tooltip("SE共通音量を0〜1で調整するスライダー。")]
        public Slider seVolumeSlider;

        [Tooltip("ポーズ画面の背面に表示する blur overlay。\n未設定ならこの GameObject か子から自動取得する。")]
        public PauseBlurOverlay blurOverlay;

        [Header("Behavior")]
        [Tooltip("ON: GameState.Playing / LineClearing のときだけポーズ画面を開ける。")]
        public bool onlyOpenDuringGameplay = true;

        [Tooltip("ON: OnEnable 時に pauseCanvas を非表示へ初期化する。")]
        public bool hideCanvasOnStart = true;

        [Tooltip("ON: Escape キーで開閉する。デバッグ/PC確認用。")]
        public bool toggleWithEscapeKey = true;

        [Tooltip("ON: ポーズ Canvas を表示する前に、現在の画面をキャプチャして徐々にぼかす。")]
        public bool blurBackgroundBeforeShowingCanvas = true;

        [Header("Events")]
        public UnityEvent onPauseOpened;
        public UnityEvent onPauseClosed;

        [Header("Runtime (read only)")]
        [SerializeField] bool _isOpen;
        [SerializeField] bool _isOpening;

        public bool IsOpen => _isOpen;
        public bool IsOpening => _isOpening;

        Coroutine _openRoutine;

        void OnEnable()
        {
            if (blurOverlay == null)
                blurOverlay = GetComponentInChildren<PauseBlurOverlay>(true);

            BindButtons(true);
            RefreshHapticsToggleVisual();
            RefreshAudioSliders();

            if (hideCanvasOnStart && pauseCanvas != null)
                pauseCanvas.SetActive(false);
            if (hideCanvasOnStart && blurOverlay != null)
                blurOverlay.HideImmediate();

            _isOpen = pauseCanvas != null && pauseCanvas.activeSelf;
            if (_isOpen)
                GamePauseService.Pause(PauseHandle);
        }

        void OnDisable()
        {
            BindButtons(false);
            ForceCloseWithoutEvents();
        }

        void Update()
        {
            if (!toggleWithEscapeKey) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            TogglePauseMenu();
        }

        void BindButtons(bool subscribe)
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OpenPauseMenu);
                if (subscribe)
                    openButton.onClick.AddListener(OpenPauseMenu);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePauseMenu);
                if (subscribe)
                    closeButton.onClick.AddListener(ClosePauseMenu);
            }

            if (hapticsToggleButton != null)
            {
                hapticsToggleButton.onClick.RemoveListener(ToggleHaptics);
                if (subscribe)
                    hapticsToggleButton.onClick.AddListener(ToggleHaptics);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeSliderChanged);
                if (subscribe)
                    bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeSliderChanged);
            }

            if (seVolumeSlider != null)
            {
                seVolumeSlider.onValueChanged.RemoveListener(OnSeVolumeSliderChanged);
                if (subscribe)
                    seVolumeSlider.onValueChanged.AddListener(OnSeVolumeSliderChanged);
            }
        }

        public void ToggleHaptics()
        {
            bool enabled = !MobileHaptics.IsEnabled;
            MobileHaptics.SetEnabled(enabled);
            RefreshHapticsToggleVisual();

            if (enabled)
                MobileHaptics.PlayLightImpact();
        }

        void RefreshHapticsToggleVisual()
        {
            if (hapticsCheckObject != null)
                hapticsCheckObject.SetActive(MobileHaptics.IsEnabled);
        }

        void RefreshAudioSliders()
        {
            SoundManager soundManager = SoundManager.Instance;
            float bgmVolume = soundManager != null ? soundManager.bgmVolume : 0.5f;
            float seVolume = soundManager != null ? soundManager.seVolume : 0.5f;

            if (bgmVolumeSlider != null)
                bgmVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(bgmVolume));
            if (seVolumeSlider != null)
                seVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(seVolume));
        }

        void OnBgmVolumeSliderChanged(float value)
        {
            SoundManager.SetBGMVolume(Mathf.Clamp01(value));
        }

        void OnSeVolumeSliderChanged(float value)
        {
            SoundManager.SetSEVolume(Mathf.Clamp01(value));
        }

        public void TogglePauseMenu()
        {
            if (_isOpen)
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }

        public void OpenPauseMenu()
        {
            if (_isOpen) return;
            if (_isOpening) return;
            if (!CanOpenPauseMenu()) return;

            _openRoutine = StartCoroutine(OpenPauseMenuCo());
        }

        IEnumerator OpenPauseMenuCo()
        {
            _isOpening = true;
            _isOpen = true;

            SoundManager.Play(SoundCue.Pause);

            // まずゲームを止める。blur 演出は unscaled time で進める。
            GamePauseService.Pause(PauseHandle);

            // ぼかし終わるまでは設定 Canvas を出さない。
            if (pauseCanvas != null)
                pauseCanvas.SetActive(false);

            if (blurBackgroundBeforeShowingCanvas && blurOverlay != null)
            {
                blurOverlay.transform.SetAsLastSibling();
                yield return blurOverlay.CaptureAndBlurIn();
            }

            if (!_isOpen)
            {
                _isOpening = false;
                _openRoutine = null;
                yield break;
            }

            if (pauseCanvas != null)
            {
                pauseCanvas.SetActive(true);
                pauseCanvas.transform.SetAsLastSibling();
            }

            RefreshHapticsToggleVisual();
            RefreshAudioSliders();
            _isOpening = false;
            _openRoutine = null;
            onPauseOpened?.Invoke();
        }

        public void ClosePauseMenu()
        {
            if (!_isOpen) return;

            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            _isOpen = false;
            _isOpening = false;
            if (pauseCanvas != null)
                pauseCanvas.SetActive(false);
            if (blurOverlay != null)
                blurOverlay.HideImmediate();

            SoundManager.Play(SoundCue.Pause);
            GamePauseService.Resume(PauseHandle);
            onPauseClosed?.Invoke();
        }

        bool CanOpenPauseMenu()
        {
            if (!onlyOpenDuringGameplay) return true;
            if (GameManager.Instance == null) return true;

            GameState state = GameManager.Instance.currentState;
            return state == GameState.Playing || state == GameState.LineClearing;
        }

        void ForceCloseWithoutEvents()
        {
            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            if (pauseCanvas != null)
                pauseCanvas.SetActive(false);
            if (blurOverlay != null)
                blurOverlay.HideImmediate();

            _isOpen = false;
            _isOpening = false;
            GamePauseService.Resume(PauseHandle);
        }
    }
}
