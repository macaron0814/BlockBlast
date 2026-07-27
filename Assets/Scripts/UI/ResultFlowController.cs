using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// ゲームオーバー時に旧 GameOverPanel の代わりに Result Canvas を表示する。
    ///
    /// ■ 演出フロー (敵に追いつかれた等)
    ///   1. GamePauseService で世界全体を一時停止 (敵 / 道 / タイマー / ミニマップ)
    ///   2. プレイヤー画像をゲームオーバー用テクスチャへ差し替え
    ///   3. 画面をフェードアウト
    ///   4. フェード完了後に Result Canvas を表示
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ResultFlowController : MonoBehaviour
    {
        public static ResultFlowController Instance { get; private set; }

        const string PauseHandle = "Result";

        [Header("UI")]
        [Tooltip("表示する Result Canvas / Panel。未設定ならこの GameObject を使う。")]
        public GameObject resultCanvas;

        [Tooltip("旧 GameOverPanel。表示前に必ず非表示にする。")]
        public GameObject legacyGameOverPanel;

        [Tooltip("Result 表示時に再生する SequentialAnimatorPlayer。空なら resultCanvas から取得。")]
        public SequentialAnimatorPlayer sequencePlayer;

        [Tooltip("リザルト画面内のボタン。押すと replayTargetSceneName へ戻る。")]
        public Button resultReloadButton;

        [Tooltip("リザルトボタン押下時の遷移先シーン名。通常は Title。")]
        public string replayTargetSceneName = "Title";

        [Header("Result Score")]
        [Tooltip("今回のカロリースコアを表示する TMP。")]
        public TMP_Text currentScoreTextTMP;

        [Tooltip("今回のカロリースコアを表示する uGUI Text。TMP を使わない場合用。")]
        public Text currentScoreText;

        [Tooltip("ローカル保存されたベストスコアを表示する TMP。")]
        public TMP_Text bestScoreTextTMP;

        [Tooltip("ローカル保存されたベストスコアを表示する uGUI Text。TMP を使わない場合用。")]
        public Text bestScoreText;

        [Tooltip("スコア表示フォーマット。{0}=カロリー値。")]
        public string currentScoreFormat = "{0}";

        [Tooltip("ベストスコア表示フォーマット。{0}=ベストカロリー値。")]
        public string bestScoreFormat = "{0}";

        [Tooltip("PlayerPrefs に保存するベストスコアのキー。再インストールまでは保持される。")]
        public string bestScorePrefsKey = "BestCalorieScore";

        [Header("Systems")]
        public EnemySystem enemySystem;
        public UIManager uiManager;

        [Header("Player Game Over Visual")]
        [Tooltip("プレイヤー画像 (CharacterAnimator)。ゲームオーバー時に画像を固定差し替えする。")]
        public CharacterAnimator playerCharacter;

        [Tooltip("CharacterAnimator が無い場合に直接差し替える Image。")]
        public Image playerImageFallback;

        [Tooltip("差し替えるゲームオーバー用スプライト。未設定なら差し替えしない。")]
        public Sprite gameOverSprite;

        [Tooltip("画像差し替え後、フェード開始までの待ち時間 (秒)。Realtime。")]
        [Min(0f)]
        public float holdBeforeFade = 0.4f;

        [Header("Reward Continue")]
        public bool enableRewardContinue = true;
        [Tooltip("リワード復活ボタンのResult内座標。")]
        public Vector2 rewardContinueAnchoredPosition = new Vector2(0f, -150f);
        public Vector2 rewardContinueSize = new Vector2(360f, 282f);
        [Min(0.03f)]
        public float rewardFrameInterval = 0.12f;
        [Tooltip("復活時に敵を最低でもこの角度まで離す。")]
        [Min(0f)]
        public float continueMinimumEnemyDistance = 40f;
        [Tooltip("各敵の現在位置へ追加する後退角度。")]
        [Min(0f)]
        public float continueEnemyPushDistance = 25f;
        [Min(1)]
        public int continueMinimumTurns = 3;
        [Min(0f)]
        public float continueFadeInDuration = 0.35f;

        [Header("Fade")]
        [Tooltip("フェードに使う CanvasGroup。未設定なら全画面フェードを自動生成する。")]
        public CanvasGroup fadeCanvasGroup;

        [Tooltip("自動生成フェードの色。")]
        public Color fadeColor = Color.black;

        [Tooltip("フェードアウトにかける秒数 (Realtime)。")]
        [Min(0f)]
        public float fadeOutDuration = 0.6f;

        [Tooltip("フェード用 Canvas の sortingOrder。Result より下にする。")]
        public int fadeSortingOrder = 1000;

        [Tooltip("Result Canvas の sortingOrder。フェードより上にする。")]
        public int resultSortingOrder = 1001;

        [Header("Behavior")]
        [Tooltip("ON: 旧 GameOverPanel を出さず Result Canvas に置き換える。")]
        public bool replaceLegacyGameOverPanel = true;

        [Tooltip("ON: 起動時に resultCanvas を非表示にする。")]
        public bool hideCanvasOnStart = true;

        [Tooltip("ON: resultReloadButton が未設定の場合、resultCanvas 配下の最初の Button を自動で使う。")]
        public bool autoBindFirstResultButton = true;

        [Header("Runtime (read only)")]
        [SerializeField] bool _resultOpen;
        [SerializeField] GameOverType _lastGameOverType;
        [SerializeField] long _currentCalories;
        [SerializeField] long _bestCalories;

        Coroutine _sequenceRoutine;
        Coroutine _rewardFrameRoutine;
        Button _rewardContinueButton;
        Image _rewardContinueImage;
        readonly Dictionary<int, Sprite[]> _rewardFrames = new Dictionary<int, Sprite[]>();

        public bool IsResultOpen => _resultOpen;
        public GameOverType LastGameOverType => _lastGameOverType;

        public static bool ShouldSuppressLegacyGameOverPanel =>
            Instance != null
            && Instance.replaceLegacyGameOverPanel
            && Instance.ResolveResultCanvas() != null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveReferences();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnCalorieChanged += HandleCalorieChanged;
            ResolveReferences();
            BindResultButton(true);
            BindRewardService(true);
            EnsureRewardContinueButton();
            _bestCalories = LoadBestScore();

            if (hideCanvasOnStart)
            {
                var canvas = ResolveResultCanvas();
                if (canvas != null)
                    canvas.SetActive(false);
            }

            if (fadeCanvasGroup != null)
                SetFadeAlpha(0f);
        }

        void OnDisable()
        {
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnCalorieChanged -= HandleCalorieChanged;
            BindResultButton(false);
            BindRewardService(false);
            ForceCloseWithoutEvents();
        }

        void ResolveReferences()
        {
            if (resultCanvas == null)
                resultCanvas = gameObject;

            if (sequencePlayer == null)
                sequencePlayer = GetComponent<SequentialAnimatorPlayer>();

            if (enemySystem == null && GameManager.Instance != null)
                enemySystem = GameManager.Instance.enemySystem;

            if (uiManager == null && GameManager.Instance != null)
                uiManager = GameManager.Instance.uiManager;

            if (legacyGameOverPanel == null && uiManager != null)
                legacyGameOverPanel = uiManager.gameOverPanel;

            if (playerCharacter == null)
                playerCharacter = FindObjectOfType<CharacterAnimator>();

            if (resultReloadButton == null && autoBindFirstResultButton)
            {
                var canvas = ResolveResultCanvas();
                if (canvas != null)
                    resultReloadButton = canvas.GetComponentInChildren<Button>(true);
            }
        }

        void HandleCalorieChanged(long totalCalories)
        {
            _currentCalories = CalorieFormatter.Clamp(totalCalories);
        }

        void BindResultButton(bool subscribe)
        {
            if (resultReloadButton == null) return;

            resultReloadButton.onClick.RemoveListener(ReloadCurrentScene);
            if (subscribe)
                resultReloadButton.onClick.AddListener(ReloadCurrentScene);
        }

        void BindRewardService(bool subscribe)
        {
            RewardedAdService service = RewardedAdService.Instance;
            if (service == null)
                return;

            service.RewardCompleted -= HandleRewardCompleted;
            service.RewardFailed -= HandleRewardFailed;
            if (subscribe)
            {
                service.RewardCompleted += HandleRewardCompleted;
                service.RewardFailed += HandleRewardFailed;
            }
        }

        void EnsureRewardContinueButton()
        {
            if (!enableRewardContinue || _rewardContinueButton != null)
                return;

            GameObject canvas = ResolveResultCanvas();
            if (canvas == null)
                return;

            var buttonObject = new GameObject(
                "RewardContinueButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.layer = canvas.layer;
            buttonObject.transform.SetParent(canvas.transform, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = rewardContinueAnchoredPosition;
            rect.sizeDelta = rewardContinueSize;

            _rewardContinueImage = buttonObject.GetComponent<Image>();
            _rewardContinueImage.preserveAspect = true;
            _rewardContinueImage.raycastTarget = true;

            _rewardContinueButton = buttonObject.GetComponent<Button>();
            _rewardContinueButton.targetGraphic = _rewardContinueImage;
            _rewardContinueButton.onClick.AddListener(HandleRewardContinueClicked);
            buttonObject.SetActive(false);
        }

        void HandleRewardContinueClicked()
        {
            RewardedAdService service = RewardedAdService.Instance;
            if (!_resultOpen || service == null || !service.CanContinue)
            {
                RefreshRewardContinueUI();
                return;
            }

            if (_rewardContinueButton != null)
                _rewardContinueButton.interactable = false;
            service.ShowForContinue();
        }

        void HandleRewardCompleted()
        {
            if (!_resultOpen)
                return;

            StartCoroutine(ContinueAfterReward());
        }

        void HandleRewardFailed(string message)
        {
            Debug.LogWarning($"[ResultFlowController] Reward continue failed: {message}");
            RefreshRewardContinueUI();
        }

        void RefreshRewardContinueUI()
        {
            EnsureRewardContinueButton();
            if (_rewardContinueButton == null)
                return;

            RewardedAdService service = RewardedAdService.Instance;
            int remaining = service != null ? service.RemainingAttempts : 0;
            bool visible = enableRewardContinue && _resultOpen && remaining > 0;
            _rewardContinueButton.gameObject.SetActive(visible);
            _rewardContinueButton.interactable = visible && service != null && service.CanContinue;

            if (!visible)
            {
                StopRewardFrameAnimation();
                return;
            }

            Sprite[] frames = GetRewardFrames(remaining);
            if (_rewardContinueImage != null)
                _rewardContinueImage.sprite = frames.Length > 0 ? frames[0] : null;

            StartRewardFrameAnimation();
        }

        Sprite[] GetRewardFrames(int remaining)
        {
            remaining = Mathf.Clamp(remaining, 1, 3);
            if (_rewardFrames.TryGetValue(remaining, out Sprite[] cached))
                return cached;

            string resourcePath = $"RewardContinue/{remaining}";
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            System.Array.Sort(textures, (a, b) =>
                string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));

            var frames = new List<Sprite>(textures.Length);
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null) continue;
                frames.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f));
            }

            if (frames.Count == 0)
            {
                Sprite[] importedSprites = Resources.LoadAll<Sprite>(resourcePath);
                System.Array.Sort(importedSprites, (a, b) =>
                    string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));
                frames.AddRange(importedSprites);
            }

            cached = frames.ToArray();
            _rewardFrames[remaining] = cached;
            return cached;
        }

        void StartRewardFrameAnimation()
        {
            StopRewardFrameAnimation();
            _rewardFrameRoutine = StartCoroutine(AnimateRewardFrames());
        }

        void StopRewardFrameAnimation()
        {
            if (_rewardFrameRoutine == null)
                return;
            StopCoroutine(_rewardFrameRoutine);
            _rewardFrameRoutine = null;
        }

        IEnumerator AnimateRewardFrames()
        {
            int frameIndex = 0;
            while (_resultOpen && _rewardContinueImage != null)
            {
                RewardedAdService service = RewardedAdService.Instance;
                int remaining = service != null ? service.RemainingAttempts : 0;
                if (remaining <= 0)
                    break;

                Sprite[] frames = GetRewardFrames(remaining);
                if (frames.Length > 0)
                {
                    _rewardContinueImage.sprite = frames[frameIndex % frames.Length];
                    frameIndex++;
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.03f, rewardFrameInterval));
            }

            _rewardFrameRoutine = null;
        }

        GameObject ResolveResultCanvas()
        {
            return resultCanvas != null ? resultCanvas : gameObject;
        }

        void HandleGameOver(GameOverType type)
        {
            if (!replaceLegacyGameOverPanel) return;
            if (ResolveResultCanvas() == null) return;

            OpenResult(type);
        }

        public void OpenResult(GameOverType type)
        {
            if (_resultOpen) return;

            ResolveReferences();
            _resultOpen = true;
            _lastGameOverType = type;

            if (legacyGameOverPanel != null)
                legacyGameOverPanel.SetActive(false);

            // 1. 全体停止
            StopWorldForResult();

            // 結果 Canvas はフェード後に出すので、まだ隠しておく
            var canvas = ResolveResultCanvas();
            if (canvas != null)
                canvas.SetActive(false);

            if (_sequenceRoutine != null)
                StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(PlayResultSequence());
        }

        IEnumerator PlayResultSequence()
        {
            // 2. プレイヤー画像をゲームオーバー用へ差し替え
            SwapPlayerToGameOver();

            if (holdBeforeFade > 0f)
                yield return new WaitForSecondsRealtime(holdBeforeFade);

            // 3. フェードアウト
            EnsureFadeReady();
            yield return FadeTo(1f, fadeOutDuration);

            // 4. リザルト表示
            RefreshResultScoreTexts();

            var canvas = ResolveResultCanvas();
            if (canvas != null)
            {
                EnsureResultAboveFade(canvas);
                canvas.SetActive(true);
                canvas.transform.SetAsLastSibling();
            }

            RefreshRewardContinueUI();

            if (sequencePlayer != null && !sequencePlayer.playOnEnable)
                sequencePlayer.PlaySequence();

            _sequenceRoutine = null;
        }

        void RefreshResultScoreTexts()
        {
            long current = CalorieFormatter.Clamp(_currentCalories);
            long best = LoadBestScore();
            if (current > best)
            {
                best = current;
                SaveBestScore(best);
            }

            _bestCalories = best;

            string currentText = string.Format(currentScoreFormat, CalorieFormatter.Format(current));
            string bestText = string.Format(bestScoreFormat, CalorieFormatter.Format(best));

            if (currentScoreTextTMP != null) currentScoreTextTMP.text = currentText;
            if (currentScoreText != null) currentScoreText.text = currentText;
            if (bestScoreTextTMP != null) bestScoreTextTMP.text = bestText;
            if (bestScoreText != null) bestScoreText.text = bestText;
        }

        long LoadBestScore()
        {
            string longKey = bestScorePrefsKey + ".Long";
            if (PlayerPrefs.HasKey(longKey)
                && long.TryParse(PlayerPrefs.GetString(longKey), out long stored))
            {
                return CalorieFormatter.Clamp(stored);
            }

            // 旧int保存データからの移行。
            return CalorieFormatter.Clamp(PlayerPrefs.GetInt(bestScorePrefsKey, 0));
        }

        void SaveBestScore(long value)
        {
            value = CalorieFormatter.Clamp(value);
            PlayerPrefs.SetString(bestScorePrefsKey + ".Long", value.ToString());
            PlayerPrefs.Save();
        }

        void SwapPlayerToGameOver()
        {
            if (gameOverSprite == null) return;

            if (playerCharacter != null)
                playerCharacter.ShowStaticSprite(gameOverSprite);
            else if (playerImageFallback != null)
                playerImageFallback.sprite = gameOverSprite;
        }

        void StopWorldForResult()
        {
            // Wave/サバイバル状態を破棄せず、その場からコンティニューできるよう
            // TimeScaleの一時停止だけを使う。
            GamePauseService.Pause(PauseHandle);
        }

        // ─────────────────────────────────────
        //  Fade
        // ─────────────────────────────────────

        void EnsureFadeReady()
        {
            if (fadeCanvasGroup != null) return;

            var canvasObj = new GameObject("ResultFadeOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = fadeSortingOrder;

            var imgObj = new GameObject("FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgObj.transform.SetParent(canvasObj.transform, false);
            var rect = imgObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imgObj.GetComponent<Image>();
            image.color = fadeColor;
            image.raycastTarget = true;

            fadeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
            SetFadeAlpha(0f);
        }

        IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (fadeCanvasGroup == null)
                yield break;

            fadeCanvasGroup.blocksRaycasts = true;

            float start = fadeCanvasGroup.alpha;

            if (duration <= 0f)
            {
                SetFadeAlpha(targetAlpha);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                SetFadeAlpha(Mathf.Lerp(start, targetAlpha, t));
                yield return null;
            }

            SetFadeAlpha(targetAlpha);
        }

        void SetFadeAlpha(float alpha)
        {
            if (fadeCanvasGroup == null) return;
            fadeCanvasGroup.alpha = alpha;
            fadeCanvasGroup.blocksRaycasts = alpha > 0.001f;
        }

        void EnsureResultAboveFade(GameObject canvas)
        {
            if (canvas == null) return;

            var canvasComp = canvas.GetComponent<Canvas>();
            if (canvasComp == null)
                canvasComp = canvas.AddComponent<Canvas>();

            canvasComp.overrideSorting = true;
            canvasComp.sortingOrder = resultSortingOrder;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.AddComponent<GraphicRaycaster>();
        }

        public void ReloadCurrentScene()
        {
            // Result 表示中は GamePauseService で timeScale=0 になっているので、ロード前に必ず解除する。
            GamePauseService.ResetAll();
            Time.timeScale = 1f;

            if (!string.IsNullOrEmpty(replayTargetSceneName))
            {
                SceneManager.LoadScene(replayTargetSceneName);
                return;
            }

            // 遷移先未設定時だけ従来通り現在シーンを再ロードする。
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.name);
        }

        IEnumerator ContinueAfterReward()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || !gameManager.ContinueAfterReward(
                    continueMinimumEnemyDistance,
                    continueEnemyPushDistance,
                    continueMinimumTurns))
            {
                RefreshRewardContinueUI();
                yield break;
            }

            _resultOpen = false;
            StopRewardFrameAnimation();
            if (_rewardContinueButton != null)
                _rewardContinueButton.gameObject.SetActive(false);

            sequencePlayer?.StopSequence();

            if (playerCharacter != null)
                playerCharacter.RestoreFromStaticSprite();

            GameObject canvas = ResolveResultCanvas();
            if (canvas != null)
                canvas.SetActive(false);

            EnsureFadeReady();
            yield return FadeTo(0f, continueFadeInDuration);

            GamePauseService.Resume(PauseHandle);
            SoundManager.PlayBgm(BgmCue.Game);
        }

        void ForceCloseWithoutEvents()
        {
            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            _resultOpen = false;
            StopRewardFrameAnimation();

            var canvas = ResolveResultCanvas();
            if (canvas != null)
                canvas.SetActive(false);

            SetFadeAlpha(0f);
            GamePauseService.Resume(PauseHandle);
        }
    }
}
