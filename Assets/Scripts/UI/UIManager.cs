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
    /// <summary>
    /// Управляет всеми UI-панелями: главное меню, HUD, пауза, save/load, конец игры, промоция.
    /// Прикрепи к Canvas в сцене, назначь все ссылки в Inspector.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Panels ─────────────────────────────────────────────────
        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject hudPanel;
        public GameObject pauseMenuPanel;
        public GameObject saveLoadPanel;
        public GameObject gameOverPanel;

        /// <summary>
        /// Панель промоции пешки.
        /// Должна содержать 4 кнопки с компонентом PromotionButton (или настроить вручную).
        /// Создай в иерархии: Canvas → PromotionPanel → [Queen, Rook, Bishop, Knight] (Button + Image).
        /// </summary>
        [Header("Promotion Panel")]
        public GameObject promotionPanel;

        [Tooltip("Список кнопок выбора фигуры при промоции. " +
                 "Порядок должен совпадать с promotionConfigs.")]
        public List<Button> promotionButtons;

        [Tooltip("Конфиги фигур для промоции в том же порядке, что promotionButtons. " +
                 "Обычно: Queen, Rook, Bishop, Knight.")]
        public List<PieceConfig> promotionConfigs;

        [Tooltip("Image на каждой кнопке промоции — заполняется спрайтом фигуры нужной команды.")]
        public List<Image> promotionButtonImages;

        // ── HUD ────────────────────────────────────────────────────
        [Header("HUD")]
        public TextMeshProUGUI elapsedTimeText;
        public TextMeshProUGUI currentTurnText;
        public List<TextMeshProUGUI> teamTimerTexts;
        public Button pauseButton;

        // ── Pause Menu ─────────────────────────────────────────────
        [Header("Pause Menu")]
        public Button pauseResumeButton;
        public Button pauseSaveButton;
        public Button pauseMainMenuButton;

        // ── Main Menu ──────────────────────────────────────────────
        [Header("Main Menu")]
        public Button newGameButton;
        public Button loadGameButton;
        public Button quitButton;

        // ── Save / Load ────────────────────────────────────────────
        [Header("Save / Load")]
        public Transform slotListParent;
        public GameObject slotEntryPrefab;
        public Button closeSaveLoadButton;

        // ── Game Over ──────────────────────────────────────────────
        [Header("Game Over")]
        public TextMeshProUGUI gameOverText;
        public Button gameOverNewGameButton;
        public Button gameOverMainMenuButton;

        // ── References ─────────────────────────────────────────────
        [Header("References")]
        public TimerManager timerManager;

        // ── Runtime ────────────────────────────────────────────────
        private bool _saveMode;
        private Action<PieceConfig> _onPromotionChosen;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            pauseResumeButton?.onClick.AddListener(() => GameManager.Instance.TogglePause());
            pauseSaveButton?.onClick.AddListener(() => ShowSaveLoad(saveMode: true));
            pauseMainMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());

            newGameButton?.onClick.AddListener(() => GameManager.Instance.StartNewGame());
            loadGameButton?.onClick.AddListener(() => ShowSaveLoad(saveMode: false));
            quitButton?.onClick.AddListener(() => GameManager.Instance.QuitGame());

            closeSaveLoadButton?.onClick.AddListener(HideSaveLoad);

            pauseButton?.onClick.AddListener(() => GameManager.Instance.TogglePause());

            gameOverNewGameButton?.onClick.AddListener(() => GameManager.Instance.StartNewGame());
            gameOverMainMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());

            WirePromotionButtons();

            if (timerManager != null)
            {
                timerManager.OnTeamTimeUpdated.AddListener(OnTeamTimeUpdated);
            }

            HideAll();
        }

        // ── Panel visibility ───────────────────────────────────────

        public void ShowMainMenu() {
            HideAll();
            mainMenuPanel?.SetActive(true);
            GetComponent<ShaderController>().SetShaderPreset(2);
            GetComponent<Animator>().SetTrigger("OpenMenu");
        }
        public void ShowHUD() { HideAll(); hudPanel?.SetActive(true); pauseButton?.gameObject.SetActive(true);
            teamTimerTexts[0].text = "5:00";
            teamTimerTexts[1].text = "5:00"; // temporary solution
        }

        public void ShowPauseMenu() => pauseMenuPanel?.SetActive(true);
        public void HidePauseMenu() => pauseMenuPanel?.SetActive(false);

        public void ShowGameOver(int losingTeam)
        {
            gameOverPanel?.SetActive(true);
            pauseButton?.gameObject.SetActive(false);
            if (gameOverText != null) {
                gameOverText.text = $"{(losingTeam == 0 ? "Black" : "White")} won";
                gameOverText.color = losingTeam == 0 ? Color.black : Color.white;
            }
        }

        public void UpdateTurnDisplay(int teamIndex, string teamName = null)
        {
            if (currentTurnText != null)
                currentTurnText.text = teamName != null
                    ? $"{teamName}'s turn"
                    : $"Team {teamIndex}'s turn";
        }

        // ── Promotion panel ────────────────────────────────────────

        /// <summary>
        /// Показывает панель выбора фигуры для промоции пешки.
        /// teamIndex нужен чтобы показать правильные спрайты (белые/чёрные варианты).
        /// callback вызывается когда игрок кликает на кнопку.
        /// </summary>
        /// <summary>
        /// Возвращает true если промоция полностью настроена в Inspector:
        /// есть хотя бы один конфиг и либо есть панель, либо авто-промоция возможна.
        /// </summary>
        public bool PromotionPanelIsReady()
        {
            return promotionConfigs != null && promotionConfigs.Count > 0;
        }

        public void ShowPromotionPanel(int teamIndex, Action<PieceConfig> callback)
        {
            // Если панель не назначена — авто-промоция в первый конфиг из списка
            if (promotionPanel == null)
            {
                Debug.LogWarning("[UIManager] PromotionPanel не назначен. Авто-промоция в первый конфиг.");
                if (promotionConfigs != null && promotionConfigs.Count > 0)
                    callback?.Invoke(promotionConfigs[0]);
                else
                    Debug.LogError("[UIManager] promotionConfigs пуст — промоция невозможна! " +
                                   "Заполни UIManager.PromotionConfigs в Inspector.");
                return;
            }

            _onPromotionChosen = callback;

            // Обновить спрайты кнопок под нужную команду
            if (promotionButtonImages != null && promotionConfigs != null)
            {
                for (int i = 0; i < promotionButtonImages.Count && i < promotionConfigs.Count; i++)
                {
                    if (promotionButtonImages[i] != null && promotionConfigs[i] != null)
                        promotionButtonImages[i].sprite = promotionConfigs[i].GetSprite(teamIndex);
                }
            }

            promotionPanel.SetActive(true);

            // Пауза времени пока игрок выбирает
            timerManager?.Pause();
        }

        public void HidePromotionPanel()
        {
            promotionPanel?.SetActive(false);
            timerManager?.Resume();
        }

        // ── Save / Load ────────────────────────────────────────────

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
            var btn = go.GetComponentInChildren<Button>();
            btn?.onClick.AddListener(() => onClick());
        }

        private void OnSlotSelected(string slotName)
        {
            if (_saveMode) GameManager.Instance.SaveGame(slotName);
            else GameManager.Instance.LoadGame(slotName);
            HideSaveLoad();
        }

        // ── Timer callbacks ────────────────────────────────────────

        private void OnElapsedUpdated(float elapsed)
        {
            if (elapsedTimeText != null)
                elapsedTimeText.text = TimerManager.FormatTime(elapsed);
        }

        private void OnTeamTimeUpdated(int team, float remaining)
        {
            if (teamTimerTexts != null && team < teamTimerTexts.Count && teamTimerTexts[team] != null)
                teamTimerTexts[team].text = TimerManager.FormatTime(remaining);
        }

        // ── Keyboard ───────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                GameManager.Instance.TogglePause();
        }

        // ── Internal ───────────────────────────────────────────────

        /// <summary>
        /// Привязывает кнопки промоции к конфигам.
        /// Индекс кнопки = индекс конфига в promotionConfigs.
        /// </summary>
        private void WirePromotionButtons()
        {
            if (promotionButtons == null || promotionConfigs == null) return;

            for (int i = 0; i < promotionButtons.Count && i < promotionConfigs.Count; i++)
            {
                int idx = i;
                Button button = promotionButtons[i];
                button?.onClick.AddListener(() =>
                {
                    var chosen = promotionConfigs[idx];
                    _onPromotionChosen?.Invoke(chosen);
                });
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