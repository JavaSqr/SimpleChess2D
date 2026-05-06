using System.Collections.Generic;
using UnityEngine;
using ChessTemplate.Config;
using ChessTemplate.Core;
using ChessTemplate.Data;

namespace ChessTemplate.Logic
{
    /// <summary>
    /// Вычисляет допустимые ходы для фигур по их MovePattern.
    /// Возвращает MoveInfo — содержит тип хода и все данные для исполнения в SelectionHandler.
    /// </summary>
    public class MoveValidator : MonoBehaviour
    {
        [Header("References")]
        public BoardGenerator board;
        public PieceSpawner spawner;

        [Header("Rules")]
        public bool enableCastling = true;
        public bool enableEnPassant = true;

        // Последний двойной ход пешки — нужен для проверки эн пасант.
        // Устанавливается SelectionHandler'ом после каждого хода.
        // (-1,-1) означает что эн пасант недоступен.
        public (int row, int col) LastDoublePawnMove { get; set; } = (-1, -1);
        public int LastDoublePawnTeam { get; set; } = -1;

        // ── Результат валидации ────────────────────────────────────

        /// <summary>Информация об одном допустимом ходе.</summary>
        public struct MoveInfo
        {
            public int toRow, toCol;
            public MoveType moveType;

            // Рокировка
            public int rookFromCol;
            public int rookToCol;

            // Эн пасант: клетка захваченной пешки (отличается от toRow/toCol)
            public int enPassantCaptureRow;
            public int enPassantCaptureCol;
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Все легальные ходы для фигуры на (row, col).
        /// Возвращает список MoveInfo с типом хода и вспомогательными данными.
        /// </summary>
        public List<MoveInfo> GetValidMoves(int row, int col)
        {
            var piece = spawner.GetPieceAt(row, col);
            if (piece == null) return new List<MoveInfo>();

            var raw = GetRawMoves(piece);
            var legal = new List<MoveInfo>();

            foreach (var move in raw)
            {
                if (!LeavesKingInCheck(piece, move))
                    legal.Add(move);
            }

            return legal;
        }

        /// <summary>Только целевые клетки (для совместимости с подсветкой).</summary>
        public List<(int row, int col)> GetValidMovePositions(int row, int col)
        {
            var result = new List<(int, int)>();
            foreach (var m in GetValidMoves(row, col))
                result.Add((m.toRow, m.toCol));
            return result;
        }

        /// <summary>Найти MoveInfo для конкретного хода на (toRow, toCol).</summary>
        public MoveInfo? FindMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            foreach (var m in GetValidMoves(fromRow, fromCol))
                if (m.toRow == toRow && m.toCol == toCol) return m;
            return null;
        }

        public bool IsInCheck(int teamIndex)
        {
            var king = FindRoyal(teamIndex);
            if (king == null) return false;
            return IsThreatenedOnBoard(spawner.Pieces, king.Row, king.Col, teamIndex);
        }

        public bool HasNoLegalMoves(int teamIndex)
        {
            foreach (var piece in spawner.Pieces.Values)
            {
                if (piece.TeamIndex != teamIndex) continue;
                if (GetValidMoves(piece.Row, piece.Col).Count > 0) return false;
            }
            return true;
        }

        // ── Raw move generation ────────────────────────────────────

        private List<MoveInfo> GetRawMoves(Piece piece)
        {
            var moves = new List<MoveInfo>();
            int team = piece.TeamIndex;
            int dir = (team == 0) ? 1 : -1;

            foreach (var pattern in piece.Config.movePatterns)
            {
                if (pattern.firstMoveOnly && piece.HasMoved) continue;

                int dr = pattern.dRow * dir;
                int dc = pattern.applyDirectionToCol ? pattern.dCol * dir : pattern.dCol;

                AddMovesForPattern(piece, dr, dc, pattern, moves);
            }

            if (enableCastling && piece.Config.isRoyal && !piece.HasMoved)
                AddCastlingMoves(piece, moves);

            if (enableEnPassant && piece.Config.canPromote) // canPromote = это пешка
                AddEnPassantMoves(piece, moves);

            return moves;
        }

