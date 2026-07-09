using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public enum SoundCue
    {
        GameStart,
        BlockPlace,
        BlockHover,
        BlockGrab,
        BlockCancel,
        FoodDigest,
        BulletShoot,
        BulletBounce,
        BulletHit,
        MoneyEarned,
        ShopSelect,
        ShopConfirm,
        Pause,
        EnemyDefeat,
        BossDefeat,
        HighSuperChat,
        GameClear,
        GameOver,
        VolumeAdjust,
    }

    public enum BgmCue
    {
        Title,
        Game,
        Shop,
    }

    /// <summary>
    /// Assets/Sound の効果音をシチュエーション別に再生する共通マネージャ。
    /// GameEvents で拾えるものは自動購読し、細かい操作音は SoundManager.Play(cue) から鳴らす。
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public class SoundManager : MonoBehaviour
    {
        [System.Serializable]
        public class SoundEntry
        {
            public SoundCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Min(0f)] public float cooldown = 0.02f;
        }

        [System.Serializable]
        public class BgmEntry
        {
            public BgmCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop = true;
        }

        public static SoundManager Instance { get; private set; }
        public static event System.Action<int, int> OnVolumeLevelsChanged;

        const string SEVolumeLevelKey = "BlockBlast.SEVolumeLevel";
        const string BGMVolumeLevelKey = "BlockBlast.BGMVolumeLevel";

        [Header("SE Audio Source")]
        [Tooltip("SE 専用 AudioSource。既存の SoundManager に付いている AudioSource は SE 用のまま変更しない。未設定なら Awake で取得/追加する。")]
        public AudioSource audioSource;

        [Header("BGM Audio Source")]
        [Tooltip("BGM 専用 AudioSource。SE 用 audioSource とは必ず別にする。未設定なら子 GameObject に自動作成する。")]
        public AudioSource bgmAudioSource;

        [Header("Volume")]
        [Tooltip("SE の最大音量。実際の音量 = seMaxVolume × (seVolumeLevel / 10)。")]
        [Range(0f, 1f)]
        public float seMaxVolume = 1f;

        [Tooltip("BGM の最大音量。実際の音量 = bgmMaxVolume × (bgmVolumeLevel / 10)。")]
        [Range(0f, 1f)]
        public float bgmMaxVolume = 1f;

        [Tooltip("SE 音量段階。0=無音 / 10=seMaxVolume。")]
        [Range(0, 10)]
        public int seVolumeLevel = 10;

        [Tooltip("BGM 音量段階。0=無音 / 10=bgmMaxVolume。")]
        [Range(0, 10)]
        public int bgmVolumeLevel = 10;

        [Tooltip("(旧) SE の masterVolume。互換用。新規設定では seMaxVolume / seVolumeLevel を使う。")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Tooltip("ON: シーン切り替えでも破棄しない。")]
        public bool dontDestroyOnLoad = false;

        [Header("Event Settings")]
        [Tooltip("この金額以上の OnMoneyEarned は HighSuperChat を鳴らす。")]
        public int highSuperChatThreshold = 10000;

        [Header("Sound Entries")]
        public List<SoundEntry> sounds = new List<SoundEntry>();

        [Header("BGM Entries")]
        public List<BgmEntry> bgms = new List<BgmEntry>();

        readonly Dictionary<SoundCue, SoundEntry> _byCue = new Dictionary<SoundCue, SoundEntry>();
        readonly Dictionary<BgmCue, BgmEntry> _byBgmCue = new Dictionary<BgmCue, BgmEntry>();
        readonly Dictionary<SoundCue, float> _lastPlayedRealtime = new Dictionary<SoundCue, float>();
        BgmCue? _currentBgmCue;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            EnsureBgmAudioSource();
            LoadVolumeSettings();
            RebuildLookup();
        }

        void OnEnable()
        {
            GameEvents.OnBlockPlaced += HandleBlockPlaced;
            GameEvents.OnLineClear += HandleLineClear;
            GameEvents.OnEnemyDefeated += HandleEnemyDefeated;
            GameEvents.OnMoneyEarned += HandleMoneyEarned;
            GameEvents.OnGameClear += HandleGameClear;
            GameEvents.OnGameOver += HandleGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnBlockPlaced -= HandleBlockPlaced;
            GameEvents.OnLineClear -= HandleLineClear;
            GameEvents.OnEnemyDefeated -= HandleEnemyDefeated;
            GameEvents.OnMoneyEarned -= HandleMoneyEarned;
            GameEvents.OnGameClear -= HandleGameClear;
            GameEvents.OnGameOver -= HandleGameOver;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void Play(SoundCue cue)
        {
            if (Instance == null) return;
            Instance.PlayCue(cue);
        }

        public static void PlayBgm(BgmCue cue)
        {
            if (Instance == null) return;
            Instance.PlayBgmCue(cue);
        }

        public static void StopBgm()
        {
            if (Instance == null) return;
            Instance.StopBgmCue();
        }

        public static void SetSEVolumeLevel(int level)
        {
            if (Instance == null) return;
            Instance.SetSeLevel(level);
        }

        public static void SetBGMVolumeLevel(int level)
        {
            if (Instance == null) return;
            Instance.SetBgmLevel(level);
        }

        public static void AddSEVolumeLevel(int delta)
        {
            if (Instance == null) return;
            Instance.SetSeLevel(Instance.seVolumeLevel + delta);
        }

        public static void AddBGMVolumeLevel(int delta)
        {
            if (Instance == null) return;
            Instance.SetBgmLevel(Instance.bgmVolumeLevel + delta);
        }

        public void PlayCue(SoundCue cue)
        {
            if (_byCue.Count != sounds.Count)
                RebuildLookup();

            if (!_byCue.TryGetValue(cue, out var entry)) return;
            if (entry == null || entry.clip == null || audioSource == null) return;

            float now = Time.realtimeSinceStartup;
            if (_lastPlayedRealtime.TryGetValue(cue, out float last)
                && now - last < entry.cooldown)
            {
                return;
            }

            _lastPlayedRealtime[cue] = now;
            audioSource.PlayOneShot(entry.clip, Mathf.Clamp01(GetSEVolume() * entry.volume));
        }

        public void PlayBgmCue(BgmCue cue)
        {
            if (_byBgmCue.Count != bgms.Count)
                RebuildLookup();

            if (!_byBgmCue.TryGetValue(cue, out var entry)) return;
            if (entry == null || entry.clip == null) return;

            EnsureBgmAudioSource();
            if (bgmAudioSource == null) return;

            if (_currentBgmCue.HasValue
                && _currentBgmCue.Value.Equals(cue)
                && bgmAudioSource.clip == entry.clip
                && bgmAudioSource.isPlaying)
            {
                ApplyBgmVolume();
                return;
            }

            _currentBgmCue = cue;
            bgmAudioSource.clip = entry.clip;
            bgmAudioSource.loop = entry.loop;
            bgmAudioSource.volume = Mathf.Clamp01(GetBGMVolume() * entry.volume);
            bgmAudioSource.Play();
        }

        public void StopBgmCue()
        {
            if (bgmAudioSource != null)
                bgmAudioSource.Stop();
            _currentBgmCue = null;
        }

        public float GetSEVolume() => Mathf.Clamp01(seMaxVolume * (Mathf.Clamp(seVolumeLevel, 0, 10) / 10f));

        public float GetBGMVolume() => Mathf.Clamp01(bgmMaxVolume * (Mathf.Clamp(bgmVolumeLevel, 0, 10) / 10f));

        public void SetSeLevel(int level)
        {
            seVolumeLevel = Mathf.Clamp(level, 0, 10);
            SaveVolumeSettings();
            OnVolumeLevelsChanged?.Invoke(seVolumeLevel, bgmVolumeLevel);
            PlayCue(SoundCue.VolumeAdjust);
        }

        public void SetBgmLevel(int level)
        {
            bgmVolumeLevel = Mathf.Clamp(level, 0, 10);
            ApplyBgmVolume();
            SaveVolumeSettings();
            OnVolumeLevelsChanged?.Invoke(seVolumeLevel, bgmVolumeLevel);
            PlayCue(SoundCue.VolumeAdjust);
        }

        void RebuildLookup()
        {
            _byCue.Clear();
            for (int i = 0; i < sounds.Count; i++)
            {
                var entry = sounds[i];
                if (entry == null) continue;
                _byCue[entry.cue] = entry;
            }

            _byBgmCue.Clear();
            for (int i = 0; i < bgms.Count; i++)
            {
                var entry = bgms[i];
                if (entry == null) continue;
                _byBgmCue[entry.cue] = entry;
            }
        }

        void EnsureBgmAudioSource()
        {
            if (bgmAudioSource != null)
                return;

            var child = transform.Find("BGM Audio Source");
            if (child == null)
            {
                var obj = new GameObject("BGM Audio Source");
                obj.transform.SetParent(transform, false);
                child = obj.transform;
            }

            bgmAudioSource = child.GetComponent<AudioSource>();
            if (bgmAudioSource == null)
                bgmAudioSource = child.gameObject.AddComponent<AudioSource>();

            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true;
            ApplyBgmVolume();
        }

        void ApplyBgmVolume()
        {
            if (bgmAudioSource == null) return;

            float entryVolume = 1f;
            if (_currentBgmCue.HasValue && _byBgmCue.TryGetValue(_currentBgmCue.Value, out var entry) && entry != null)
                entryVolume = entry.volume;

            bgmAudioSource.volume = Mathf.Clamp01(GetBGMVolume() * entryVolume);
        }

        void LoadVolumeSettings()
        {
            seVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(SEVolumeLevelKey, seVolumeLevel), 0, 10);
            bgmVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(BGMVolumeLevelKey, bgmVolumeLevel), 0, 10);
        }

        void SaveVolumeSettings()
        {
            PlayerPrefs.SetInt(SEVolumeLevelKey, seVolumeLevel);
            PlayerPrefs.SetInt(BGMVolumeLevelKey, bgmVolumeLevel);
            PlayerPrefs.Save();
        }

        void HandleBlockPlaced() => PlayCue(SoundCue.BlockPlace);

        void HandleLineClear(int linesCleared, int comboCount)
        {
            if (linesCleared > 0)
                PlayCue(SoundCue.FoodDigest);
        }

        void HandleEnemyDefeated(Vector3 _, int bonusAmount)
        {
            PlayCue(SoundCue.EnemyDefeat);
        }

        void HandleMoneyEarned(int amount)
        {
            PlayCue(amount >= highSuperChatThreshold ? SoundCue.HighSuperChat : SoundCue.MoneyEarned);
        }

        void HandleGameClear() => PlayCue(SoundCue.GameClear);

        void HandleGameOver(GameOverType _) => PlayCue(SoundCue.GameOver);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (masterVolume < 0f) masterVolume = 0f;
            seMaxVolume = Mathf.Clamp01(seMaxVolume);
            bgmMaxVolume = Mathf.Clamp01(bgmMaxVolume);
            seVolumeLevel = Mathf.Clamp(seVolumeLevel, 0, 10);
            bgmVolumeLevel = Mathf.Clamp(bgmVolumeLevel, 0, 10);

            EnsureSoundEntryExists(SoundCue.GameStart);
            EnsureSoundEntryExists(SoundCue.GameOver);
            EnsureBgmEntryExists(BgmCue.Title);
            EnsureBgmEntryExists(BgmCue.Game);
            EnsureBgmEntryExists(BgmCue.Shop);
        }

        void EnsureSoundEntryExists(SoundCue cue)
        {
            if (sounds == null)
                sounds = new List<SoundEntry>();

            for (int i = 0; i < sounds.Count; i++)
            {
                if (sounds[i] != null && sounds[i].cue == cue)
                    return;
            }

            sounds.Add(new SoundEntry { cue = cue });
        }

        void EnsureBgmEntryExists(BgmCue cue)
        {
            if (bgms == null)
                bgms = new List<BgmEntry>();

            for (int i = 0; i < bgms.Count; i++)
            {
                if (bgms[i] != null && bgms[i].cue == cue)
                    return;
            }

            bgms.Add(new BgmEntry { cue = cue, loop = true });
        }
#endif
    }
}
