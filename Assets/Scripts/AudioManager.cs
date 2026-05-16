using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChessTemplate.Data;

namespace ChessTemplate.Audio
{
    public enum SoundPriority
    {
        CheckMate = 0,
        Check = 1,
        Capture = 2,
        Castling = 3,
        Move = 4,
        GameStart = 5,
        GameOver = 6,
        UI = 7
    }

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

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
        public AudioClip menuMusic;
        public AudioClip gameMusic;
        public AudioClip gameOverMusic;
        [Min(0f)] public float musicCrossfadeDuration = 1.5f;

        [Header("Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        private AudioSource _gameSource;
        private AudioSource _uiSource;
        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _activeMusicSource;
        private Coroutine _crossfadeCoroutine;

        private struct SoundRequest
        {
            public AudioClip clip;
            public SoundPriority priority;
        }

        private readonly List<SoundRequest> _pendingRequests = new();
        private bool _flushScheduled;

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

        public void PlayUISound(AudioClip clip) { if (clip != null) PlayImmediate(_uiSource, clip); }
        public void PlayUIClick() => PlayUISound(uiClickClip);
        public void PlayUIOpen() => PlayUISound(uiOpenClip);

        public void PlayMenuMusic() => PlayMusic(menuMusic);
        public void PlayGameMusic() => PlayMusic(gameMusic);
        public void PlayGameOverMusic() => PlayMusic(gameOverMusic);

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            if (_activeMusicSource.clip == clip && _activeMusicSource.isPlaying) return;
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(CrossfadeTo(clip));
        }

        public void StopMusic()
        {
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(FadeOutAll());
        }

        private IEnumerator CrossfadeTo(AudioClip newClip)
        {
            AudioSource incoming = (_activeMusicSource == _musicA) ? _musicB : _musicA;
            AudioSource outgoing = _activeMusicSource;

            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            float target = musicVolume * masterVolume;
            float elapsed = 0f;
            float duration = Mathf.Max(musicCrossfadeDuration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                incoming.volume = Mathf.Lerp(0f, target, t);
                outgoing.volume = Mathf.Lerp(target, 0f, t);
                yield return null;
            }

            incoming.volume = target;
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
            if (_crossfadeCoroutine == null)
                _activeMusicSource.volume = musicVolume * masterVolume;
        }

        private void PlayImmediate(AudioSource source, AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.volume = masterVolume;
            source.PlayOneShot(clip);
        }
    }
}