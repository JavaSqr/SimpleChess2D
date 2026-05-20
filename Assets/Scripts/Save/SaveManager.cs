using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SimpleChess.Config;
using SimpleChess.Core;
using SimpleChess.Data;

namespace SimpleChess.Save
{
    public class SaveManager : MonoBehaviour
    {
        private static SaveManager _instance;
        public static SaveManager Instance => _instance;

        [Header("References")]
        public BoardGenerator board;
        public PieceSpawner spawner;
        [Tooltip("Drag all PieceConfig assets here. Required for loading saves.")]
        public List<PieceConfig> allPieceConfigs;

        private string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(SaveDir);
        }

        public bool Save(string slotName, GameState state, string boardConfigId, string gameSetupId)
        {
            try
            {
                var save = new SaveData
                {
                    saveId = slotName,
                    savedAt = DateTime.UtcNow.ToString("O"),
                    boardConfigId = boardConfigId,
                    gameSetupConfigId = gameSetupId,
                    currentTeamTurn = state.TurnTeam,
                    elapsedSeconds = state.ElapsedSeconds,
                    isPaused = state.Phase == GamePhase.Paused
                };

                save.board.rows = board.Rows;
                save.board.cols = board.Cols;
                save.board.pieces = spawner.GetAllPieceData();
                save.moveHistory = new List<MoveRecord>(state.History);

                File.WriteAllText(SlotPath(slotName), JsonUtility.ToJson(save, prettyPrint: true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                return false;
            }
        }

        public SaveData Load(string slotName)
        {
            string path = SlotPath(slotName);
            if (!File.Exists(path)) { Debug.LogWarning($"[SaveManager] Slot not found: {slotName}"); return null; }

            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogError($"[SaveManager] Load failed: {e.Message}"); return null; }
        }

        public GameState ApplySave(SaveData save)
        {
            if (save == null) return null;

            spawner.SpawnFromSave(save.board.pieces, allPieceConfigs);

            var state = new GameState
            {
                Phase = save.isPaused ? GamePhase.Paused : GamePhase.Playing,
                TurnTeam = save.currentTeamTurn,
                ElapsedSeconds = save.elapsedSeconds
            };
            state.History.AddRange(save.moveHistory);
            return state;
        }

        public List<string> GetAllSlots()
        {
            var slots = new List<string>();
            foreach (var file in Directory.GetFiles(SaveDir, "*.json"))
                slots.Add(Path.GetFileNameWithoutExtension(file));
            return slots;
        }

        public bool SlotExists(string slotName) => File.Exists(SlotPath(slotName));
        public void DeleteSlot(string slotName) { var p = SlotPath(slotName); if (File.Exists(p)) File.Delete(p); }
        public SaveData PeekSave(string slotName) => Load(slotName);

        private string SlotPath(string slotName) =>
            Path.Combine(SaveDir, slotName.Replace(" ", "_") + ".json");
    }
}