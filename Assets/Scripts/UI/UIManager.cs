using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BlockBlastGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        public Text turnText;
        public Text scoreText;
        public Text stageText;
        public Text lineProgressText;
        public Text comboText;
        public Image urgencyOverlay;

        [Header("Game Over Panel")]
        public GameObject gameOverPanel;
        public Text gameOverTitle;
        public Text gameOverMessage;
        public Button restartButton;

        [Header("Stage Transition Panel")]
        public GameObject stageTransitionPanel;
        public Text stageClearText;
        public Button[] perkButtons;
        public Text[] perkTexts;

        [Header("Wave / Survival HUD")]
        public Text waveText;
        public Text survivalTimerText;

        [Header("Spaceship Panel")]
        public GameObject spaceshipPanel;
        public Text spaceshipInfoText;
        public Button launchButton;

        [Header("Route HUD")]
        public RectTransform routeParent;
        public Sprite routeBaseSprite;
        public Sprite routeConnectorSprite;
        public Sprite routeCurrentSprite;
        public Sprite routeShopSprite;
        public Sprite routeVendingSprite;
        public Sprite routeCakeSprite;
        public Sprite routeBossSprite;
        public Vector2 routeAnchoredPosition = new Vector2(0f, -260f);
        public Vector2 routeNodeSize = new Vector2(116f, 62f);
        public Vector2 routeEventIconSize = new Vector2(88f, 88f);
        public Vector2 routeCurrentIconSize = new Vector2(96f, 96f);
        public Vector2 routeEventOffset = new Vector2(0f, 18f);
        public Vector2 routeCurrentOffset = new Vector2(0f, 20f);
        public Vector2 routeConnectorSize = new Vector2(56f, 22f);
        public float routeSpacing = 14f;
        [Tooltip("マス移動アニメーションにかける秒数")]
        public float routeMoveDuration = 3f;
        [Tooltip("一度に表示するマスの最大数。0 = 制限なし")]
        public int routeMaxVisibleNodes = 4;

        float blinkTimer;
        bool blinkVisible = true;
        int lastTurnValue;
        RectTransform _routeRoot;
        readonly List<RectTransform> _routeNodeRects = new List<RectTransform>();
        readonly List<Image> _routeNodeBaseImages = new List<Image>();
        readonly List<Image> _routeEventImages = new List<Image>();
        readonly List<Image> _routeConnectorImages = new List<Image>();
        Image _routeCurrentImage;
        Image _routeCurrentBaseImage;
        Image _routeCurrentConnectorImage;
        EnemyWaveData _displayedRouteWaveData;
        int _displayedRouteNodeCount = -1;
        bool _routeDirty = true;

        void OnEnable()
        {
            GameEvents.OnTurnChanged += UpdateTurnDisplay;
            GameEvents.OnScoreChanged += UpdateScoreDisplay;
            GameEvents.OnStageChanged += UpdateStageDisplay;
            GameEvents.OnLineProgress += UpdateLineProgress;
            GameEvents.OnLineClear += ShowComboPopup;
            GameEvents.OnGameOver += ShowGameOver;
            GameEvents.OnStageClear += ShowStageTransition;
            GameEvents.OnSpaceshipBuild += ShowSpaceshipBuild;
            GameEvents.OnWaveStarted += UpdateWaveDisplay;
            GameEvents.OnSurvivalTimerUpdate += UpdateSurvivalTimer;
        }

        void OnDisable()
        {
            GameEvents.OnTurnChanged -= UpdateTurnDisplay;
            GameEvents.OnScoreChanged -= UpdateScoreDisplay;
            GameEvents.OnStageChanged -= UpdateStageDisplay;
            GameEvents.OnLineProgress -= UpdateLineProgress;
            GameEvents.OnLineClear -= ShowComboPopup;
            GameEvents.OnGameOver -= ShowGameOver;
            GameEvents.OnStageClear -= ShowStageTransition;
            GameEvents.OnSpaceshipBuild -= ShowSpaceshipBuild;
            GameEvents.OnWaveStarted -= UpdateWaveDisplay;
            GameEvents.OnSurvivalTimerUpdate -= UpdateSurvivalTimer;
        }

        void Start()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (stageTransitionPanel != null) stageTransitionPanel.SetActive(false);
            if (spaceshipPanel != null) spaceshipPanel.SetActive(false);

            if (restartButton != null)
                restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());

            if (launchButton != null)
                launchButton.onClick.AddListener(OnLaunchSpaceship);
        }

        void Update()
        {
            UpdateUrgencyOverlay();
            UpdateTurnBlink();
            UpdateRouteHud();
        }

        void UpdateTurnDisplay(int turns)
        {
            lastTurnValue = turns;
            if (turnText == null) return;

            turnText.text = $"TURN: {turns}";

            if (turns <= 3)
            {
                blinkTimer = 0f;
                blinkVisible = true;
            }
            else if (turns <= 7)
            {
                turnText.color = new Color(1f, 0.6f, 0f);
            }
            else
            {
                turnText.color = Color.white;
            }
        }

        void UpdateTurnBlink()
        {
            if (turnText == null || lastTurnValue > 3) return;

            blinkTimer += Time.unscaledDeltaTime;
            float blinkSpeed = lastTurnValue <= 1 ? 0.15f : 0.3f;

            if (blinkTimer >= blinkSpeed)
            {
                blinkTimer = 0f;
                blinkVisible = !blinkVisible;
                turnText.color = blinkVisible ? Color.red : new Color(1f, 0.2f, 0.2f, 0.3f);
            }
        }

        void UpdateScoreDisplay(int score)
        {
            if (scoreText != null)
                scoreText.text = $"SCORE: {score}";
        }

        void UpdateStageDisplay(int stage)
        {
            if (stageText != null)
                stageText.text = $"STAGE {stage}/5";

            _routeDirty = true;
        }

        void UpdateLineProgress(int cleared, int target)
        {
            if (lineProgressText == null) return;
            int remaining = Mathf.Max(0, target - cleared);
            lineProgressText.text = $"LINE: {cleared}/{target}";

            if (remaining <= 2)
                lineProgressText.color = new Color(0.3f, 1f, 0.3f);
            else
                lineProgressText.color = Color.white;
        }

        void UpdateUrgencyOverlay()
        {
            if (urgencyOverlay == null) return;

            var chase = GameManager.Instance?.chaseSystem;
            if (chase == null) return;

            float urgency = chase.GetCurrentUrgency();
            Color overlayColor = new Color(1f, 0f, 0f, urgency * 0.15f);
            urgencyOverlay.color = overlayColor;
        }

        void ShowComboPopup(int linesCleared, int comboCount)
        {
            if (comboText == null) return;

            string message = "";
            if (linesCleared >= 2)
                message = $"{linesCleared} LINES! (+{linesCleared * (linesCleared + 1) / 2} turns)";
            if (comboCount > 0)
                message = $"COMBO x{comboCount + 1}! " + message;

            if (!string.IsNullOrEmpty(message))
            {
                comboText.gameObject.SetActive(true);
                comboText.text = message;
                CancelInvoke(nameof(HideComboText));
                Invoke(nameof(HideComboText), 2f);
            }
        }

        void HideComboText()
        {
            if (comboText != null)
                comboText.gameObject.SetActive(false);
        }

        void UpdateWaveDisplay(int waveIndex, int totalWaves)
        {
            if (waveText != null)
                waveText.text = $"WAVE {waveIndex + 1}/{totalWaves}";
        }

        void UpdateSurvivalTimer(float elapsed, float limit)
        {
            if (survivalTimerText == null) return;
            float remaining = Mathf.Max(0f, limit - elapsed);
            int min = (int)(remaining / 60f);
            int sec = (int)(remaining % 60f);
            survivalTimerText.text = $"{min}:{sec:D2}";

            survivalTimerText.color = remaining <= 10f
                ? Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                : Color.white;
        }

        void ShowGameOver(GameOverType type)
        {
            if (gameOverPanel == null) return;

            gameOverPanel.SetActive(true);

            var chase = GameManager.Instance.chaseSystem;
            if (gameOverTitle != null)
                gameOverTitle.text = chase.GetGameOverTitle(type);
            if (gameOverMessage != null)
                gameOverMessage.text = chase.GetGameOverMessage(type);
        }

        void ShowStageTransition()
        {
            if (stageTransitionPanel == null) return;

            stageTransitionPanel.SetActive(true);

            var roguelike = GameManager.Instance.roguelikeSystem;
            var perks = roguelike.GetRandomPerks(3);

            for (int i = 0; i < perkButtons.Length && i < perks.Count; i++)
            {
                int index = i;
                PerkType perk = perks[i];
                perkButtons[i].gameObject.SetActive(true);
                if (perkTexts != null && i < perkTexts.Length)
                    perkTexts[i].text = roguelike.GetPerkDescription(perk);

                perkButtons[i].onClick.RemoveAllListeners();
                perkButtons[i].onClick.AddListener(() =>
                {
                    stageTransitionPanel.SetActive(false);
                    GameManager.Instance.ProceedToNextStage(perk);
                });
            }
        }

        void ShowSpaceshipBuild(List<ItemData> parts)
        {
            if (spaceshipPanel == null) return;

            spaceshipPanel.SetActive(true);

            var titleText = spaceshipPanel.transform.Find("Title")?.GetComponent<Text>();
            if (titleText != null)
                titleText.text = "ゲームクリア";

            if (spaceshipInfoText != null)
                spaceshipInfoText.text = string.Empty;

            if (launchButton != null)
            {
                var buttonText = launchButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                    buttonText.text = "OK";
            }
        }

        // ════════════════════════════════════════
        //  Route HUD  ─  うさぎ固定 / マス消化スクロール
        // ════════════════════════════════════════

        void UpdateRouteHud()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null
                || (gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.LineClearing))
            {
                SetRouteHudVisible(false);
                return;
            }

            var enemySystem = gameManager.enemySystem;
            if (enemySystem == null || enemySystem.RouteNodes == null || enemySystem.RouteNodes.Count == 0)
            {
                SetRouteHudVisible(false);
                return;
            }

            if (!HasRouteSprites())
            {
                SetRouteHudVisible(false);
                return;
            }

            EnsureRouteRoot();
            if (_routeRoot == null)
                return;

            int nodeCount = enemySystem.RouteNodes.Count;
            if (_routeDirty
                || _displayedRouteWaveData != enemySystem.CurrentWaveData
                || _displayedRouteNodeCount != nodeCount)
            {
                RebuildRouteHud(enemySystem);
                _routeDirty = false;
            }

            int consumed = RouteTimelineMath.GetConsumedCount(
                enemySystem.SurvivalElapsed, enemySystem.SurvivalTimeLimit, nodeCount);
            float moveProgress = RouteTimelineMath.GetMoveTravelProgress(
                enemySystem.SurvivalElapsed, enemySystem.SurvivalTimeLimit, nodeCount, routeMoveDuration);

            LayoutRouteScroll(nodeCount, consumed, moveProgress);
            SetRouteHudVisible(true);
        }

        bool HasRouteSprites()
        {
            return routeBaseSprite != null
                && routeConnectorSprite != null
                && routeCurrentSprite != null;
        }

        void EnsureRouteRoot()
        {
            if (routeParent == null)
            {
                var hud = GameObject.Find("HUD");
                if (hud != null)
                    routeParent = hud.GetComponent<RectTransform>();
            }

            if (routeParent == null)
                routeParent = stageText != null ? stageText.canvas.GetComponent<RectTransform>() : null;

            if (routeParent == null)
                return;

            if (_routeRoot != null)
                return;

            var routeObject = new GameObject("RouteHUD", typeof(RectTransform));
            routeObject.transform.SetParent(routeParent, false);
            _routeRoot = routeObject.GetComponent<RectTransform>();
            _routeRoot.anchorMin = new Vector2(0.5f, 1f);
            _routeRoot.anchorMax = new Vector2(0.5f, 1f);
            _routeRoot.pivot = new Vector2(0.5f, 1f);
            _routeRoot.anchoredPosition = routeAnchoredPosition;
            _routeRoot.sizeDelta = new Vector2(800f, 140f);
        }

        void RebuildRouteHud(EnemySystem enemySystem)
        {
            int nodeCount = enemySystem.RouteNodes.Count;
            EnsureRouteVisuals(nodeCount);
            ApplyRouteNodeIcons(enemySystem);

            _displayedRouteWaveData = enemySystem.CurrentWaveData;
            _displayedRouteNodeCount = nodeCount;
        }

        void EnsureRouteVisuals(int nodeCount)
        {
            while (_routeNodeRects.Count < nodeCount)
            {
                int index = _routeNodeRects.Count;

                var nodeObject = new GameObject($"RouteNode_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                nodeObject.transform.SetParent(_routeRoot, false);
                var nodeRect = nodeObject.GetComponent<RectTransform>();
                var nodeImage = nodeObject.GetComponent<Image>();
                nodeImage.preserveAspect = true;
                nodeImage.raycastTarget = false;

                var eventObject = new GameObject($"RouteEvent_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                eventObject.transform.SetParent(nodeRect, false);
                var eventRect = eventObject.GetComponent<RectTransform>();
                eventRect.anchorMin = eventRect.anchorMax = eventRect.pivot = new Vector2(0.5f, 0.5f);
                var eventImage = eventObject.GetComponent<Image>();
                eventImage.preserveAspect = true;
                eventImage.raycastTarget = false;

                _routeNodeRects.Add(nodeRect);
                _routeNodeBaseImages.Add(nodeImage);
                _routeEventImages.Add(eventImage);
            }

            int connectorCount = Mathf.Max(0, nodeCount - 1);
            while (_routeConnectorImages.Count < connectorCount)
            {
                int index = _routeConnectorImages.Count;
                var connectorObject = new GameObject($"RouteConnector_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                connectorObject.transform.SetParent(_routeRoot, false);
                var connectorImage = connectorObject.GetComponent<Image>();
                connectorImage.preserveAspect = true;
                connectorImage.raycastTarget = false;
                _routeConnectorImages.Add(connectorImage);
            }

            if (_routeCurrentBaseImage == null)
            {
                var baseObj = new GameObject("RouteCurrentBase", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                baseObj.transform.SetParent(_routeRoot, false);
                _routeCurrentBaseImage = baseObj.GetComponent<Image>();
                _routeCurrentBaseImage.preserveAspect = true;
                _routeCurrentBaseImage.raycastTarget = false;
            }

            if (_routeCurrentConnectorImage == null)
            {
                var connObj = new GameObject("RouteCurrentConnector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                connObj.transform.SetParent(_routeRoot, false);
                _routeCurrentConnectorImage = connObj.GetComponent<Image>();
                _routeCurrentConnectorImage.preserveAspect = true;
                _routeCurrentConnectorImage.raycastTarget = false;
            }

            if (_routeCurrentImage == null)
            {
                var currentObject = new GameObject("RouteCurrent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                currentObject.transform.SetParent(_routeRoot, false);
                _routeCurrentImage = currentObject.GetComponent<Image>();
                _routeCurrentImage.preserveAspect = true;
                _routeCurrentImage.raycastTarget = false;
            }

            for (int i = 0; i < _routeNodeRects.Count; i++)
                _routeNodeRects[i].gameObject.SetActive(i < nodeCount);

            for (int i = 0; i < _routeConnectorImages.Count; i++)
                _routeConnectorImages[i].gameObject.SetActive(i < connectorCount);
        }

        /// <summary>
        /// 左揃え。うさぎを左端に固定し、最大 routeMaxVisibleNodes 個まで表示。
        /// 1 つ消えると右から次のマスが出てくる。
        /// </summary>
        void LayoutRouteScroll(int nodeCount, int consumedCount, float moveProgress)
        {
            if (_routeRoot == null) return;

            _routeRoot.anchoredPosition = routeAnchoredPosition;

            float stride = routeNodeSize.x + routeConnectorSize.x + routeSpacing;

            // 左揃え ─ ルートルートの左端を基準
            float rootHalfW = _routeRoot.sizeDelta.x * 0.5f;
            float rabbitX = -rootHalfW + routeNodeSize.x * 0.5f;

            // うさぎ開始地点の受け皿
            _routeCurrentBaseImage.rectTransform.anchorMin =
                _routeCurrentBaseImage.rectTransform.anchorMax =
                _routeCurrentBaseImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _routeCurrentBaseImage.rectTransform.anchoredPosition = new Vector2(rabbitX, 0f);
            _routeCurrentBaseImage.rectTransform.sizeDelta = routeNodeSize;

            // うさぎアイコン
            _routeCurrentImage.rectTransform.anchorMin =
                _routeCurrentImage.rectTransform.anchorMax =
                _routeCurrentImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _routeCurrentImage.rectTransform.anchoredPosition = new Vector2(rabbitX, 0) + routeCurrentOffset;
            _routeCurrentImage.rectTransform.sizeDelta = routeCurrentIconSize;
            _routeCurrentImage.transform.SetAsLastSibling();

            // 表示ウィンドウ: consumedCount 〜 windowEnd-1
            int maxVis = routeMaxVisibleNodes > 0 ? routeMaxVisibleNodes : nodeCount;
            int windowEnd = Mathf.Min(consumedCount + maxVis, nodeCount);

            // マス配置
            for (int i = 0; i < nodeCount; i++)
            {
                bool inWindow = i >= consumedCount && i < windowEnd;
                _routeNodeRects[i].gameObject.SetActive(inWindow);

                if (!inWindow) continue;

                int relIdx = i - consumedCount;
                float x = rabbitX + (relIdx + 1 - moveProgress) * stride;

                RectTransform nodeRect = _routeNodeRects[i];
                nodeRect.anchorMin = nodeRect.anchorMax = nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.anchoredPosition = new Vector2(x, 0f);
                nodeRect.sizeDelta = routeNodeSize;

                RectTransform eventRect = _routeEventImages[i].rectTransform;
                eventRect.sizeDelta = routeEventIconSize;
                eventRect.anchoredPosition = routeEventOffset;
            }

            // うさぎ ↔ 最初の可視ノード間のコネクター
            bool hasFirstVisible = consumedCount < windowEnd
                && _routeNodeRects[consumedCount].gameObject.activeSelf;
            _routeCurrentConnectorImage.gameObject.SetActive(hasFirstVisible);
            if (hasFirstVisible)
            {
                float lx = rabbitX + routeNodeSize.x * 0.5f;
                float rx = _routeNodeRects[consumedCount].anchoredPosition.x - routeNodeSize.x * 0.5f;
                RectTransform ccRect = _routeCurrentConnectorImage.rectTransform;
                ccRect.anchorMin = ccRect.anchorMax = ccRect.pivot = new Vector2(0.5f, 0.5f);
                ccRect.anchoredPosition = new Vector2((lx + rx) * 0.5f, 0f);
                ccRect.sizeDelta = routeConnectorSize;
            }

            // ノード間コネクター
            int connectorCount = Mathf.Max(0, nodeCount - 1);
            for (int i = 0; i < connectorCount; i++)
            {
                bool leftVis = _routeNodeRects[i].gameObject.activeSelf;
                bool rightVis = _routeNodeRects[i + 1].gameObject.activeSelf;

                _routeConnectorImages[i].gameObject.SetActive(leftVis && rightVis);

                if (!(leftVis && rightVis)) continue;

                float leftX = _routeNodeRects[i].anchoredPosition.x + routeNodeSize.x * 0.5f;
                float rightX = _routeNodeRects[i + 1].anchoredPosition.x - routeNodeSize.x * 0.5f;
                float connX = (leftX + rightX) * 0.5f;

                RectTransform connRect = _routeConnectorImages[i].rectTransform;
                connRect.anchorMin = connRect.anchorMax = connRect.pivot = new Vector2(0.5f, 0.5f);
                connRect.anchoredPosition = new Vector2(connX, 0f);
                connRect.sizeDelta = routeConnectorSize;
            }
        }

        void ApplyRouteNodeIcons(EnemySystem enemySystem)
        {
            for (int i = 0; i < enemySystem.RouteNodes.Count; i++)
            {
                _routeNodeBaseImages[i].sprite = routeBaseSprite;

                Image eventImage = _routeEventImages[i];
                Sprite eventSprite = GetRouteEventSprite(enemySystem.RouteNodes[i].GetDisplayEventType());
                eventImage.sprite = eventSprite;
                eventImage.enabled = eventSprite != null;
            }

            for (int i = 0; i < _routeConnectorImages.Count; i++)
            {
                _routeConnectorImages[i].sprite = routeConnectorSprite;
                _routeConnectorImages[i].enabled = i < enemySystem.RouteNodes.Count - 1;
            }

            _routeCurrentBaseImage.sprite = routeBaseSprite;
            _routeCurrentBaseImage.enabled = true;

            _routeCurrentConnectorImage.sprite = routeConnectorSprite;
            _routeCurrentConnectorImage.enabled = true;

            _routeCurrentImage.sprite = routeCurrentSprite;
            _routeCurrentImage.enabled = true;
        }

        Sprite GetRouteEventSprite(RouteEventType eventType)
        {
            return eventType switch
            {
                RouteEventType.Shop => routeShopSprite,
                RouteEventType.VendingMachine => routeVendingSprite,
                RouteEventType.Cake => routeCakeSprite,
                RouteEventType.Boss => routeBossSprite,
                _ => null
            };
        }

        void SetRouteHudVisible(bool visible)
        {
            if (_routeRoot != null)
                _routeRoot.gameObject.SetActive(visible);
        }

        void OnLaunchSpaceship()
        {
            if (spaceshipPanel != null)
                spaceshipPanel.SetActive(false);

            GameManager.Instance.ChangeState(GameState.Ending);
        }
    }
}
