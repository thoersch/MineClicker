using System;
using UnityEngine;

namespace IdleMine
{
    /// <summary>Every sound effect in the game. Each one maps to one or more clips on AudioManager.</summary>
    public enum Sfx { Click, Dig, Crit, Coins, Purchase, Keystone, Deny, Breakthrough, Motherlode, Reward, Cart, Ascend }

    [Serializable]
    public class SfxEntry
    {
        public Sfx id;
        [Tooltip("Several clips = one is picked at random each time.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch spread (0.08 = +/-8%), so repeated sounds don't feel robotic.")]
        [Range(0f, 0.3f)] public float pitchJitter;
        [Tooltip("Ignore repeats closer together than this (seconds). Keeps fast tapping and mass-buying pleasant.")]
        public float minInterval = 0.05f;
    }

    /// <summary>
    /// Music and sound effects. Anything can call AudioManager.Play(Sfx.X); it's a no-op if there's no
    /// AudioManager in the scene or the player turned sound off. Music and sound on/off are remembered
    /// per device. The music fades rather than cutting. Ads pause all audio through AudioListener.pause.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class AudioManager : MonoBehaviour
    {
        const string MusicKey = "audio.music", SoundKey = "audio.sound";

        [SerializeField] AudioClip music;
        [Range(0f, 1f)] [SerializeField] float musicVolume = 0.4f;
        [SerializeField] float musicFadeSeconds = 1.5f;
        [SerializeField] SfxEntry[] sounds = new SfxEntry[0];
        [Tooltip("How many effects can overlap.")]
        [SerializeField] int voices = 12;

        public static AudioManager Instance { get; private set; }

        AudioSource _music;
        AudioSource[] _voices;
        int _nextVoice;
        SfxEntry[] _byId;
        float[] _lastPlayed;
        bool _musicOn, _soundOn;

        public bool MusicOn
        {
            get { return _musicOn; }
            set { _musicOn = value; PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public bool SoundOn
        {
            get { return _soundOn; }
            set { _soundOn = value; PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Play(Sfx id, float volume = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, volume);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _musicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            _soundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;

            int count = Enum.GetValues(typeof(Sfx)).Length;
            _byId = new SfxEntry[count];
            _lastPlayed = new float[count];
            for (int i = 0; i < count; i++) _lastPlayed[i] = -999f;
            foreach (var e in sounds) _byId[(int)e.id] = e;

            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = music;
            _music.loop = true;
            _music.playOnAwake = false;
            _music.priority = 0;
            _music.volume = 0f;

            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _voices[i] = s;
            }
        }

        void Update()
        {
            if (_music.clip == null) return;
            float target = _musicOn ? musicVolume : 0f;
            _music.volume = Mathf.MoveTowards(_music.volume, target, musicVolume / Mathf.Max(0.01f, musicFadeSeconds) * Time.unscaledDeltaTime);
            if (target > 0f && !_music.isPlaying) _music.Play();
            else if (target <= 0f && _music.volume <= 0f && _music.isPlaying) _music.Pause();
        }

        void PlayInternal(Sfx id, float volume)
        {
            if (!_soundOn) return;
            var e = _byId[(int)id];
            if (e == null || e.clips == null || e.clips.Length == 0) return;

            float now = Time.unscaledTime;
            if (now - _lastPlayed[(int)id] < e.minInterval) return;
            _lastPlayed[(int)id] = now;

            var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.pitch = 1f + UnityEngine.Random.Range(-e.pitchJitter, e.pitchJitter);
            voice.PlayOneShot(clip, e.volume * volume);
        }
    }
}
