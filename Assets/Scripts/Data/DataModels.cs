using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleChess.Data
{
    [Serializable]
    public class PieceData
    {
        public string pieceConfigId;
        public int row;
        public int col;
        public int teamIndex;
        public bool hasMoved;
    }

    [Serializable]
    public class BoardData
    {
        public int rows;
        public int cols;
        public List<PieceData> pieces = new();
    }

    public enum MoveType
    {
        Normal,
        Castling,   // king + rook both move
        EnPassant,  // captured pawn is NOT at toRow/toCol
        Promotion
    }

    [Serializable]
    public class MoveRecord
    {
        public string pieceConfigId;
        public int teamIndex;
        public int fromRow, fromCol;
        public int toRow, toCol;
        public MoveType moveType;
        public bool capturedPiece;
        public PieceData capturedPieceData;

        public int rookFromCol;
        public int rookToCol;

        public int enPassantCaptureRow;
        public int enPassantCaptureCol;

        public float timestamp;
    }

    [Serializable]
    public class SaveData
    {
        public string saveId;
        public string savedAt;
        public string boardConfigId;
        public string gameSetupConfigId;
        public int currentTeamTurn;
        public float elapsedSeconds;
        public bool isPaused;
        public BoardData board = new();
        public List<MoveRecord> moveHistory = new();
    }

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