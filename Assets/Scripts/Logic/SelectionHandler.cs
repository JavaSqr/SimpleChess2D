using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using ChessTemplate.Config;
using ChessTemplate.Core;
using ChessTemplate.Data;

namespace ChessTemplate.Logic
{
    /// <summary>
    /// Клик + перетаскивание → выбор → подсветка → исполнение хода.
    ///
    /// Поддерживает два режима управления одновременно:
    ///   • Клик — выбрать фигуру, затем кликнуть на целевую клетку
    ///   • Drag  — зажать фигуру и отпустить над целевой клеткой
    ///
    /// Drag-поведение:
    ///   • Отпустить над допустимой клеткой → ход
    ///   • Отпустить над своей фигурой → переключиться на неё (drag отменяется)
    ///   • Отпустить над недопустимой клеткой → фигура возвращается на место
    ///   • Зажатая фигура отображается крупнее (dragScaleMultiplier в Piece)
    ///
    /// Inspector:
    ///   Board        → BoardGenerator
    ///   Spawner      → PieceSpawner
    ///   Validator    → MoveValidator
    ///   Turns        → TurnManager
    ///   Board Config → BoardConfig
    ///   Ui           → UIManager
    ///   Drag Camera  → Main Camera (для перевода экранных координат в мировые)
    /// </summary>
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
        [Tooltip("Камера используемая для перевода Screen → World координат при drag.")]
        public Camera dragCamera;

        [Header("Events")]
        [Tooltip("Любой совершённый ход (до передачи хода TurnManager'у).")]
        public UnityEvent<MoveRecord> OnMoveMade;

        // ── Runtime ────────────────────────────────────────────────

        private Piece _selected;
        private List<MoveValidator.MoveInfo> _validMoves = new();
        private bool _active = true;

        // Drag state
        private Piece _dragPiece;       // фигура под курсором
        private bool _isDragging;
        private int _dragFromRow, _dragFromCol;

        // Промоция
        private bool _waitingForPromotion;
        private int _promotionRow, _promotionCol, _promotionTeam;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (dragCamera == null)
                dragCamera = Camera.main;
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

        // ── Mouse input (drag) ─────────────────────────────────────

        private void Update()
        {
            if (!_active || _waitingForPromotion) return;

            if (Input.GetMouseButtonDown(0))
                TryBeginDrag();

            if (_isDragging)
            {
                if (Input.GetMouseButton(0))
                    UpdateDrag();
                else
                    EndDrag();
            }
        }

        private void TryBeginDrag()
        {
            var worldPos = GetMouseWorldPos();
            var (row, col) = board.WorldToCell(worldPos);
            if (row < 0) return;

            var piece = spawner.GetPieceAt(row, col);
            if (piece == null || piece.TeamIndex != turns.CurrentTeam) return;

            // Начинаем drag
            _dragPiece = piece;
            _dragFromRow = row;
            _dragFromCol = col;
            _isDragging = true;

            // Выбираем фигуру (подсветка ходов)
            SelectPiece(piece);
            piece.BeginDrag();
        }

        private void UpdateDrag()
        {
            _dragPiece?.UpdateDragPosition(GetMouseWorldPos());
        }

        private void EndDrag()
        {
            if (!_isDragging || _dragPiece == null)
            {
                _isDragging = false;
                return;
            }

            _dragPiece.EndDrag();
            _isDragging = false;

            var worldPos = GetMouseWorldPos();
            var (row, col) = board.WorldToCell(worldPos);

            // Отпустили вне доски — отмена
            if (row < 0)
            {
                _dragPiece.CancelDrag();
                // Оставляем выделение — игрок может кликнуть куда ходить
                _dragPiece = null;
                return;
            }

            var targetPiece = spawner.GetPieceAt(row, col);

            // Отпустили на свою фигуру — переключиться на неё
            if (targetPiece != null && targetPiece.TeamIndex == turns.CurrentTeam
                && targetPiece != _dragPiece)
            {
                _dragPiece.CancelDrag();
                _dragPiece = null;
                Deselect();
                SelectPiece(targetPiece);
                return;
            }

            // Отпустили на допустимую клетку — ход
            var move = FindMoveAt(row, col);
            if (move.HasValue)
            {
                _dragPiece = null;
                ExecuteMove(_selected, move.Value);
                return;
            }

            // Отпустили на недопустимую клетку — возврат на место
            _dragPiece.CancelDrag();
            _dragPiece = null;
            // Оставляем выделение
        }

        private Vector3 GetMouseWorldPos()
        {
            var screenPos = Input.mousePosition;
            screenPos.z = Mathf.Abs(dragCamera.transform.position.z);
            return dragCamera.ScreenToWorldPoint(screenPos);
        }

        // ── Click (OnMouseDown через Cell) ─────────────────────────

