using UnityEngine;
using ChessTemplate.Config;
using ChessTemplate.Data;

namespace ChessTemplate.Core
{
    /// <summary>
    /// Runtime компонент на каждой фигуре.
    /// Добавляется автоматически PieceSpawner'ом — вручную не добавлять.
    ///
    /// spriteOffset из PieceConfig сохраняется в _cellOffset и прибавляется
    /// к позиции клетки при каждом MoveTo — offset не теряется после ходов.
    /// </summary>
    public class Piece : MonoBehaviour
    {
        public PieceConfig Config { get; private set; }
        public int TeamIndex { get; private set; }
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool HasMoved { get; private set; }
        public bool IsDragging { get; private set; }

        private SpriteRenderer _sr;
        private Vector3 _cellOffset;      // сохранённый spriteOffset в мировых координатах
        private Vector3 _homePosition;    // позиция на клетке + offset (для отмены drag)
        private int _baseSortingOrder;
        private string _baseSortingLayer;

        [Tooltip("Во сколько раз фигура увеличивается при перетаскивании.")]
        public float dragScaleMultiplier = 1.25f;

        // ── Init ───────────────────────────────────────────────────

        public void Init(PieceConfig config, int teamIndex, int row, int col,
                         string sortingLayer = "Pieces", int sortingOrder = 2)
        {
            Config = config;
            TeamIndex = teamIndex;
            Row = row;
            Col = col;
            HasMoved = false;

            _baseSortingLayer = sortingLayer;
            _baseSortingOrder = sortingOrder;

            _sr = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingLayerName = sortingLayer;
            _sr.sortingOrder = sortingOrder;
            _sr.sprite = config.GetSprite(teamIndex);

            if (_sr.sprite == null)
                Debug.LogWarning($"[Piece] Нет спрайта для '{config.pieceId}' team {teamIndex}");

            if (teamIndex != 0 && config.spriteTeam1 == null)
                _sr.color = config.team1Tint;

            transform.localScale = Vector3.one * config.spriteScale;

            // Сохраняем offset отдельно — он будет применяться при каждом MoveTo
            _cellOffset = (Vector3)config.spriteOffset;

            // Применяем offset к начальной позиции
            _homePosition = transform.position + _cellOffset;
            transform.position = _homePosition;
        }

        // ── Movement ───────────────────────────────────────────────

        /// <summary>
        /// Переместить фигуру на клетку (row, col).
        /// worldPos — центр клетки из CellToWorld; offset добавляется здесь автоматически.
        /// </summary>
        public void MoveTo(int row, int col, Vector3 cellWorldPos)
        {
            Row = row;
            Col = col;
            _homePosition = cellWorldPos + _cellOffset;
            transform.position = _homePosition;
            HasMoved = true;
        }

        // ── Drag API ───────────────────────────────────────────────

        public void BeginDrag()
        {
            if (IsDragging) return;
            IsDragging = true;

            transform.localScale = Vector3.one * Config.spriteScale * dragScaleMultiplier;
            _sr.sortingOrder = _baseSortingOrder + 10;
        }

        /// <summary>worldPos — мировые координаты курсора (без offset, drag следует за мышью точно).</summary>
        public void UpdateDragPosition(Vector3 worldPos)
        {
            if (!IsDragging) return;
            transform.position = new Vector3(worldPos.x + Config.spriteOffset.x, worldPos.y + Config.spriteOffset.y, transform.position.z);
        }

        public void EndDrag()
        {
            if (!IsDragging) return;
            IsDragging = false;

            transform.localScale = Vector3.one * Config.spriteScale;
            _sr.sortingOrder = _baseSortingOrder;
        }

        /// <summary>Отменить drag — фигура возвращается на клетку с учётом offset.</summary>
        public void CancelDrag()
        {
            EndDrag();
            transform.position = _homePosition;
        }

        // ── Data ───────────────────────────────────────────────────

        public PieceData ToData() => new PieceData
        {
            pieceConfigId = Config.pieceId,
            row = Row,
            col = Col,
            teamIndex = TeamIndex,
            hasMoved = HasMoved
        };

        public void MarkAsMoved() => HasMoved = true;

        public void RestoreFromData(PieceData d, BoardGenerator board)
        {
            Row = d.row;
            Col = d.col;
            HasMoved = d.hasMoved;
            _homePosition = board.CellToWorld(d.row, d.col) + _cellOffset;
            transform.position = _homePosition;
        }
    }
}