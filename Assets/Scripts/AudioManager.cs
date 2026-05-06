using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChessTemplate.Data;

namespace ChessTemplate.Audio
{
    // ── Приоритеты звуков (чем меньше число — тем выше приоритет) ─
    // Если за один ход происходит несколько событий (например захват + шах),
    // играет только звук с наименьшим значением Priority.
    public enum SoundPriority
    {
        CheckMate = 0,   // высший приоритет
        Check = 1,
        Capture = 2,
        Castling = 3,
        Move = 4,
        GameStart = 5,
        GameOver = 6,
        UI = 7    // низший приоритет
    }

    /// <summary>
    /// Аудиоменеджер с системой приоритетов и плавным переключением фоновой музыки.
    ///
    /// Звуковые эффекты:
    ///   За один «игровой момент» (EndTurn) может поступить несколько запросов.
    ///   Из них проигрывается только один — с наивысшим приоритетом.
    ///   UI-звуки воспроизводятся немедленно без очереди.
    ///
    /// Фоновая музыка:
    ///   Используются два AudioSource (_musicA, _musicB), которые чередуются
    ///   при кроссфейде. Вызови PlayMusic(clip) для плавного перехода к новому треку.
    ///   Например: PlayMusic(menuMusic) при открытии меню,
    ///             PlayMusic(gameMusic) при старте игры,
    ///             PlayMusic(gameOverMusic) при конце партии.
    ///
    /// Inspector:
    ///   Назначь AudioClip для каждого события.
    ///   Music: Menu / Game / GameOver — треки для каждого состояния.
    ///   Music Crossfade Duration — длительность кроссфейда (сек).
    ///   Music Volume — громкость музыки (0–1).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // ── Clips ──────────────────────────────────────────────────
        [Header("Game sounds")]
        public AudioClip moveClip;
        public AudioClip captureClip;
        public AudioClip castlingClip;
        public AudioClip checkClip;
        public AudioClip checkMateClip;
        public AudioClip stalemateClip;
        public AudioClip gameStartClip;
        public AudioClip gameOverClip;

        [Header("UI sounds")]
        public AudioClip uiClickClip;
        public AudioClip uiOpenClip;

        [Header("Music")]
        [Tooltip("Музыка главного меню.")]
        public AudioClip menuMusic;

        [Tooltip("Музыка во время игры.")]
        public AudioClip gameMusic;

        [Tooltip("Музыка на экране конца игры.")]
        public AudioClip gameOverMusic;

        [Tooltip("Длительность плавного перехода между треками (сек).")]
        [Min(0f)]
        public float musicCrossfadeDuration = 1.5f;

        [Header("Settings")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Range(0f, 1f)]
        public float musicVolume = 0.5f;

        // ── Sources ────────────────────────────────────────────────
        private AudioSource _gameSource;    // игровые SFX (приоритетная очередь)
        private AudioSource _uiSource;      // UI-звуки (немедленно)
        private AudioSource _musicA;        // музыкальный слой A
        private AudioSource _musicB;        // музыкальный слой B

        // Текущий активный музыкальный источник
        private AudioSource _activeMusicSource;
        private Coroutine _crossfadeCoroutine;

        // ── Priority queue ─────────────────────────────────────────
        private struct SoundRequest
        {
            public AudioClip clip;
            public SoundPriority priority;
        }

        private readonly List<SoundRequest> _pendingRequests = new();
        private bool _flushScheduled = false;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _gameSource = CreateSource(false);
            _uiSource = CreateSource(false);
            _musicA = CreateSource(true);
            _musicB = CreateSource(true);

