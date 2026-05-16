using UnityEngine;
using ChessTemplate.Config;

namespace ChessTemplate.Core
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Cell : MonoBehaviour
    {
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool IsLight { get; private set; }

        private SpriteRenderer _baseSr;
        private SpriteRenderer _highlightSr;
        private bool _highlighted;

        public event System.Action<Cell> OnClick;

        public void Init(int row, int col, bool isLight, BoardConfig cfg, string sortingLayer, int sortingOrder)
        {
            Row = row;
            Col = col;
            IsLight = isLight;

            _baseSr = GetComponent<SpriteRenderer>();
            _baseSr.sortingLayerName = sortingLayer;
            _baseSr.sortingOrder = sortingOrder;

            Sprite customSprite = cfg.GetCellSprite(isLight);
            if (customSprite != null)
            {
                _baseSr.sprite = customSprite;
                _baseSr.color = Color.white;
            }
            else
            {
                _baseSr.sprite = BoardGenerator.CreatePixelSprite();
                _baseSr.color = isLight ? cfg.lightCellColor : cfg.darkCellColor;
            }

            transform.localScale = Vector3.one * cfg.cellSize;

            var hlGo = new GameObject("Highlight");
            hlGo.transform.SetParent(transform, false);
            hlGo.transform.localPosition = Vector3.zero;
            hlGo.transform.localScale = Vector3.one;

            _highlightSr = hlGo.AddComponent<SpriteRenderer>();
            _highlightSr.sprite = BoardGenerator.CreatePixelSprite();
            _highlightSr.color = new Color(0, 0, 0, 0);
            _highlightSr.sortingLayerName = sortingLayer;
            _highlightSr.sortingOrder = sortingOrder + 1;

            if (GetComponent<BoxCollider2D>() == null)
                gameObject.AddComponent<BoxCollider2D>();
        }

        public void SetHighlight(Color color)
        {
            _highlighted = true;
            _highlightSr.color = color;
        }

        public void ClearHighlight()
        {
            _highlighted = false;
            _highlightSr.color = new Color(0, 0, 0, 0);
        }

        public void Flash(Color color, float duration = 0.3f)
        {
            StartCoroutine(FlashRoutine(color, duration));
        }

        private System.Collections.IEnumerator FlashRoutine(Color color, float duration)
        {
            _highlightSr.color = color;
            yield return new WaitForSeconds(duration);
            if (!_highlighted) _highlightSr.color = new Color(0, 0, 0, 0);
        }

        private void OnMouseDown() => OnClick?.Invoke(this);
    }
}