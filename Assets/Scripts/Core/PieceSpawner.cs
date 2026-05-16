using System.Collections.Generic;
using UnityEngine;
using ChessTemplate.Config;
using ChessTemplate.Data;

namespace ChessTemplate.Core
{
    public class PieceSpawner : MonoBehaviour
    {
        [Header("References")]
        public BoardGenerator board;
        public GameObject piecePrefab;

        [Header("Sorting")]
        public string piecesSortingLayer = "Pieces";
        public int piecesSortingOrder = 2;

        private readonly Dictionary<(int, int), Piece> _pieces = new();
        public IReadOnlyDictionary<(int, int), Piece> Pieces => _pieces;

        public void SpawnFromConfig(GameSetupConfig setup)
        {
            ClearAll();
            foreach (var p in setup.placements)
                SpawnPiece(p.piece, p.teamIndex, p.row, p.col);
        }

        public void SpawnFromSave(List<PieceData> dataList, List<PieceConfig> allConfigs)
        {
            ClearAll();
            foreach (var d in dataList)
            {
                var cfg = allConfigs.Find(c => c.pieceId == d.pieceConfigId);
                if (cfg == null) { Debug.LogWarning($"[PieceSpawner] Config not found: '{d.pieceConfigId}'"); continue; }
                SpawnPiece(cfg, d.teamIndex, d.row, d.col).RestoreFromData(d, board);
            }
        }

        public Piece MovePiece(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (!_pieces.TryGetValue((fromRow, fromCol), out var piece)) return null;

            if (_pieces.TryGetValue((toRow, toCol), out var target) && target.TeamIndex != piece.TeamIndex)
                RemovePiece(toRow, toCol);

            _pieces.Remove((fromRow, fromCol));
            piece.MoveTo(toRow, toCol, board.CellToWorld(toRow, toCol));
            _pieces[(toRow, toCol)] = piece;
            return piece;
        }

        public Piece PromotePiece(int row, int col, PieceConfig newConfig)
        {
            if (!_pieces.TryGetValue((row, col), out var oldPiece))
            {
                Debug.LogWarning($"[PieceSpawner] PromotePiece: no piece at ({row},{col})");
                return null;
            }

            int teamIndex = oldPiece.TeamIndex;
            _pieces.Remove((row, col));
            Destroy(oldPiece.gameObject);

            var promoted = SpawnPiece(newConfig, teamIndex, row, col);
            promoted.MarkAsMoved();
            return promoted;
        }

        public Piece GetPieceAt(int row, int col)
        {
            _pieces.TryGetValue((row, col), out var p);
            return p;
        }

        public bool IsEmpty(int row, int col) => !_pieces.ContainsKey((row, col));

        public List<PieceData> GetAllPieceData()
        {
            var list = new List<PieceData>();
            foreach (var p in _pieces.Values) list.Add(p.ToData());
            return list;
        }

        public void RemovePiece(int row, int col)
        {
            if (!_pieces.TryGetValue((row, col), out var p)) return;
            _pieces.Remove((row, col));
            Destroy(p.gameObject);
        }

        public void ClearAll()
        {
            foreach (var p in _pieces.Values)
                if (p != null) Destroy(p.gameObject);
            _pieces.Clear();
        }

        private Piece SpawnPiece(PieceConfig cfg, int teamIndex, int row, int col)
        {
            var go = Instantiate(piecePrefab, board.CellToWorld(row, col), Quaternion.identity);
            go.name = $"{cfg.pieceId}_t{teamIndex}_{row}_{col}";
            go.transform.SetParent(transform, true);

            var piece = go.GetComponent<Piece>() ?? go.AddComponent<Piece>();
            piece.Init(cfg, teamIndex, row, col, board, piecesSortingLayer, piecesSortingOrder);
            _pieces[(row, col)] = piece;
            return piece;
        }
    }
}