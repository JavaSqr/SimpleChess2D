using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessTemplate.Data
{
    // ─────────────────────────────────────────────
    //  Piece data stored in JSON save file
    // ─────────────────────────────────────────────
    [Serializable]
    public class PieceData
    {
        public string pieceConfigId;   // matches PieceConfig.pieceId
        public int row;
        public int col;
        public int teamIndex;          // 0 = white, 1 = black (or custom)
        public bool hasMoved;
    }

    // ─────────────────────────────────────────────
    //  Board state snapshot
    // ─────────────────────────────────────────────
    [Serializable]
    public class BoardData
    {
        public int rows;
        public int cols;
        public List<PieceData> pieces = new();
    }

    // ─────────────────────────────────────────────
    //  Тип хода — нужен для корректного исполнения
    //  спецходов в SelectionHandler
    // ─────────────────────────────────────────────
    public enum MoveType
    {
        Normal,
        Castling,    // король + ладья двигаются вместе
        EnPassant,   // пешка бьёт на проходе; захваченная пешка не на toRow/toCol
        Promotion    // пешка достигла последнего ряда
    }

    // ─────────────────────────────────────────────
    //  Single move record (for history / undo)
    // ─────────────────────────────────────────────
    [Serializable]
    public class MoveRecord
    {
        public string pieceConfigId;
        public int teamIndex;
        public int fromRow, fromCol;
        public int toRow, toCol;
        public MoveType moveType;
        public bool capturedPiece;
        public PieceData capturedPieceData;  // null если захвата не было

        // Рокировка: позиция ладьи до и после
        public int rookFromCol;
        public int rookToCol;

        // Эн пасант: строка/столбец захваченной пешки (отличается от toRow/toCol)
        public int enPassantCaptureRow;
        public int enPassantCaptureCol;

        public float timestamp;
    }

    // ─────────────────────────────────────────────
    //  Full game save file
    // ─────────────────────────────────────────────
    [Serializable]
    public class SaveData
    {
        public string saveId;
        public string savedAt;             // ISO-8601
        public string boardConfigId;
        public string gameSetupConfigId;
        public int currentTeamTurn;
        public float elapsedSeconds;
        public bool isPaused;
        public BoardData board = new();
        public List<MoveRecord> moveHistory = new();
    }

    // ─────────────────────────────────────────────
    //  Runtime game state (not serialized directly)
    // ─────────────────────────────────────────────
    public enum GamePhase { MainMenu, Setup, Playing, Paused, GameOver }

    public class GameState
    {
        public GamePhase Phase { get; set; } = GamePhase.MainMenu;
        public int TurnTeam { get; set; } = 0;
        public bool IsCheck { get; set; }
        public bool IsCheckMate { get; set; }
        public float ElapsedSeconds { get; set; }
        public List<MoveRecord> History { get; } = new();
    }
}