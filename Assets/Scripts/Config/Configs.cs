using System.Collections.Generic;
using UnityEngine;

namespace SimpleChess.Config
{
    [CreateAssetMenu(menuName = "SimpleChess/BoardConfig", fileName = "BoardConfig")]
    public class BoardConfig : ScriptableObject
    {
        [Header("Identity")]
        public string boardId = "classic";

        [Header("Grid size")]
        [Min(2)] public int rows = 8;
        [Min(2)] public int cols = 8;
        public float cellSize = 1f;

        [Header("Cell visuals")]
        public Color lightCellColor = Color.white;
        public Color darkCellColor = new Color(0.35f, 0.24f, 0.10f);
        public List<CellSpriteEntry> cellSprites = new();

        [Header("Highlight colors")]
        public Color selectedColor = new Color(1f, 0.9f, 0f, 0.5f);
        public Color validMoveColor = new Color(0f, 1f, 0f, 0.35f);
        public Color captureColor = new Color(1f, 0f, 0f, 0.35f);
        public Color checkColor = new Color(1f, 0.3f, 0f, 0.55f);

        [Header("Board border")]
        public bool showBorder = true;
        public Color borderColor = Color.black;
        public float borderWidth = 0.05f;

        public Sprite GetCellSprite(bool isLight)
        {
            int idx = isLight ? 0 : 1;
            foreach (var entry in cellSprites)
                if (entry.typeIndex == idx) return entry.sprite;
            return null;
        }
    }

    [System.Serializable]
    public class CellSpriteEntry
    {
        public int typeIndex; // 0 = light, 1 = dark
        public Sprite sprite;
    }

    [System.Serializable]
    public class MovePattern
    {
        public int dRow;
        public int dCol;
        [Tooltip("If true, dCol is flipped for team 1 (use for pawn diagonal attacks).")]
        public bool applyDirectionToCol = false;
        public bool slide = false;
        public bool canCapture = true;
        public bool canMoveEmpty = true;
        public bool firstMoveOnly = false;
    }

    [CreateAssetMenu(menuName = "SimpleChess/PieceConfig", fileName = "PieceConfig")]
    public class PieceConfig : ScriptableObject
    {
        [Header("Identity")]
        public string pieceId = "pawn";
        public string pieceName = "Pawn";

        [Header("Visuals")]
        public Sprite spriteTeam0;
        [Tooltip("If empty, spriteTeam0 is used with team1Tint applied.")]
        public Sprite spriteTeam1;
        public Color team1Tint = new Color(0.15f, 0.15f, 0.15f);
        public Vector2 spriteOffset = Vector2.zero;
        public float spriteScale = 0.85f;

        [Header("Move patterns")]
        public List<MovePattern> movePatterns = new();

        [Header("Special flags")]
        public bool canPromote;    // pawn promotion
        public bool isRoyal;       // losing this piece = check/mate
        public bool canCastleWith; // rook castling partner

        [Header("Value")]
        public int pointValue = 1;

        public Sprite GetSprite(int teamIndex)
        {
            if (teamIndex == 0 || spriteTeam1 == null) return spriteTeam0;
            return spriteTeam1;
        }
    }

    [System.Serializable]
    public class PiecePlacement
    {
        public PieceConfig piece;
        public int row;
        public int col;
        public int teamIndex;
    }

    [System.Serializable]
    public class TeamConfig
    {
        public string teamName = "White";
        public Color teamColor = Color.white;
        public int teamIndex = 0;
    }

    [CreateAssetMenu(menuName = "SimpleChess/GameSetupConfig", fileName = "GameSetupConfig")]
    public class GameSetupConfig : ScriptableObject
    {
        [Header("Identity")]
        public string setupId = "classic";
        public string setupName = "Classic Chess";

        [Header("Board")]
        public BoardConfig boardConfig;

        [Header("Teams")]
        public List<TeamConfig> teams = new();

        [Header("Initial placement")]
        public List<PiecePlacement> placements = new();

        [Header("Rules")]
        public bool enableCastling = true;
        public bool enableEnPassant = true;
        public bool enablePromotion = true;
        public int startingTeamTurn = 0;
    }
}