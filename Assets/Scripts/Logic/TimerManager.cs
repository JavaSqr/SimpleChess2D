using UnityEngine;
using UnityEngine.Events;

namespace ChessTemplate.Logic
{
    /// <summary>
    /// Countdown-таймер на команду. Общий секундомер удалён.
    /// Если useCountdown = false — таймер просто ничего не делает (можно отключить GameObject).
    ///
    /// Inspector:
    ///   Use Countdown         → включить режим countdown
    ///   Time Budget Per Team  → секунд на команду (по умолчанию 5 минут)
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        [Header("Mode")]
        [Tooltip("Включить countdown-таймер на команду. Если выключено — таймер не работает.")]
        public bool useCountdown = false;

        [Tooltip("Секунд на команду в countdown-режиме.")]
        public float timeBudgetPerTeam = 300f;

        [Header("Events")]
        [Tooltip("Срабатывает каждый кадр в countdown-режиме. Передаёт (teamIndex, remainingSeconds).")]
        public UnityEvent<int, float> OnTeamTimeUpdated;

        [Tooltip("Время команды вышло. Передаёт teamIndex.")]
        public UnityEvent<int> OnTeamTimeExpired;

        // ── Runtime ────────────────────────────────────────────────
        public bool IsPaused { get; private set; } = true;
        public int ActiveTeam { get; private set; } = 0;

        private float[] _teamBudgets;
        private int _teamCount = 2;

        // ── Init ───────────────────────────────────────────────────

        public void Init(int teamCount, int startTeam = 0)
        {
            _teamCount = teamCount;
            ActiveTeam = startTeam;

            _teamBudgets = new float[teamCount];
            for (int i = 0; i < teamCount; i++)
                _teamBudgets[i] = timeBudgetPerTeam;
        }

        /// <summary>Восстановить бюджеты из сохранения.</summary>
        public void InitFromSave(int teamCount, int startTeam, float[] savedBudgets)
        {
            _teamCount = teamCount;
            ActiveTeam = startTeam;

            _teamBudgets = new float[teamCount];
            for (int i = 0; i < teamCount; i++)
                _teamBudgets[i] = (savedBudgets != null && i < savedBudgets.Length)
                    ? savedBudgets[i]
                    : timeBudgetPerTeam;
        }

        // ── Update ─────────────────────────────────────────────────

        private void Update()
        {
            if (!useCountdown || IsPaused || _teamBudgets == null) return;

            _teamBudgets[ActiveTeam] -= Time.deltaTime;
            float remaining = Mathf.Max(0f, _teamBudgets[ActiveTeam]);
            OnTeamTimeUpdated?.Invoke(ActiveTeam, remaining);

            if (_teamBudgets[ActiveTeam] <= 0f)
            {
                _teamBudgets[ActiveTeam] = 0f;
                Pause();
                OnTeamTimeExpired?.Invoke(ActiveTeam);
            }
        }

        // ── API ────────────────────────────────────────────────────

        public void StartTimer() => IsPaused = false;
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void TogglePause() => IsPaused = !IsPaused;

        public void SetTeam(int team)
            => ActiveTeam = Mathf.Clamp(team, 0, _teamCount - 1);

        public float GetTeamRemaining(int team)
        {
            if (_teamBudgets == null || team >= _teamBudgets.Length) return 0f;
            return Mathf.Max(0f, _teamBudgets[team]);
        }

        public float[] GetAllBudgets() => _teamBudgets;

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}