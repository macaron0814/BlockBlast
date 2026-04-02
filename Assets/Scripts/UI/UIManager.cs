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

        [Header("Spaceship Panel")]
        public GameObject spaceshipPanel;
        public Text spaceshipInfoText;
        public Button launchButton;

        float blinkTimer;
        bool blinkVisible = true;
        int lastTurnValue;

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

            if (spaceshipInfoText != null)
            {
                if (parts == null || parts.Count == 0)
                    spaceshipInfoText.text = "Normal spaceship complete!\nEscaping Earth without items!";
                else
                    spaceshipInfoText.text = $"Spaceship parts: {parts.Count}\nSpecial spaceship complete!";
            }
        }

        void OnLaunchSpaceship()
        {
            if (spaceshipPanel != null)
                spaceshipPanel.SetActive(false);

            GameManager.Instance.ChangeState(GameState.Ending);
        }
    }
}
