using System.Collections.Generic;
using UnityEngine;
using ChessTemplate.Config;

namespace ChessTemplate.Core
{
    public class BoardGenerator : MonoBehaviour
    {
        [Header("Config")]
        public BoardConfig boardConfig;

        [Header("Prefabs")]
        public GameObject cellPrefab;

        [Header("Sorting")]
        public string boardSortingLayer = "Board";
        public int boardSortingOrder = 0;

        [Header("Behaviour")]
        public bool generateOnAwake = false;

        private Cell[,] _grid;
        private int _rows, _cols;
        private Vector3 _originOffset;

        public Cell[,] Grid => _grid;
        public int Rows => _rows;
        public int Cols => _cols;
        public float CellSize => boardConfig != null ? boardConfig.cellSize : 1f;
        public bool IsGenerated => _grid != null;

        public static event System.Action<Cell> OnCellClicked;

        private void Awake()
        {
            if (!generateOnAwake) return;
            if (boardConfig == null) { Debug.LogError("[BoardGenerator] BoardConfig missing.", this); return; }
            GenerateBoard();
        }

        public void GenerateBoard(BoardConfig newConfig = null)
        {
            if (newConfig != null) boardConfig = newConfig;
            if (boardConfig == null) { Debug.LogError("[BoardGenerator] BoardConfig missing.", this); return; }
            ClearBoard();
            BuildGrid();
        }

        public void ClearBoard()
        {
            if (_grid != null)
            {
                foreach (var cell in _grid)
                    if (cell != null) Destroy(cell.gameObject);
                _grid = null;
            }
        }

        public Cell GetCell(int row, int col)
        {
            if (_grid == null || row < 0 || row >= _rows || col < 0 || col >= _cols) return null;
            return _grid[row, col];
        }

        public Vector3 CellToWorld(int row, int col)
        {
            float cs = boardConfig.cellSize;
            float half = cs * 0.5f;
            var local = new Vector3(
                col * cs + half + _originOffset.x,
                row * cs + half + _originOffset.y,
                0f);
            return transform.position + transform.rotation * local;
        }

        public (int row, int col) WorldToCell(Vector3 worldPos)
        {
            // Undo board rotation before computing grid coordinates
            Vector3 unrotated = Quaternion.Inverse(transform.rotation) * (worldPos - transform.position);
            Vector3 local = unrotated - _originOffset;
            int col = Mathf.FloorToInt(local.x / boardConfig.cellSize);
            int row = Mathf.FloorToInt(local.y / boardConfig.cellSize);
            if (row < 0 || row >= _rows || col < 0 || col >= _cols) return (-1, -1);
            return (row, col);
        }

        public void HighlightCells(IEnumerable<(int row, int col)> cells, Color color)
        {
            foreach (var (r, c) in cells)
                GetCell(r, c)?.SetHighlight(color);
        }

        public void ClearHighlights()
        {
            if (_grid == null) return;
            foreach (var cell in _grid)
                cell?.ClearHighlight();
        }

        private void BuildGrid()
        {
            _rows = boardConfig.rows;
            _cols = boardConfig.cols;
            _grid = new Cell[_rows, _cols];

            float cs = boardConfig.cellSize;
            _originOffset = new Vector3(-_cols * cs * 0.5f, -_rows * cs * 0.5f, 0f);

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    bool isLight = (r + c) % 2 == 0;
                    var go = Instantiate(cellPrefab, CellToWorld(r, c), Quaternion.identity, transform);
                    go.name = $"Cell_{r}_{c}";

                    var cell = go.GetComponent<Cell>() ?? go.AddComponent<Cell>();
                    cell.Init(r, c, isLight, boardConfig, boardSortingLayer, boardSortingOrder);
                    cell.OnClick += HandleCellClick;
                    _grid[r, c] = cell;
                }
            }

            if (boardConfig.showBorder) DrawBorder();
        }

        private void DrawBorder()
        {
            float w = _cols * boardConfig.cellSize;
            float h = _rows * boardConfig.cellSize;
            float bw = boardConfig.borderWidth;
            float ox = _originOffset.x;
            float oy = _originOffset.y;

            var borderGo = new GameObject("__Border");
            borderGo.transform.SetParent(transform, false);

            CreateBorderSegment(borderGo.transform, new Vector3(ox + w * 0.5f, oy - bw * 0.5f, 0), new Vector2(w + bw * 2, bw));
            CreateBorderSegment(borderGo.transform, new Vector3(ox + w * 0.5f, oy + h + bw * 0.5f, 0), new Vector2(w + bw * 2, bw));
            CreateBorderSegment(borderGo.transform, new Vector3(ox - bw * 0.5f, oy + h * 0.5f, 0), new Vector2(bw, h));
            CreateBorderSegment(borderGo.transform, new Vector3(ox + w + bw * 0.5f, oy + h * 0.5f, 0), new Vector2(bw, h));
        }

        private void CreateBorderSegment(Transform parent, Vector3 localPos, Vector2 size)
        {
            var go = new GameObject("Segment");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePixelSprite();
            sr.color = boardConfig.borderColor;
            sr.sortingLayerName = boardSortingLayer;
            sr.sortingOrder = boardSortingOrder - 1;

            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void HandleCellClick(Cell cell) => OnCellClicked?.Invoke(cell);

        private static Sprite _pixelSprite;
        public static Sprite CreatePixelSprite()
        {
            if (_pixelSprite != null) return _pixelSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _pixelSprite;
        }
    }
}