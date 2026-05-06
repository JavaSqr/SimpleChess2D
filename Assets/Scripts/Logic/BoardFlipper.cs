using UnityEngine;
using UnityEngine.UI;
using ChessTemplate.Core;
using ChessTemplate.Logic;

namespace ChessTemplate
{
    /// <summary>
    /// Переворачивает доску и все фигуры на 180° по оси Z.
    ///
    /// Переворот означает что игрок команды 1 видит свои фигуры снизу,
    /// как в реальных шахматах при игре за одним столом.
    ///
    /// Два режима (можно включить оба):
    ///   autoFlipOnTurn — переворачивает автоматически при смене хода,
    ///                    только если доска ещё не повёрнута в сторону текущей команды.
    ///   Кнопка flipButton — переворачивает вручную в любой момент.
    ///
    /// Inspector:
    ///   Board Transform  ? Transform объекта Board (BoardGenerator)
    ///   Pieces Transform ? Transform объекта Pieces (PieceSpawner)
    ///   Turn Manager     ? TurnManager
    ///   Flip Button      ? Button (опционально)
    ///   Auto Flip On Turn ? включить авто-переворот при смене хода
    ///   Flip Duration    ? 0 = мгновенно, > 0 = анимация
    /// </summary>
    public class BoardFlipper : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform объекта Board. Именно он вращается.")]
        public Transform boardTransform;

        [Tooltip("Transform объекта Pieces. Вращается синхронно с доской.")]
        public Transform piecesTransform;

        [Tooltip("TurnManager — нужен для подписки на OnTurnChanged.")]
        public TurnManager turnManager;

        [Header("Settings")]
        [Tooltip("Автоматически переворачивать при смене хода (только если нужно).")]
        public bool autoFlipOnTurn = true;

        [Tooltip("Кнопка ручного переворота (опционально).")]
        public Button flipButton;

        [Tooltip("Длительность анимации переворота в секундах. 0 = мгновенно.")]
        [Min(0f)]
        public float flipDuration = 0.3f;

        // ?? Runtime ????????????????????????????????????????????????

        // Текущая ориентация: 0 = нормальная (команда 0 снизу), 1 = перевёрнутая (команда 1 снизу)
        private int _currentOrientation = 0;
        private bool _isAnimating = false;
        private float _animProgress = 0f;
        private float _fromAngle, _toAngle;

        private void Awake()
        {
            flipButton?.onClick.AddListener(FlipManual);
        }

        private void OnEnable()
        {
            if (turnManager != null)
                turnManager.OnTurnChanged.AddListener(OnTurnChanged);
        }

        private void OnDisable()
        {
            if (turnManager != null)
                turnManager.OnTurnChanged.RemoveListener(OnTurnChanged);
        }

        // ?? API ????????????????????????????????????????????????????

        /// <summary>Вручную перевернуть доску (кнопка).</summary>
        public void FlipManual()
        {
            if (_isAnimating) return;
            StartFlip();
        }

        /// <summary>Сбросить ориентацию в исходное положение (новая игра).</summary>
        public void ResetOrientation()
        {
            _isAnimating = false;
            _currentOrientation = 0;
            ApplyRotation(0f);
        }

        // ?? Turn listener ??????????????????????????????????????????

        private void OnTurnChanged(int team)
        {
            if (!autoFlipOnTurn || _isAnimating) return;

            // Переворачиваем только если ориентация не совпадает с текущей командой
            // команда 0 ? ориентация 0 (нормальная)
            // команда 1 ? ориентация 1 (перевёрнутая)
            if (_currentOrientation != team)
                StartFlip();
        }

        // ?? Flip logic ?????????????????????????????????????????????

        private void StartFlip()
        {
            float currentAngle = boardTransform != null
                ? boardTransform.eulerAngles.z
                : 0f;

            _fromAngle = currentAngle;
            _toAngle = currentAngle + 180f;
            _animProgress = 0f;

            if (flipDuration <= 0f)
            {
                ApplyRotation(_toAngle);
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

            float angle = Mathf.LerpAngle(_fromAngle, _toAngle, EaseInOut(_animProgress));
            ApplyRotation(angle);
        }

        private void ApplyRotation(float zAngle)
        {
            var rot = Quaternion.Euler(0f, 0f, zAngle);
            if (boardTransform != null) boardTransform.rotation = rot;
            if (piecesTransform != null) piecesTransform.rotation = rot;
        }

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}