        private void HandleCellClick(Cell cell)
        {
            // Клик обрабатывается только если не идёт drag в этот момент
            if (_isDragging || _waitingForPromotion || !_active) return;

            int r = cell.Row, c = cell.Col;
            var clicked = spawner.GetPieceAt(r, c);

            if (_selected == null)
            {
                if (clicked != null && clicked.TeamIndex == turns.CurrentTeam)
                    SelectPiece(clicked);
                return;
            }

            // Клик на свою другую фигуру — переключить
            if (clicked != null && clicked.TeamIndex == turns.CurrentTeam && clicked != _selected)
            {
                Deselect();
                SelectPiece(clicked);
                return;
            }

            // Клик на допустимую клетку — ход
            var move = FindMoveAt(r, c);
            if (move.HasValue)
            {
                ExecuteMove(_selected, move.Value);
                return;
            }

            Deselect();
        }

        // ── Selection ──────────────────────────────────────────────

        private void SelectPiece(Piece piece)
        {
            _selected = piece;
            _validMoves = validator.GetValidMoves(piece.Row, piece.Col);

            board.GetCell(piece.Row, piece.Col)?.SetHighlight(boardConfig.selectedColor);

            foreach (var move in _validMoves)
            {
                bool isCapture = !spawner.IsEmpty(move.toRow, move.toCol)
                                 || move.moveType == MoveType.EnPassant;
                var color = isCapture ? boardConfig.captureColor : boardConfig.validMoveColor;
                board.GetCell(move.toRow, move.toCol)?.SetHighlight(color);
            }
        }

        private void Deselect()
        {
            board.ClearHighlights();
            _selected = null;
            _validMoves.Clear();
        }

        private MoveValidator.MoveInfo? FindMoveAt(int row, int col)
        {
            foreach (var m in _validMoves)
                if (m.toRow == row && m.toCol == col) return m;
            return null;
        }

        private void CancelDragIfActive()
        {
            if (_isDragging && _dragPiece != null)
            {
                _dragPiece.CancelDrag();
                _dragPiece = null;
            }
            _isDragging = false;
        }

        // ── Move execution ─────────────────────────────────────────

        private void ExecuteMove(Piece piece, MoveValidator.MoveInfo move)
        {
            int fromRow = piece.Row;
            int fromCol = piece.Col;

            // Собираем данные о захвате ДО хода
            Piece capturedPiece = move.moveType == MoveType.EnPassant
                ? spawner.GetPieceAt(move.enPassantCaptureRow, move.enPassantCaptureCol)
                : spawner.GetPieceAt(move.toRow, move.toCol);

            bool hadCapture = (capturedPiece != null && capturedPiece.TeamIndex != piece.TeamIndex)
                              || move.moveType == MoveType.EnPassant;

            // Исполняем ход
            switch (move.moveType)
            {
                case MoveType.Castling: ExecuteCastling(piece, move); break;
                case MoveType.EnPassant: ExecuteEnPassant(piece, move); break;
                default: spawner.MovePiece(fromRow, fromCol, move.toRow, move.toCol); break;
            }

            board.GetCell(move.toRow, move.toCol)?.Flash(boardConfig.selectedColor);

            // Обновляем состояние эн пасант
            bool isDoublePawn = piece.Config.canPromote && Mathf.Abs(move.toRow - fromRow) == 2;
            if (isDoublePawn)
            {
                validator.LastDoublePawnMove = (move.toRow, move.toCol);
                validator.LastDoublePawnTeam = piece.TeamIndex;
            }
            else
            {
                validator.LastDoublePawnMove = (-1, -1);
                validator.LastDoublePawnTeam = -1;
            }

            // Формируем запись хода
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

            // Промоция
            if (piece.Config.canPromote && IsPromotionRow(move.toRow, piece.TeamIndex))
            {
                StartPromotion(move.toRow, move.toCol, piece.TeamIndex, record);
                return;
            }

            turns.EndTurn(record);
        }

        private void ExecuteCastling(Piece king, MoveValidator.MoveInfo move)
        {
            spawner.MovePiece(king.Row, king.Col, king.Row, move.toCol);
            spawner.MovePiece(king.Row, move.rookFromCol, king.Row, move.rookToCol);
        }

        private void ExecuteEnPassant(Piece pawn, MoveValidator.MoveInfo move)
        {
            spawner.MovePiece(pawn.Row, pawn.Col, move.toRow, move.toCol);
            spawner.RemovePiece(move.enPassantCaptureRow, move.enPassantCaptureCol);
        }

        // ── Promotion ──────────────────────────────────────────────

        private bool IsPromotionRow(int row, int teamIndex) =>
            teamIndex == 0 ? row == board.Rows - 1 : row == 0;

        // record нужен чтобы передать его в EndTurn после промоции
        private MoveRecord _pendingRecord;

        private void StartPromotion(int row, int col, int teamIndex, MoveRecord record)
        {
            _promotionRow = row;
            _promotionCol = col;
            _promotionTeam = teamIndex;
            _pendingRecord = record;

            if (ui == null || !ui.PromotionPanelIsReady())
            {
                Debug.LogWarning("[SelectionHandler] UIManager/PromotionPanel не настроен. Авто-промоция.");
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
            if (chosen == null)
            {
                Debug.LogError("[SelectionHandler] Нет конфигов для промоции в UIManager.PromotionConfigs.");
                turns.EndTurn(record);
                return;
            }
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