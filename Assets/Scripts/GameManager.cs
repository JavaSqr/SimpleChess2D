using UnityEngine;
using SimpleChess.Audio;
using SimpleChess.Config;
using SimpleChess.Core;
using SimpleChess.Data;
using SimpleChess.Logic;
using SimpleChess.Save;
using SimpleChess.UI;

namespace SimpleChess
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Default config")]
        public GameSetupConfig defaultSetupConfig;

        [Header("Scene references")]
        public BoardGenerator board;
        public PieceSpawner spawner;
        public MoveValidator validator;
        public TurnManager turns;
        public SelectionHandler selection;
        public TimerManager timer;
        public BoardFlipper boardFlipper;
        public AudioManager audioManager;
        public SaveManager saveManager;
        public UIManager ui;

        public GameState State { get; private set; } = new GameState();
        public GameSetupConfig ActiveSetup { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            turns.OnTurnChanged.AddListener(OnTurnChanged);
            selection.OnMoveMade.AddListener(OnMoveMade);

            State.Phase = GamePhase.MainMenu;
            selection.SetActive(false);
            ui?.ShowMainMenu();
            audioManager?.PlayMenuMusic();
        }

        public void StartNewGame(GameSetupConfig setup = null)
        {
            ActiveSetup = setup != null ? setup : defaultSetupConfig;
            if (ActiveSetup == null) { Debug.LogError("[GameManager] No GameSetupConfig assigned!"); return; }

            board.GenerateBoard(ActiveSetup.boardConfig);
            spawner.SpawnFromConfig(ActiveSetup);

            validator.enableCastling = ActiveSetup.enableCastling;
            validator.enableEnPassant = ActiveSetup.enableEnPassant;

            turns.setupConfig = ActiveSetup;
            turns.StartGame();

            timer.Init(ActiveSetup.teams.Count, ActiveSetup.startingTeamTurn);
            timer.StartTimer();

            boardFlipper?.ResetOrientation();

            State = new GameState { Phase = GamePhase.Playing };
            selection.SetActive(true);

            audioManager?.OnGameStart();
            audioManager?.PlayGameMusic();
            ui?.ShowHUD();
        }

        public void TogglePause()
        {
            if (State.Phase == GamePhase.Playing)
            {
                State.Phase = GamePhase.Paused;
                timer.Pause();
                selection.SetActive(false);
                ui?.ShowPauseMenu();
            }
            else if (State.Phase == GamePhase.Paused)
            {
                State.Phase = GamePhase.Playing;
                timer.Resume();
                selection.SetActive(true);
                ui?.HidePauseMenu();
            }
        }

        public bool SaveGame(string slotName)
            => saveManager.Save(slotName, State, ActiveSetup?.boardConfig?.boardId, ActiveSetup?.setupId);

        public bool LoadGame(string slotName)
        {
            var data = saveManager.Load(slotName);
            if (data == null) return false;

            var setupToUse = ActiveSetup ?? defaultSetupConfig;
            board.GenerateBoard(setupToUse != null ? setupToUse.boardConfig : null);

            var newState = saveManager.ApplySave(data);
            if (newState == null) return false;

            State = newState;
            State.Phase = GamePhase.Playing;

            turns.SetTeam(data.currentTeamTurn);
            timer.Init(setupToUse?.teams.Count ?? 2, data.currentTeamTurn);
            timer.StartTimer();

            boardFlipper?.ResetOrientation();
            selection.SetActive(true);
            audioManager?.PlayGameMusic();
            ui?.ShowHUD();
            return true;
        }

        public void ReturnToMainMenu()
        {
            timer.Pause();
            selection.SetActive(false);
            spawner.ClearAll();
            board.ClearBoard();
            boardFlipper?.ResetOrientation();

            State.Phase = GamePhase.MainMenu;
            audioManager?.PlayMenuMusic();
            ui?.ShowMainMenu();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnTurnChanged(int team)
        {
            State.TurnTeam = team;
            timer.SetTeam(team);
            ui?.UpdateTurnDisplay(team,
                ActiveSetup?.teams.Count > team ? ActiveSetup.teams[team].teamName : null);
        }

        private void OnMoveMade(MoveRecord record) => State.History.Add(record);

        public void OnCheckMate(int losingTeam)
        {
            State.Phase = GamePhase.GameOver;
            timer.Pause();
            selection.SetActive(false);
            audioManager?.OnGameOver();
            audioManager?.PlayGameOverMusic();
            ui?.ShowGameOver(losingTeam);
        }

        public void OnTimeExpired(int losingTeam)
        {
            State.Phase = GamePhase.GameOver;
            timer.Pause();
            selection.SetActive(false);
            audioManager?.OnGameOver();
            audioManager?.PlayGameOverMusic();
            ui?.ShowGameOver(losingTeam);
        }
    }
}