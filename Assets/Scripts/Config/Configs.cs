using System.Collections.Generic;
using UnityEngine;

namespace ChessTemplate.Config
{
    [CreateAssetMenu(menuName = "ChessTemplate/BoardConfig", fileName = "BoardConfig")]
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

        [Tooltip("Опционально: спрайты вместо цветов. typeIndex 0 = светлая, 1 = тёмная.")]
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
        public int typeIndex; // 0 - white, 1 - black
        public Sprite sprite;
    }


    [System.Serializable]
    public class MovePattern
    {
        [Tooltip("Смещение по строкам за один шаг.")]
        public int dRow;

        [Tooltip("Смещение по столбцам за один шаг.")]
        public int dCol;

        [Tooltip(
            "Применять множитель направления команды к dCol.\n\n" +
            "FALSE (по умолчанию) — dCol абсолютный. Используй для:\n" +
            "  • Ладья, слон, ферзь, конь, король — у них паттерны уже покрывают ОБА направления\n" +
            "    (отдельные записи с +dCol и -dCol).\n\n" +
            "TRUE — dCol зеркалится для команды 1. Используй для:\n" +
            "  • Пешка: захват по диагонали (dRow=1, dCol=1) — для чёрных станет dRow=-1, dCol=-1.\n" +
            "  • Любой паттерн где смысл 'вправо от игрока', а не абсолютное направление.")]
        public bool applyDirectionToCol = false;

        [Tooltip("Скользящий ход (повторяется до упора): ладья, слон, ферзь. False = один шаг.")]
        public bool slide = false;

        [Tooltip("Может захватывать вражескую фигуру на целевой клетке.")]
        public bool canCapture = true;

        [Tooltip("Может ходить на пустую клетку. False = только захват (диагональ пешки).")]
        public bool canMoveEmpty = true;

        [Tooltip("Разрешён только если фигура ещё не ходила (двойной ход пешки).")]
        public bool firstMoveOnly = false;
    }

    [CreateAssetMenu(menuName = "ChessTemplate/PieceConfig", fileName = "PieceConfig")]
    public class PieceConfig : ScriptableObject
    {
        [Header("Identity")]
        public string pieceId = "pawn";
        public string pieceName = "Pawn";

        [Header("Visuals")]
        public Sprite spriteTeam0;
        [Tooltip("Спрайт для команды 1. Если пусто — берётся spriteTeam0 с тинтом team1Tint.")]
        public Sprite spriteTeam1;
        public Color team1Tint = new Color(0.15f, 0.15f, 0.15f);
        public Vector2 spriteOffset = Vector2.zero;
        public float spriteScale = 0.85f;

        [Header("Move patterns")]
        public List<MovePattern> movePatterns = new();

        [Header("Special flags")]
        public bool canPromote;      // пешка — промоция
        public bool isRoyal;         // король — определяет шах/мат
        public bool canCastleWith;   // ладья — партнёр рокировки

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

    [CreateAssetMenu(menuName = "ChessTemplate/GameSetupConfig", fileName = "GameSetupConfig")]
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