            _activeMusicSource = _musicA;
        }

        private AudioSource CreateSource(bool loop)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            return src;
        }

        // ── LateUpdate: воспроизводим лучший звук из очереди ──────

        private void LateUpdate()
        {
            if (!_flushScheduled || _pendingRequests.Count == 0)
            {
                _flushScheduled = false;
                return;
            }

            SoundRequest best = _pendingRequests[0];
            foreach (var req in _pendingRequests)
                if ((int)req.priority < (int)best.priority)
                    best = req;

            _pendingRequests.Clear();
            _flushScheduled = false;

            PlayImmediate(_gameSource, best.clip);
        }

        // ── Public API — игровые SFX ───────────────────────────────

        public void RequestSound(AudioClip clip, SoundPriority priority)
        {
            if (clip == null) return;
            _pendingRequests.Add(new SoundRequest { clip = clip, priority = priority });
            _flushScheduled = true;
        }

        public void OnMove(MoveRecord _) => RequestSound(moveClip, SoundPriority.Move);
        public void OnCapture(MoveRecord _) => RequestSound(captureClip, SoundPriority.Capture);
        public void OnCastling(MoveRecord _) => RequestSound(castlingClip, SoundPriority.Castling);
        public void OnCheck(int _) => RequestSound(checkClip, SoundPriority.Check);
        public void OnCheckMate(int _) => RequestSound(checkMateClip, SoundPriority.CheckMate);
        public void OnStalemate(int _) => RequestSound(stalemateClip, SoundPriority.CheckMate);
        public void OnGameStart() => RequestSound(gameStartClip, SoundPriority.GameStart);
        public void OnGameOver() => RequestSound(gameOverClip, SoundPriority.GameOver);

        // ── Public API — UI SFX ────────────────────────────────────

        public void PlayUISound(AudioClip clip)
        {
            if (clip == null) return;
            PlayImmediate(_uiSource, clip);
        }

        public void PlayUIClick() => PlayUISound(uiClickClip);
        public void PlayUIOpen() => PlayUISound(uiOpenClip);

        // ── Public API — Музыка ────────────────────────────────────

        /// <summary>
        /// Плавно переключает фоновую музыку на указанный трек.
        /// Если clip == null или совпадает с текущим треком, ничего не происходит.
        /// Длительность кроссфейда — musicCrossfadeDuration.
        /// </summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            if (_activeMusicSource.clip == clip && _activeMusicSource.isPlaying) return;

            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);

            _crossfadeCoroutine = StartCoroutine(CrossfadeTo(clip));
        }

        /// <summary>Плавно останавливает всю фоновую музыку.</summary>
        public void StopMusic()
        {
            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);

            _crossfadeCoroutine = StartCoroutine(FadeOutAll());
        }

        /// <summary>Удобные методы для вызова из GameManager при смене состояния.</summary>
        public void PlayMenuMusic() => PlayMusic(menuMusic);
        public void PlayGameMusic() => PlayMusic(gameMusic);
        public void PlayGameOverMusic() => PlayMusic(gameOverMusic);

        // ── Crossfade coroutine ────────────────────────────────────

        private IEnumerator CrossfadeTo(AudioClip newClip)
        {
            // Определяем «входящий» источник (тот, который сейчас не активен)
            AudioSource incoming = (_activeMusicSource == _musicA) ? _musicB : _musicA;
            AudioSource outgoing = _activeMusicSource;

            // Настраиваем входящий источник
            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            float targetVolume = musicVolume * masterVolume;
            float elapsed = 0f;
            float duration = Mathf.Max(musicCrossfadeDuration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                incoming.volume = Mathf.Lerp(0f, targetVolume, t);
                outgoing.volume = Mathf.Lerp(targetVolume, 0f, t);

                yield return null;
            }

            incoming.volume = targetVolume;
            outgoing.volume = 0f;
            outgoing.Stop();
            outgoing.clip = null;

            _activeMusicSource = incoming;
            _crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutAll()
        {
            float startA = _musicA.volume;
            float startB = _musicB.volume;
            float elapsed = 0f;
            float duration = Mathf.Max(musicCrossfadeDuration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _musicA.volume = Mathf.Lerp(startA, 0f, t);
                _musicB.volume = Mathf.Lerp(startB, 0f, t);
                yield return null;
            }

            _musicA.Stop();
            _musicB.Stop();
            _crossfadeCoroutine = null;
        }

        // ── Volume ─────────────────────────────────────────────────

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            _gameSource.volume = masterVolume;
            _uiSource.volume = masterVolume;
            UpdateMusicVolume();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            UpdateMusicVolume();
        }

        private void UpdateMusicVolume()
        {
            float target = musicVolume * masterVolume;
            // Обновляем только активный источник (другой остановлен или в кроссфейде)
            if (_crossfadeCoroutine == null)
                _activeMusicSource.volume = target;
        }

        // ── Internal ───────────────────────────────────────────────

        private void PlayImmediate(AudioSource source, AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.volume = masterVolume;
            source.PlayOneShot(clip);
        }
    }
}
