using UnityEngine;
using UnityEngine.Events;

namespace ChessTemplate.Logic
{
    public class TimerManager : MonoBehaviour
    {
        [Header("Mode")]
        public bool useCountdown = false;
        public float timeBudgetPerTeam = 300f;

        [Header("Events")]
        public UnityEvent<int, float> OnTeamTimeUpdated;
        public UnityEvent<int> OnTeamTimeExpired;

        public bool IsPaused { get; private set; } = true;
        public int ActiveTeam { get; private set; } = 0;

        private float[] _teamBudgets;
        private int _teamCount = 2;

        public void Init(int teamCount, int startTeam = 0)
        {
            _teamCount = teamCount;
            ActiveTeam = startTeam;
            _teamBudgets = new float[teamCount];
            for (int i = 0; i < teamCount; i++) _teamBudgets[i] = timeBudgetPerTeam;
        }

        public void InitFromSave(int teamCount, int startTeam, float[] savedBudgets)
        {
            _teamCount = teamCount;
            ActiveTeam = startTeam;
            _teamBudgets = new float[teamCount];
            for (int i = 0; i < teamCount; i++)
                _teamBudgets[i] = (savedBudgets != null && i < savedBudgets.Length)
                    ? savedBudgets[i] : timeBudgetPerTeam;
        }

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

        public void StartTimer() => IsPaused = false;
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void TogglePause() => IsPaused = !IsPaused;

        public void SetTeam(int team) => ActiveTeam = Mathf.Clamp(team, 0, _teamCount - 1);

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