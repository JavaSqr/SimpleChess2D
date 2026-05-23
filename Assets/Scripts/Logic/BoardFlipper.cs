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

        [Tooltip("X = time 0..1, Y = progress 0..1 where 0 = start angle, 1 = +180 degrees.")]
        public AnimationCurve flipCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private int _currentOrientation = 0;
        private bool _isAnimating;
        private float _animTime;
        private float _fromAngle, _toAngle;

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
            _animTime = 0f;

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

            _animTime += Time.deltaTime / flipDuration;
            float t = Mathf.Clamp01(_animTime);

            if (t >= 1f)
            {
                _isAnimating = false;
                _currentOrientation = 1 - _currentOrientation;
            }

            float progress = flipCurve.Evaluate(t);
            ApplyAngle(_fromAngle + (_toAngle - _fromAngle) * progress);
        }

        private void ApplyAngle(float zAngle)
        {
            CurrentBoardAngle = zAngle;

            if (boardTransform != null)
                boardTransform.rotation = Quaternion.Euler(0f, 0f, zAngle);
        }
    }
}