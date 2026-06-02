using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public enum SoundCue
    {
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
        VolumeAdjust,
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

        public static SoundManager Instance { get; private set; }

        [Header("Audio Source")]
        [Tooltip("未設定なら Awake で自動追加する。")]
        public AudioSource audioSource;

        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Tooltip("ON: シーン切り替えでも破棄しない。")]
        public bool dontDestroyOnLoad = false;

        [Header("Event Settings")]
        [Tooltip("この金額以上の OnMoneyEarned は HighSuperChat を鳴らす。")]
        public int highSuperChatThreshold = 10000;

        [Header("Sound Entries")]
        public List<SoundEntry> sounds = new List<SoundEntry>();

        readonly Dictionary<SoundCue, SoundEntry> _byCue = new Dictionary<SoundCue, SoundEntry>();
        readonly Dictionary<SoundCue, float> _lastPlayedRealtime = new Dictionary<SoundCue, float>();

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
            RebuildLookup();
        }

        void OnEnable()
        {
            GameEvents.OnBlockPlaced += HandleBlockPlaced;
            GameEvents.OnLineClear += HandleLineClear;
            GameEvents.OnEnemyDefeated += HandleEnemyDefeated;
            GameEvents.OnMoneyEarned += HandleMoneyEarned;
            GameEvents.OnGameClear += HandleGameClear;
        }

        void OnDisable()
        {
            GameEvents.OnBlockPlaced -= HandleBlockPlaced;
            GameEvents.OnLineClear -= HandleLineClear;
            GameEvents.OnEnemyDefeated -= HandleEnemyDefeated;
            GameEvents.OnMoneyEarned -= HandleMoneyEarned;
            GameEvents.OnGameClear -= HandleGameClear;
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
            audioSource.PlayOneShot(entry.clip, Mathf.Clamp01(masterVolume * entry.volume));
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

#if UNITY_EDITOR
        void OnValidate()
        {
            if (masterVolume < 0f) masterVolume = 0f;
        }
#endif
    }
}
