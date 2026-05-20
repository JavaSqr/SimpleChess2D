using UnityEngine;
using UnityEngine.Events;
using SimpleChess.Config;
using SimpleChess.Data;

namespace SimpleChess.Logic
{
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        public MoveValidator validator;
        public GameSetupConfig setupConfig;

        [Header("Turn events")]
        public UnityEvent<int> OnTurnChanged;
        public UnityEvent OnTeam0Turn;
        public UnityEvent OnTeam1Turn;

        [Header("Action events")]
        public UnityEvent<MoveRecord> OnPieceMoved;
        public UnityEvent<MoveRecord> OnPieceCaptured;
        public UnityEvent<MoveRecord> OnCastling;

        [Header("Game state events")]
        public UnityEvent<int> OnCheck;
        public UnityEvent<int> OnCheckMate;
        public UnityEvent<int> OnStalemate;

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

        public void EndTurn(MoveRecord record)
        {
            FireMoveEvents(record);

            CurrentTeam = (CurrentTeam + 1) % TeamCount;

            if (validator.IsInCheck(CurrentTeam))
            {
                if (validator.HasNoLegalMoves(CurrentTeam)) OnCheckMate?.Invoke(CurrentTeam);
                else OnCheck?.Invoke(CurrentTeam);
            }
            else if (validator.HasNoLegalMoves(CurrentTeam))
            {
                OnStalemate?.Invoke(CurrentTeam);
            }

            FireTurnEvents(CurrentTeam);
        }

        public void SetTeam(int team) => CurrentTeam = Mathf.Clamp(team, 0, TeamCount - 1);

        private void FireMoveEvents(MoveRecord record)
        {
            switch (record.moveType)
            {
                case MoveType.Castling:
                    OnCastling?.Invoke(record);
                    break;
                case MoveType.EnPassant:
                    OnPieceMoved?.Invoke(record);
                    OnPieceCaptured?.Invoke(record);
                    break;
                default:
                    OnPieceMoved?.Invoke(record);
                    if (record.capturedPiece) OnPieceCaptured?.Invoke(record);
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