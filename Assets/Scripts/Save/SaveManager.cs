using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ChessTemplate.Config;
using ChessTemplate.Core;
using ChessTemplate.Data;

namespace ChessTemplate.Save
{
    public class SaveManager : MonoBehaviour
    {
        private static SaveManager _instance;
        public static SaveManager Instance => _instance;

        [Header("References — set in Inspector")]
        public BoardGenerator board;
        public PieceSpawner spawner;
        public List<PieceConfig> allPieceConfigs; // drag all PieceConfig SOs here

        private string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(SaveDir);
        }

        /// <summary>Save current game to a named slot.</summary>
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

                string json = JsonUtility.ToJson(save, prettyPrint: true);
                File.WriteAllText(SlotPath(slotName), json);

                Debug.Log($"[SaveManager] Saved to slot '{slotName}' → {SlotPath(slotName)}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Load save data from slot (does NOT apply it — returns the data).</summary>
        public SaveData Load(string slotName)
        {
            string path = SlotPath(slotName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] Slot not found: {slotName}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Apply loaded save data to spawner and return restored GameState.</summary>
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

        /// <summary>Returns all existing save slot names.</summary>
        public List<string> GetAllSlots()
        {
            var slots = new List<string>();
            foreach (var file in Directory.GetFiles(SaveDir, "*.json"))
                slots.Add(Path.GetFileNameWithoutExtension(file));
            return slots;
        }

        public bool SlotExists(string slotName) => File.Exists(SlotPath(slotName));

        public void DeleteSlot(string slotName)
        {
            string path = SlotPath(slotName);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Read save metadata without loading full board (for save menu list).</summary>
        public SaveData PeekSave(string slotName)
        {
            // Same as Load — returns full data; UI can display save.savedAt etc.
            return Load(slotName);
        }

        private string SlotPath(string slotName) =>
            Path.Combine(SaveDir, slotName.Replace(" ", "_") + ".json");
    }
}