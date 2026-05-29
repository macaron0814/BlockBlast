using System.Collections;
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

        [Tooltip("リザルト画面内のボタン。押すと現在のシーンを再ロードする。")]
        public Button resultReloadButton;

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

        Coroutine _sequenceRoutine;

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
            ResolveReferences();
            BindResultButton(true);

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
            BindResultButton(false);
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

        void BindResultButton(bool subscribe)
        {
            if (resultReloadButton == null) return;

            resultReloadButton.onClick.RemoveListener(ReloadCurrentScene);
            if (subscribe)
                resultReloadButton.onClick.AddListener(ReloadCurrentScene);
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
            var canvas = ResolveResultCanvas();
            if (canvas != null)
            {
                EnsureResultAboveFade(canvas);
                canvas.SetActive(true);
                canvas.transform.SetAsLastSibling();
            }

            if (sequencePlayer != null && !sequencePlayer.playOnEnable)
                sequencePlayer.PlaySequence();

            _sequenceRoutine = null;
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
            if (enemySystem == null && GameManager.Instance != null)
                enemySystem = GameManager.Instance.enemySystem;

            if (enemySystem != null)
            {
                // リザルトでは敵を消さず、追いつかれた瞬間の見た目を残したまま止める。
                // GamePauseService.Pause で Time.timeScale=0 になるため、敵の移動や道の回転は停止する。
                enemySystem.PauseSurvivalForShop();
            }

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

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.name);
        }

        void ForceCloseWithoutEvents()
        {
            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            _resultOpen = false;

            var canvas = ResolveResultCanvas();
            if (canvas != null)
                canvas.SetActive(false);

            SetFadeAlpha(0f);
            GamePauseService.Resume(PauseHandle);
        }
    }
}
