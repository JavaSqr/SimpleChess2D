using UnityEngine;
using UnityEngine.UI;
using SimpleChess.Logic;

namespace SimpleChess
{
    public class BoardFlipper : MonoBehaviour
    {
        [Header("References")]
        public Transform boardTransform;
        public TurnManager turnManager;

        [Header("Settings")]
        public bool autoFlipOnTurn = true;
        public Button flipButton;
        [Min(0f)] public float flipDuration = 0.3f;

        private int _currentOrientation = 0;
        private bool _isAnimating;
        private float _animProgress;
        private float _fromAngle, _toAngle;

        // Current board angle, pieces read this to counter-rotate
        public float CurrentBoardAngle { get; private set; } = 0f;

        private void Awake() => flipButton?.onClick.AddListener(FlipManual);

        private void OnEnable()
        {
            if (turnManager != null) turnManager.OnTurnChanged.AddListener(OnTurnChanged);
        }

        private void OnDisable()
        {
            if (turnManager != null) turnManager.OnTurnChanged.RemoveListener(OnTurnChanged);
        }

        public void FlipManual()
        {
            if (_isAnimating) return;
            StartFlip();
        }

        public void ResetOrientation()
        {
            _isAnimating = false;
            _currentOrientation = 0;
            ApplyAngle(0f);
        }

        private void OnTurnChanged(int team)
        {
            if (!autoFlipOnTurn || _isAnimating) return;
            if (_currentOrientation != team) StartFlip();
        }

        private void StartFlip()
        {
            _fromAngle = CurrentBoardAngle;
            _toAngle = _fromAngle + 180f;
            _animProgress = 0f;

            if (flipDuration <= 0f)
            {
                ApplyAngle(_toAngle);
                _currentOrientation = 1 - _currentOrientation;
            }
            else
            {
                _isAnimating = true;
            }
        }

        private void Update()
        {
            if (!_isAnimating) return;

            _animProgress += Time.deltaTime / flipDuration;
            if (_animProgress >= 1f)
            {
                _animProgress = 1f;
                _isAnimating = false;
                _currentOrientation = 1 - _currentOrientation;
            }

            ApplyAngle(Mathf.LerpAngle(_fromAngle, _toAngle, EaseInOut(_animProgress)));
        }

        private void ApplyAngle(float zAngle)
        {
            CurrentBoardAngle = zAngle;

            if (boardTransform != null)
                boardTransform.rotation = Quaternion.Euler(0f, 0f, zAngle);
        }

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}