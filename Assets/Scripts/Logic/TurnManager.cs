using UnityEngine;
using UnityEngine.Events;
using ChessTemplate.Config;
using ChessTemplate.Data;

namespace ChessTemplate.Logic
{
    /// <summary>
    /// Отслеживает очерёдность ходов и генерирует все игровые ивенты.
    /// Прикрепи к Logic GameObject.
    ///
    /// Inspector — назначь:
    ///   Validator    → MoveValidator
    ///   Setup Config → GameSetupConfig
    ///   OnCheckMate  → GameManager.OnCheckMate(int)
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        public MoveValidator validator;
        public GameSetupConfig setupConfig;

        // ── Ивенты хода ────────────────────────────────────────────

        [Header("Turn events")]
        [Tooltip("Срабатывает при любой смене хода. Передаёт индекс команды чья очередь.")]
        public UnityEvent<int> OnTurnChanged;

        [Tooltip("Срабатывает когда наступает ход команды 0 (белые по умолчанию).")]
        public UnityEvent OnTeam0Turn;

        [Tooltip("Срабатывает когда наступает ход команды 1 (чёрные по умолчанию).")]
        public UnityEvent OnTeam1Turn;

        // ── Ивенты действий ────────────────────────────────────────

        [Header("Action events")]
        [Tooltip("Любой обычный ход (не рокировка, не эн пасант). Передаёт MoveRecord.")]
        public UnityEvent<MoveRecord> OnPieceMoved;

        [Tooltip("Фигура захвачена (обычный захват или эн пасант). Передаёт MoveRecord.")]
        public UnityEvent<MoveRecord> OnPieceCaptured;

        [Tooltip("Выполнена рокировка. Передаёт MoveRecord (moveType = Castling).")]
        public UnityEvent<MoveRecord> OnCastling;

        // ── Ивенты состояния игры ──────────────────────────────────

        [Header("Game state events")]
        [Tooltip("Команда под шахом. Передаёт индекс команды.")]
        public UnityEvent<int> OnCheck;

        [Tooltip("Мат. Передаёт индекс проигравшей команды.")]
        public UnityEvent<int> OnCheckMate;

        [Tooltip("Пат. Передаёт индекс команды без ходов.")]
        public UnityEvent<int> OnStalemate;

        // ── Runtime ────────────────────────────────────────────────

        public int CurrentTeam { get; private set; } = 0;
        public int TeamCount { get; private set; } = 2;

        private void Awake()
        {
            if (setupConfig != null)
            {
                CurrentTeam = setupConfig.startingTeamTurn;
                TeamCount = setupConfig.teams.Count;
            }
        }

        public void StartGame()
        {
            CurrentTeam = setupConfig != null ? setupConfig.startingTeamTurn : 0;
            FireTurnEvents(CurrentTeam);
        }

        /// <summary>
        /// Вызывается SelectionHandler'ом после каждого хода.
        /// record содержит тип хода и данные для ивентов.
        /// </summary>
        public void EndTurn(MoveRecord record)
        {
            // Сначала стреляем ивенты самого хода
            FireMoveEvents(record);

            // Переключаем команду
            CurrentTeam = (CurrentTeam + 1) % TeamCount;

            // Проверяем шах / мат / пат
            if (validator.IsInCheck(CurrentTeam))
            {
                if (validator.HasNoLegalMoves(CurrentTeam))
                    OnCheckMate?.Invoke(CurrentTeam);
                else
                    OnCheck?.Invoke(CurrentTeam);
            }
            else if (validator.HasNoLegalMoves(CurrentTeam))
            {
                OnStalemate?.Invoke(CurrentTeam);
            }

            FireTurnEvents(CurrentTeam);
        }

        public void SetTeam(int team)
        {
            CurrentTeam = Mathf.Clamp(team, 0, TeamCount - 1);
        }

        // ── Internal ───────────────────────────────────────────────

        private void FireMoveEvents(MoveRecord record)
        {
            switch (record.moveType)
            {
                case MoveType.Castling:
                    OnCastling?.Invoke(record);
                    break;

                case MoveType.EnPassant:
                    // Эн пасант — это всегда захват
                    OnPieceMoved?.Invoke(record);
                    OnPieceCaptured?.Invoke(record);
                    break;

                default:
                    OnPieceMoved?.Invoke(record);
                    if (record.capturedPiece)
                        OnPieceCaptured?.Invoke(record);
                    break;
            }
        }

        private void FireTurnEvents(int team)
        {
            OnTurnChanged?.Invoke(team);

            if (team == 0) OnTeam0Turn?.Invoke();
            else if (team == 1) OnTeam1Turn?.Invoke();
        }
    }
}