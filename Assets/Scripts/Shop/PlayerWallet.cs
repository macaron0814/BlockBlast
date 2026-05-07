using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// プレイヤーの「総資産」(所持金) を保持する単一のソース。
    ///
    /// ■ 自動収益連携
    ///   ・GameEvents.OnEnemyDefeated  … 敵撃破ボーナスを加算
    ///   ・GameEvents.OnMoneyEarned    … スーパーチャット等の収益を加算
    /// ■ 購入処理
    ///   ・CanAfford(cost)   … 買えるかチェック
    ///   ・TrySpend(cost)    … 足りていれば差し引いて true を返す
    ///   ・Add(amount)       … デバッグや報酬付与で直接加算
    ///
    /// ■ セットアップ
    ///   1. 任意の永続 GameObject (例: GameManager と同居) にアタッチ
    ///   2. Inspector の startingAssets で初期所持金を指定
    ///   3. UI 表示は GameEvents.OnTotalAssetsChanged を購読する
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [Header("初期総資産")]
        [Tooltip("ゲーム開始時に所持する金額 (円)。デバッグ時は大きな値にして購入テスト可能。")]
        public int startingAssets = 0;

        [Header("自動収益連携")]
        [Tooltip("OnEnemyDefeated のボーナス金額を総資産に加算する")]
        public bool creditEnemyDefeatBonus = true;

        [Tooltip("OnMoneyEarned (スパチャ表示など) を総資産に加算する")]
        public bool creditSuperChat = true;

        [Header("Runtime (read only)")]
        [SerializeField] int _totalAssets;

        public int TotalAssets => _totalAssets;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _totalAssets = Mathf.Max(0, startingAssets);
        }

        void Start()
        {
            GameEvents.TriggerTotalAssetsChanged(_totalAssets);
        }

        void OnEnable()
        {
            GameEvents.OnEnemyDefeated += HandleEnemyDefeated;
            GameEvents.OnMoneyEarned   += HandleMoneyEarned;
        }

        void OnDisable()
        {
            GameEvents.OnEnemyDefeated -= HandleEnemyDefeated;
            GameEvents.OnMoneyEarned   -= HandleMoneyEarned;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void HandleEnemyDefeated(Vector3 _, int amount)
        {
            if (!creditEnemyDefeatBonus || amount <= 0) return;
            Add(amount);
        }

        void HandleMoneyEarned(int amount)
        {
            if (!creditSuperChat || amount <= 0) return;
            Add(amount);
        }

        // ─────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────

        public bool CanAfford(int cost) => cost <= 0 || _totalAssets >= cost;

        public void Add(int amount)
        {
            if (amount == 0) return;
            _totalAssets = Mathf.Max(0, _totalAssets + amount);
            GameEvents.TriggerTotalAssetsChanged(_totalAssets);
        }

        /// <summary>足りていれば差し引いて true を返す。足りなければ何もせず false。</summary>
        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;
            if (_totalAssets < cost) return false;
            _totalAssets -= cost;
            GameEvents.TriggerTotalAssetsChanged(_totalAssets);
            return true;
        }

        public void SetAssets(int amount)
        {
            _totalAssets = Mathf.Max(0, amount);
            GameEvents.TriggerTotalAssetsChanged(_totalAssets);
        }
    }
}