        private void AddMovesForPattern(Piece piece, int dr, int dc, MovePattern pattern,
                                        List<MoveInfo> moves)
        {
            int team = piece.TeamIndex;

            if (pattern.slide)
            {
                int r = piece.Row + dr;
                int c = piece.Col + dc;

                while (board.GetCell(r, c) != null)
                {
                    var blocker = spawner.GetPieceAt(r, c);
                    if (blocker == null)
                    {
                        if (pattern.canMoveEmpty)
                            moves.Add(new MoveInfo { toRow = r, toCol = c, moveType = MoveType.Normal });
                    }
                    else
                    {
                        if (blocker.TeamIndex != team && pattern.canCapture)
                            moves.Add(new MoveInfo { toRow = r, toCol = c, moveType = MoveType.Normal });
                        break;
                    }
                    r += dr;
                    c += dc;
                }
            }
            else
            {
                int r = piece.Row + dr;
                int c = piece.Col + dc;

                if (board.GetCell(r, c) == null) return;

                var blocker = spawner.GetPieceAt(r, c);
                if (blocker == null && pattern.canMoveEmpty)
                    moves.Add(new MoveInfo { toRow = r, toCol = c, moveType = MoveType.Normal });
                else if (blocker != null && blocker.TeamIndex != team && pattern.canCapture)
                    moves.Add(new MoveInfo { toRow = r, toCol = c, moveType = MoveType.Normal });
            }
        }

        // ── Castling ───────────────────────────────────────────────

        private void AddCastlingMoves(Piece king, List<MoveInfo> moves)
        {
            int row = king.Row;
            int team = king.TeamIndex;

            foreach (int colDir in new[] { -1, 1 })
            {
                int c = king.Col + colDir;
                while (board.GetCell(row, c) != null)
                {
                    var candidate = spawner.GetPieceAt(row, c);
                    if (candidate != null)
                    {
                        if (candidate.TeamIndex == team &&
                            candidate.Config.canCastleWith &&
                            !candidate.HasMoved)
                        {
                            int kingTarget = king.Col + colDir * 2;
                            // Ладья встаёт на клетку между стартом короля и его целью
                            int rookTarget = king.Col + colDir;

                            if (IsCastlingPathClear(king, kingTarget, colDir))
                            {
                                moves.Add(new MoveInfo
                                {
                                    toRow = row,
                                    toCol = kingTarget,
                                    moveType = MoveType.Castling,
                                    rookFromCol = candidate.Col,
                                    rookToCol = rookTarget
                                });
                            }
                        }
                        break;
                    }
                    c += colDir;
                }
            }
        }

        private bool IsCastlingPathClear(Piece king, int toCol, int dir)
        {
            int col = king.Col + dir;
            int end = toCol + dir;
            while (col != end)
            {
                if (!spawner.IsEmpty(king.Row, col)) return false;
                if (IsThreatenedOnBoard(spawner.Pieces, king.Row, col, king.TeamIndex)) return false;
                col += dir;
            }
            return true;
        }

        // ── En Passant ─────────────────────────────────────────────
        //
        // Условие: пешка противника только что сделала двойной ход и стоит рядом с нашей пешкой.
        // LastDoublePawnMove хранит позицию этой пешки, устанавливается SelectionHandler'ом.
        // Наша пешка ходит по диагонали на пустую клетку за вражеской пешкой.

        private void AddEnPassantMoves(Piece pawn, List<MoveInfo> moves)
        {
            // Эн пасант доступен только если предыдущий ход был двойным ходом вражеской пешки
            if (LastDoublePawnTeam == pawn.TeamIndex) return;
            if (LastDoublePawnMove == (-1, -1)) return;

            (int epRow, int epCol) = LastDoublePawnMove;

            // Вражеская пешка должна стоять в том же ряду что и наша, и рядом по столбцу
            if (epRow != pawn.Row) return;
            if (Mathf.Abs(epCol - pawn.Col) != 1) return;

            // Наша пешка идёт по диагонали за вражеской
            int dir = (pawn.TeamIndex == 0) ? 1 : -1;
            int captureRow = pawn.Row + dir;   // клетка куда идёт наша пешка
            int captureCol = epCol;

            if (board.GetCell(captureRow, captureCol) == null) return;

            // Целевая клетка должна быть пустой (захват на проходе)
            if (!spawner.IsEmpty(captureRow, captureCol)) return;

            moves.Add(new MoveInfo
            {
                toRow = captureRow,
                toCol = captureCol,
                moveType = MoveType.EnPassant,
                enPassantCaptureRow = epRow,
                enPassantCaptureCol = epCol
            });
        }

