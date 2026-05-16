using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChessTemplate.Config;
using ChessTemplate.Logic;
using ChessTemplate.Save;

namespace ChessTemplate.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject hudPanel;
        public GameObject pauseMenuPanel;
        public GameObject saveLoadPanel;
        public GameObject gameOverPanel;

        [Header("Promotion Panel")]
        public GameObject promotionPanel;
        public List<Button> promotionButtons;
        [Tooltip("Must match promotionButtons order. Typically: Queen, Rook, Bishop, Knight.")]
        public List<PieceConfig> promotionConfigs;
        public List<Image> promotionButtonImages;

        [Header("HUD")]
        public TextMeshProUGUI elapsedTimeText;
        public TextMeshProUGUI currentTurnText;
        public List<TextMeshProUGUI> teamTimerTexts;
        public Button pauseButton;

        [Header("Pause Menu")]
        public Button pauseResumeButton;
        public Button pauseSaveButton;
        public Button pauseMainMenuButton;

        [Header("Main Menu")]
        public Button newGameButton;
        public Button loadGameButton;
        public Button quitButton;

        [Header("Save / Load")]
        public Transform slotListParent;
        public GameObject slotEntryPrefab;
        public Button closeSaveLoadButton;

        [Header("Game Over")]
        public TextMeshProUGUI gameOverText;
        public Button gameOverNewGameButton;
        public Button gameOverMainMenuButton;

        [Header("References")]
        public TimerManager timerManager;

        private bool _saveMode;
        private Action<PieceConfig> _onPromotionChosen;

        private void Awake()
        {
            pauseResumeButton?.onClick.AddListener(() => GameManager.Instance.TogglePause());
            pauseSaveButton?.onClick.AddListener(() => ShowSaveLoad(saveMode: true));
            pauseMainMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());
            pauseButton?.onClick.AddListener(() => GameManager.Instance.TogglePause());

            newGameButton?.onClick.AddListener(() => GameManager.Instance.StartNewGame());
            loadGameButton?.onClick.AddListener(() => ShowSaveLoad(saveMode: false));
            quitButton?.onClick.AddListener(() => GameManager.Instance.QuitGame());

            closeSaveLoadButton?.onClick.AddListener(HideSaveLoad);

            gameOverNewGameButton?.onClick.AddListener(() => GameManager.Instance.StartNewGame());
            gameOverMainMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());

            WirePromotionButtons();

            if (timerManager != null)
                timerManager.OnTeamTimeUpdated.AddListener(OnTeamTimeUpdated);

            HideAll();
        }

        public void ShowMainMenu()
        {
            HideAll();
            mainMenuPanel?.SetActive(true);
            GetComponent<ShaderController>().SetShaderPreset(2);
            GetComponent<Animator>().SetTrigger("OpenMenu");
        }

        public void ShowHUD()
        {
            HideAll();
            hudPanel?.SetActive(true);
            pauseButton?.gameObject.SetActive(true);
            if (teamTimerTexts?.Count >= 2)
            {
                teamTimerTexts[0].text = "5:00";
                teamTimerTexts[1].text = "5:00";
            }
        }

        public void ShowPauseMenu() => pauseMenuPanel?.SetActive(true);
        public void HidePauseMenu() => pauseMenuPanel?.SetActive(false);

        public void ShowGameOver(int losingTeam)
        {
            gameOverPanel?.SetActive(true);
            pauseButton?.gameObject.SetActive(false);
            if (gameOverText != null)
            {
                gameOverText.text = losingTeam == 0 ? "Black won" : "White won";
                gameOverText.color = losingTeam == 0 ? Color.black : Color.white;
            }
        }

        public void UpdateTurnDisplay(int teamIndex, string teamName = null)
        {
            if (currentTurnText != null)
                currentTurnText.text = teamName != null ? $"{teamName}'s turn" : $"Team {teamIndex}'s turn";
        }

        public bool PromotionPanelIsReady() => promotionConfigs != null && promotionConfigs.Count > 0;

        public void ShowPromotionPanel(int teamIndex, Action<PieceConfig> callback)
        {
            if (promotionPanel == null)
            {
                Debug.LogWarning("[UIManager] PromotionPanel not assigned. Auto-promoting.");
                if (promotionConfigs != null && promotionConfigs.Count > 0)
                    callback?.Invoke(promotionConfigs[0]);
                else
                    Debug.LogError("[UIManager] promotionConfigs is empty.");
                return;
            }

            _onPromotionChosen = callback;

            if (promotionButtonImages != null && promotionConfigs != null)
                for (int i = 0; i < promotionButtonImages.Count && i < promotionConfigs.Count; i++)
                    if (promotionButtonImages[i] != null && promotionConfigs[i] != null)
                        promotionButtonImages[i].sprite = promotionConfigs[i].GetSprite(teamIndex);

            promotionPanel.SetActive(true);
            timerManager?.Pause();
        }

        public void HidePromotionPanel()
        {
            promotionPanel?.SetActive(false);
            timerManager?.Resume();
        }

        public void ShowSaveLoad(bool saveMode)
        {
            _saveMode = saveMode;
            saveLoadPanel?.SetActive(true);
            RefreshSlotList();
        }

        public void HideSaveLoad() => saveLoadPanel?.SetActive(false);

        private void RefreshSlotList()
        {
            if (slotListParent == null) return;

            foreach (Transform child in slotListParent)
                Destroy(child.gameObject);

            var slots = SaveManager.Instance?.GetAllSlots() ?? new List<string>();

            if (_saveMode)
            {
                string newSlot = $"Save_{DateTime.Now:yyyyMMdd_HHmmss}";
                CreateSlotEntry("[ New Save ]", () => OnSlotSelected(newSlot));
            }

            foreach (var slot in slots)
            {
                string captured = slot;
                var peek = SaveManager.Instance?.PeekSave(slot);
                string label = peek != null ? $"{slot}  |  {peek.savedAt[..10]}" : slot;
                CreateSlotEntry(label, () => OnSlotSelected(captured));
            }
        }

        private void CreateSlotEntry(string label, Action onClick)
        {
            if (slotEntryPrefab == null || slotListParent == null) return;
            var go = Instantiate(slotEntryPrefab, slotListParent);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;
            go.GetComponentInChildren<Button>()?.onClick.AddListener(() => onClick());
        }

        private void OnSlotSelected(string slotName)
        {
            if (_saveMode) GameManager.Instance.SaveGame(slotName);
            else GameManager.Instance.LoadGame(slotName);
            HideSaveLoad();
        }

        private void OnTeamTimeUpdated(int team, float remaining)
        {
            if (teamTimerTexts != null && team < teamTimerTexts.Count && teamTimerTexts[team] != null)
                teamTimerTexts[team].text = TimerManager.FormatTime(remaining);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                GameManager.Instance.TogglePause();
        }

        private void WirePromotionButtons()
        {
            if (promotionButtons == null || promotionConfigs == null) return;
            for (int i = 0; i < promotionButtons.Count && i < promotionConfigs.Count; i++)
            {
                int idx = i;
                promotionButtons[i]?.onClick.AddListener(() => _onPromotionChosen?.Invoke(promotionConfigs[idx]));
            }
        }

        private void HideAll()
        {
            mainMenuPanel?.SetActive(false);
            hudPanel?.SetActive(false);
            pauseMenuPanel?.SetActive(false);
            saveLoadPanel?.SetActive(false);
            gameOverPanel?.SetActive(false);
            promotionPanel?.SetActive(false);
        }
    }
}