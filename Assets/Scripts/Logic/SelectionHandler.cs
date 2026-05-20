using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SimpleChess.Config;
using SimpleChess.Core;
using SimpleChess.Data;

namespace SimpleChess.Logic
{
    public class SelectionHandler : MonoBehaviour
    {
        [Header("References")]
        public BoardGenerator board;
        public PieceSpawner spawner;
        public MoveValidator validator;
        public TurnManager turns;

        [Header("Board Config (highlight colors)")]
        public BoardConfig boardConfig;

        [Header("UI (for promotion panel)")]
        public UI.UIManager ui;

        [Header("Drag")]
        public Camera dragCamera;

        [Header("Events")]
        public UnityEvent<MoveRecord> OnMoveMade;

        private Piece _selected;
        private List<MoveValidator.MoveInfo> _validMoves = new();
        private bool _active = true;

        private Piece _dragPiece;
        private bool _isDragging;

        private bool _waitingForPromotion;
        private int _promotionRow, _promotionCol, _promotionTeam;
        private MoveRecord _pendingRecord;

        private void Awake()
        {
            if (dragCamera == null) dragCamera = Camera.main;
        }

        private void OnEnable() => BoardGenerator.OnCellClicked += HandleCellClick;
        private void OnDisable()
        {
            BoardGenerator.OnCellClicked -= HandleCellClick;
            CancelDragIfActive();
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (!active) CancelDragIfActive();
        }

        private void Update()
        {
            if (!_active || _waitingForPromotion) return;

            if (Input.GetMouseButtonDown(0)) TryBeginDrag();

            if (_isDragging)
            {
                if (Input.GetMouseButton(0)) _dragPiece?.UpdateDragPosition(GetMouseWorldPos());
                else EndDrag();
            }
        }

        private void TryBeginDrag()
        {
            var (row, col) = board.WorldToCell(GetMouseWorldPos());
            if (row < 0) return;

            var piece = spawner.GetPieceAt(row, col);
            if (piece == null || piece.TeamIndex != turns.CurrentTeam) return;

            _dragPiece = piece;
            _isDragging = true;

            SelectPiece(piece);
            piece.BeginDrag();
        }

        private void EndDrag()
        {
            if (!_isDragging || _dragPiece == null) { _isDragging = false; return; }

            _dragPiece.EndDrag();
            _isDragging = false;

            var (row, col) = board.WorldToCell(GetMouseWorldPos());

            if (row < 0) { _dragPiece.CancelDrag(); _dragPiece = null; return; }

            var targetPiece = spawner.GetPieceAt(row, col);
            if (targetPiece != null && targetPiece.TeamIndex == turns.CurrentTeam && targetPiece != _dragPiece)
            {
                _dragPiece.CancelDrag();
                _dragPiece = null;
                Deselect();
                SelectPiece(targetPiece);
                return;
            }

            var move = FindMoveAt(row, col);
            if (move.HasValue) { _dragPiece = null; ExecuteMove(_selected, move.Value); return; }

            _dragPiece.CancelDrag();
            _dragPiece = null;
        }

        private Vector3 GetMouseWorldPos()
        {
            var p = Input.mousePosition;
            p.z = Mathf.Abs(dragCamera.transform.position.z);
            return dragCamera.ScreenToWorldPoint(p);
        }

        private void HandleCellClick(Cell cell)
        {
            if (_isDragging || _waitingForPromotion || !_active) return;

            int r = cell.Row, c = cell.Col;
            var clicked = spawner.GetPieceAt(r, c);

            if (_selected == null)
            {
                if (clicked != null && clicked.TeamIndex == turns.CurrentTeam) SelectPiece(clicked);
                return;
            }

            if (clicked != null && clicked.TeamIndex == turns.CurrentTeam && clicked != _selected)
            {
                Deselect(); SelectPiece(clicked); return;
            }

            var move = FindMoveAt(r, c);
            if (move.HasValue) { ExecuteMove(_selected, move.Value); return; }

            Deselect();
        }

        private void SelectPiece(Piece piece)
        {
            _selected = piece;
            _validMoves = validator.GetValidMoves(piece.Row, piece.Col);

            board.GetCell(piece.Row, piece.Col)?.SetHighlight(boardConfig.selectedColor);
            foreach (var move in _validMoves)
            {
                bool isCapture = !spawner.IsEmpty(move.toRow, move.toCol) || move.moveType == MoveType.EnPassant;
                board.GetCell(move.toRow, move.toCol)?.SetHighlight(isCapture ? boardConfig.captureColor : boardConfig.validMoveColor);
            }
        }