        // ── Check simulation ───────────────────────────────────────

        private bool LeavesKingInCheck(Piece movingPiece, MoveInfo move)
        {
            int fromRow = movingPiece.Row;
            int fromCol = movingPiece.Col;
            int team = movingPiece.TeamIndex;

            var simBoard = new Dictionary<(int, int), (int teamIndex, bool isRoyal)>();
            foreach (var kv in spawner.Pieces)
                simBoard[kv.Key] = (kv.Value.TeamIndex, kv.Value.Config.isRoyal);

            // Применяем ход
            simBoard.Remove((fromRow, fromCol));
            simBoard[(move.toRow, move.toCol)] = (team, movingPiece.Config.isRoyal);

            // Для эн пасант — убираем захваченную пешку
            if (move.moveType == MoveType.EnPassant)
                simBoard.Remove((move.enPassantCaptureRow, move.enPassantCaptureCol));

            // Для рокировки — двигаем ладью
            if (move.moveType == MoveType.Castling)
            {
                simBoard.Remove((movingPiece.Row, move.rookFromCol));
                simBoard[(movingPiece.Row, move.rookToCol)] = (team, false);
            }

            // Ищем короля после хода
            (int row, int col) kingPos = (-1, -1);
            foreach (var kv in simBoard)
                if (kv.Value.teamIndex == team && kv.Value.isRoyal)
                { kingPos = kv.Key; break; }

            if (kingPos == (-1, -1)) return false;

            return IsThreatenedOnSimBoard(simBoard, kingPos.row, kingPos.col, team);
        }

        private bool IsThreatenedOnBoard(
            IReadOnlyDictionary<(int, int), Piece> pieces,
            int row, int col, int defenderTeam)
        {
            foreach (var piece in pieces.Values)
            {
                if (piece.TeamIndex == defenderTeam) continue;
                foreach (var m in GetRawMoves(piece))
                    if (m.toRow == row && m.toCol == col) return true;
            }
            return false;
        }

        private bool IsThreatenedOnSimBoard(
            Dictionary<(int, int), (int teamIndex, bool isRoyal)> simBoard,
            int targetRow, int targetCol, int defenderTeam)
        {
            foreach (var kv in simBoard)
            {
                if (kv.Value.teamIndex == defenderTeam) continue;

                var realPiece = spawner.GetPieceAt(kv.Key.Item1, kv.Key.Item2);
                if (realPiece == null) continue;

                if (SimPieceAttacks(simBoard, realPiece, targetRow, targetCol))
                    return true;
            }
            return false;
        }

        private bool SimPieceAttacks(
            Dictionary<(int, int), (int teamIndex, bool isRoyal)> simBoard,
            Piece attacker, int targetRow, int targetCol)
        {
            int team = attacker.TeamIndex;
            int dir = (team == 0) ? 1 : -1;

            foreach (var pattern in attacker.Config.movePatterns)
            {
                if (!pattern.canCapture) continue;
                if (pattern.firstMoveOnly && attacker.HasMoved) continue;

                int dr = pattern.dRow * dir;
                int dc = pattern.applyDirectionToCol ? pattern.dCol * dir : pattern.dCol;

                if (pattern.slide)
                {
                    int r = attacker.Row + dr;
                    int c = attacker.Col + dc;
                    while (board.GetCell(r, c) != null)
                    {
                        if (r == targetRow && c == targetCol) return true;
                        if (simBoard.ContainsKey((r, c))) break;
                        r += dr; c += dc;
                    }
                }
                else
                {
                    if (attacker.Row + dr == targetRow && attacker.Col + dc == targetCol)
                        return true;
                }
            }
            return false;
        }

        private Piece FindRoyal(int teamIndex)
        {
            foreach (var p in spawner.Pieces.Values)
                if (p.TeamIndex == teamIndex && p.Config.isRoyal) return p;
            return null;
        }
    }
}