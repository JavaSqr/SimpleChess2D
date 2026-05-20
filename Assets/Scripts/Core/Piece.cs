using UnityEngine;
using SimpleChess.Config;
using SimpleChess.Data;

namespace SimpleChess.Core
{
    public class Piece : MonoBehaviour
    {
        public PieceConfig Config { get; private set; }
        public int TeamIndex { get; private set; }
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool HasMoved { get; private set; }
        public bool IsDragging { get; private set; }

        private SpriteRenderer _sr;
        private int _baseSortingOrder;
        private BoardGenerator _board;
        private Vector3 _cellOffset;
        public float dragScaleMultiplier = 1.25f;

        public void Init(PieceConfig config, int teamIndex, int row, int col,
                         BoardGenerator board,
                         string sortingLayer = "Pieces", int sortingOrder = 2)
        {
            Config = config;
            TeamIndex = teamIndex;
            Row = row;
            Col = col;
            HasMoved = false;
            _board = board;

            _baseSortingOrder = sortingOrder;
            _cellOffset = (Vector3)config.spriteOffset;

            _sr = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingLayerName = sortingLayer;
            _sr.sortingOrder = sortingOrder;
            _sr.sprite = config.GetSprite(teamIndex);

            if (_sr.sprite == null)
                Debug.LogWarning($"[Piece] No sprite for '{config.pieceId}' team {teamIndex}");

            if (teamIndex != 0 && config.spriteTeam1 == null)
                _sr.color = config.team1Tint;

            transform.localScale = Vector3.one * config.spriteScale;
            SnapToCell();
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.identity;

            if (!IsDragging)
                transform.position = GetCellWorldPos();
        }

        public void MoveTo(int row, int col)
        {
            Row = row;
            Col = col;
            HasMoved = true;
        }

        public void MoveTo(int row, int col, Vector3 _ignored) => MoveTo(row, col);

        public void BeginDrag()
        {
            if (IsDragging) return;
            IsDragging = true;
            transform.localScale = Vector3.one * Config.spriteScale * dragScaleMultiplier;
            _sr.sortingOrder = _baseSortingOrder + 10;
        }

        public void UpdateDragPosition(Vector3 worldPos)
        {
            if (!IsDragging) return;
            transform.position = new Vector3(
                worldPos.x + _cellOffset.x,
                worldPos.y + _cellOffset.y,
                transform.position.z);
        }

        public void EndDrag()
        {
            if (!IsDragging) return;
            IsDragging = false;
            transform.localScale = Vector3.one * Config.spriteScale;
            _sr.sortingOrder = _baseSortingOrder;
        }

        public void CancelDrag()
        {
            EndDrag();
            SnapToCell();
        }

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
            _board = board;
            Row = d.row;
            Col = d.col;
            HasMoved = d.hasMoved;
            SnapToCell();
        }

        private Vector3 GetCellWorldPos()
        {
            if (_board == null) return transform.position;
            return _board.CellToWorld(Row, Col) + _cellOffset;
        }

        private void SnapToCell() => transform.position = GetCellWorldPos();
    }
}