        private void Deselect() { board.ClearHighlights(); _selected = null; _validMoves.Clear(); }

        private MoveValidator.MoveInfo? FindMoveAt(int row, int col)
        {
            foreach (var m in _validMoves)
                if (m.toRow == row && m.toCol == col) return m;
            return null;
        }

        private void CancelDragIfActive()
        {
            if (_isDragging && _dragPiece != null) { _dragPiece.CancelDrag(); _dragPiece = null; }
            _isDragging = false;
        }

        private void ExecuteMove(Piece piece, MoveValidator.MoveInfo move)
        {
            int fromRow = piece.Row;
            int fromCol = piece.Col;

            Piece capturedPiece = move.moveType == MoveType.EnPassant
                ? spawner.GetPieceAt(move.enPassantCaptureRow, move.enPassantCaptureCol)
                : spawner.GetPieceAt(move.toRow, move.toCol);

            bool hadCapture = (capturedPiece != null && capturedPiece.TeamIndex != piece.TeamIndex)
                              || move.moveType == MoveType.EnPassant;

            switch (move.moveType)
            {
                case MoveType.Castling:
                    spawner.MovePiece(piece.Row, piece.Col, piece.Row, move.toCol);
                    spawner.MovePiece(piece.Row, move.rookFromCol, piece.Row, move.rookToCol);
                    break;
                case MoveType.EnPassant:
                    spawner.MovePiece(piece.Row, piece.Col, move.toRow, move.toCol);
                    spawner.RemovePiece(move.enPassantCaptureRow, move.enPassantCaptureCol);
                    break;
                default:
                    spawner.MovePiece(fromRow, fromCol, move.toRow, move.toCol);
                    break;
            }

            board.GetCell(move.toRow, move.toCol)?.Flash(boardConfig.selectedColor);

            bool isDoublePawn = piece.Config.canPromote && Mathf.Abs(move.toRow - fromRow) == 2;
            validator.LastDoublePawnMove = isDoublePawn ? (move.toRow, move.toCol) : (-1, -1);
            validator.LastDoublePawnTeam = isDoublePawn ? piece.TeamIndex : -1;

            var record = new MoveRecord
            {
                pieceConfigId = piece.Config.pieceId,
                teamIndex = piece.TeamIndex,
                fromRow = fromRow,
                fromCol = fromCol,
                toRow = move.toRow,
                toCol = move.toCol,
                moveType = move.moveType,
                capturedPiece = hadCapture,
                capturedPieceData = hadCapture && capturedPiece != null ? capturedPiece.ToData() : null,
                rookFromCol = move.rookFromCol,
                rookToCol = move.rookToCol,
                enPassantCaptureRow = move.enPassantCaptureRow,
                enPassantCaptureCol = move.enPassantCaptureCol,
                timestamp = Time.time
            };

            Deselect();
            OnMoveMade?.Invoke(record);

            if (piece.Config.canPromote && IsPromotionRow(move.toRow, piece.TeamIndex))
            {
                StartPromotion(move.toRow, move.toCol, piece.TeamIndex, record);
                return;
            }

            turns.EndTurn(record);
        }

        private bool IsPromotionRow(int row, int teamIndex)
            => teamIndex == 0 ? row == board.Rows - 1 : row == 0;

        private void StartPromotion(int row, int col, int teamIndex, MoveRecord record)
        {
            _promotionRow = row;
            _promotionCol = col;
            _promotionTeam = teamIndex;
            _pendingRecord = record;

            if (ui == null || !ui.PromotionPanelIsReady())
            {
                Debug.LogWarning("[SelectionHandler] UIManager/PromotionPanel not set up. Auto-promoting.");
                AutoPromote(row, col, record);
                return;
            }

            _waitingForPromotion = true;
            SetActive(false);
            ui.ShowPromotionPanel(teamIndex, OnPromotionChosen);
        }

        private void AutoPromote(int row, int col, MoveRecord record)
        {
            var chosen = ui != null && ui.promotionConfigs?.Count > 0 ? ui.promotionConfigs[0] : null;
            if (chosen == null) { Debug.LogError("[SelectionHandler] No promotion configs in UIManager."); turns.EndTurn(record); return; }
            spawner.PromotePiece(row, col, chosen);
            turns.EndTurn(record);
        }

        public void OnPromotionChosen(PieceConfig chosenConfig)
        {
            spawner.PromotePiece(_promotionRow, _promotionCol, chosenConfig);
            _waitingForPromotion = false;
            SetActive(true);
            ui?.HidePromotionPanel();
            turns.EndTurn(_pendingRecord);
        }
    }
}