using System.Collections.Generic;
using UnityEngine;
using SimpleChess.Config;
using SimpleChess.Core;
using SimpleChess.Data;

namespace SimpleChess.Logic
{
    public class MoveValidator : MonoBehaviour
    {
        [Header("References")]
        public BoardGenerator board;
        public PieceSpawner spawner;

        [Header("Rules")]
        public bool enableCastling = true;
        public bool enableEnPassant = true;

        // Set by SelectionHandler after every move; (-1,-1) means en passant unavailable
        public (int row, int col) LastDoublePawnMove { get; set; } = (-1, -1);
        public int LastDoublePawnTeam { get; set; } = -1;

        public struct MoveInfo
        {
            public int toRow, toCol;
            public MoveType moveType;
            public int rookFromCol;
            public int rookToCol;
            public int enPassantCaptureRow;
            public int enPassantCaptureCol;
        }

        public List<MoveInfo> GetValidMoves(int row, int col)
        {
            var piece = spawner.GetPieceAt(row, col);
            if (piece == null) return new List<MoveInfo>();

            var raw = GetRawMoves(piece);
            var legal = new List<MoveInfo>();
            foreach (var move in raw)
                if (!LeavesKingInCheck(piece, move)) legal.Add(move);
            return legal;
        }

        public List<(int row, int col)> GetValidMovePositions(int row, int col)
        {
            var result = new List<(int, int)>();
            foreach (var m in GetValidMoves(row, col)) result.Add((m.toRow, m.toCol));
            return result;
        }

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

        private List<MoveInfo> GetRawMoves(Piece piece)
        {
            var moves = new List<MoveInfo>();
            int dir = (piece.TeamIndex == 0) ? 1 : -1;

            foreach (var pattern in piece.Config.movePatterns)
            {
                if (pattern.firstMoveOnly && piece.HasMoved) continue;
                int dr = pattern.dRow * dir;
                int dc = pattern.applyDirectionToCol ? pattern.dCol * dir : pattern.dCol;
                AddMovesForPattern(piece, dr, dc, pattern, moves);
            }

            if (enableCastling && piece.Config.isRoyal && !piece.HasMoved)
                AddCastlingMoves(piece, moves);

            if (enableEnPassant && piece.Config.canPromote)
                AddEnPassantMoves(piece, moves);

            return moves;
        }

        private void AddMovesForPattern(Piece piece, int dr, int dc, MovePattern pattern, List<MoveInfo> moves)
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
                    r += dr; c += dc;
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
                        if (candidate.TeamIndex == team && candidate.Config.canCastleWith && !candidate.HasMoved)
                        {
                            int kingTarget = king.Col + colDir * 2;
                            int rookTarget = king.Col + colDir;
                            if (IsCastlingPathClear(king, kingTarget, colDir))
                                moves.Add(new MoveInfo
                                {
                                    toRow = row,
                                    toCol = kingTarget,
                                    moveType = MoveType.Castling,
                                    rookFromCol = candidate.Col,
                                    rookToCol = rookTarget
                                });
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

        private void AddEnPassantMoves(Piece pawn, List<MoveInfo> moves)
        {
            if (LastDoublePawnTeam == pawn.TeamIndex) return;
            if (LastDoublePawnMove == (-1, -1)) return;

            (int epRow, int epCol) = LastDoublePawnMove;
            if (epRow != pawn.Row) return;
            if (Mathf.Abs(epCol - pawn.Col) != 1) return;

            int dir = (pawn.TeamIndex == 0) ? 1 : -1;
            int captureRow = pawn.Row + dir;
            int captureCol = epCol;

            if (board.GetCell(captureRow, captureCol) == null) return;
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

        private bool LeavesKingInCheck(Piece movingPiece, MoveInfo move)
        {
            int team = movingPiece.TeamIndex;

            var sim = new Dictionary<(int, int), (int teamIndex, bool isRoyal)>();
            foreach (var kv in spawner.Pieces)
                sim[kv.Key] = (kv.Value.TeamIndex, kv.Value.Config.isRoyal);

            sim.Remove((movingPiece.Row, movingPiece.Col));
            sim[(move.toRow, move.toCol)] = (team, movingPiece.Config.isRoyal);

            if (move.moveType == MoveType.EnPassant)
                sim.Remove((move.enPassantCaptureRow, move.enPassantCaptureCol));

            if (move.moveType == MoveType.Castling)
            {
                sim.Remove((movingPiece.Row, move.rookFromCol));
                sim[(movingPiece.Row, move.rookToCol)] = (team, false);
            }

            (int row, int col) kingPos = (-1, -1);
            foreach (var kv in sim)
                if (kv.Value.teamIndex == team && kv.Value.isRoyal) { kingPos = kv.Key; break; }

            if (kingPos == (-1, -1)) return false;
            return IsThreatenedOnSimBoard(sim, kingPos.row, kingPos.col, team);
        }

        private bool IsThreatenedOnBoard(IReadOnlyDictionary<(int, int), Piece> pieces,
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

        private bool IsThreatenedOnSimBoard(Dictionary<(int, int), (int teamIndex, bool isRoyal)> sim,
                                             int targetRow, int targetCol, int defenderTeam)
        {
            foreach (var kv in sim)
            {
                if (kv.Value.teamIndex == defenderTeam) continue;
                var realPiece = spawner.GetPieceAt(kv.Key.Item1, kv.Key.Item2);
                if (realPiece == null) continue;
                if (SimPieceAttacks(sim, realPiece, targetRow, targetCol)) return true;
            }
            return false;
        }

        private bool SimPieceAttacks(Dictionary<(int, int), (int teamIndex, bool isRoyal)> sim,
                                      Piece attacker, int targetRow, int targetCol)
        {
            int dir = (attacker.TeamIndex == 0) ? 1 : -1;

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
                        if (sim.ContainsKey((r, c))) break;
                        r += dr; c += dc;
                    }
                }
                else
                {
                    if (attacker.Row + dr == targetRow && attacker.Col + dc == targetCol) return true;
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