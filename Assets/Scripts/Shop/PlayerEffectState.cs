using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップで購入したアイテム効果の累積値を保持する単一のソース。
    ///
    /// ■ 設計
    ///   ・各システム (EnemySystem / PlayerBullet / EnemyController など) が
    ///     PlayerEffectState.Instance を参照して効果値を取りに行く Pull 型。
    ///   ・効果は加算 (BulletCountBonus 等) と乗算 (BulletSizeMultiplier 等) の 2 形態あり、
    ///     同じカテゴリを複数回購入したときは「掛け算で累積」する。
    ///   ・即時発動系 (BlockReset / MoneyBonus) は ApplyPurchase 内で直接対象に効果を出す。
    ///
    /// ■ 反映先サマリ
    ///   BulletSize          → EnemySystem.bulletScale, bulletHitAngleRadius (×)
    ///   BulletSpeed         → EnemySystem.bulletSpeed                       (×)
    ///   BulletCount         → EnemySystem.FireBulletBurst の発射数         (+)
    ///   Penetration         → PlayerBullet の貫通カウント                  (+)
    ///   PlayerSpeed         → EnemySystem.CurrentPlayerSpeedMultiplier     (×) → 敵の相対速度を遅くする
    ///   BlockReset          → BoardManager.ClearBoard()                     (即時)
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class PlayerEffectState : MonoBehaviour
    {
        public static PlayerEffectState Instance { get; private set; }

        [Header("Optional References (省略時は FindObjectOfType)")]
        [Tooltip("BlockReset 即時発動用の BoardManager 参照。空のとき FindObjectOfType で自動取得")]
        public BoardManager boardManager;

        [Tooltip("購入時の効果値テーブル参照。空のとき ShopCardSelector のものを使う")]
        public ShopItemEffectTable effectTable;

        [Header("Runtime (read only)")]
        [SerializeField] float _bulletSizeMultiplier  = 1f;
        [SerializeField] float _bulletSpeedMultiplier = 1f;
        [SerializeField] int   _bulletCountBonus      = 0;
        [SerializeField] int   _penetrationBonus      = 0;
        [SerializeField] float _playerSpeedMultiplier = 1f;
        [SerializeField] int   _blockResetUsedCount   = 0;   // 統計用

        public float BulletSizeMultiplier  => _bulletSizeMultiplier;
        public float BulletSpeedMultiplier => _bulletSpeedMultiplier;
        public int   BulletCountBonus      => _bulletCountBonus;
        public int   PenetrationBonus      => _penetrationBonus;
        public float PlayerSpeedMultiplier => _playerSpeedMultiplier;

        public event System.Action OnEffectsChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─────────────────────────────────────
        //  Purchase 適用
        // ─────────────────────────────────────

        /// <summary>
        /// ShopItemData の効果を累積適用する。
        /// effectTable が null のときは this.effectTable をフォールバックに使う。
        /// </summary>
        public void ApplyPurchase(ShopItemData item, ShopItemEffectTable table = null)
        {
            if (item == null) return;
            var t = table != null ? table : effectTable;
            float v = (t != null) ? t.GetValue(item.category, item.tierIndex) : 0f;

            switch (item.category)
            {
                case ShopItemCategory.BulletSize:
                    if (v > 0f) _bulletSizeMultiplier *= v;
                    Debug.Log($"[PlayerEffectState] BulletSize ×{v:F2} → 累積 ×{_bulletSizeMultiplier:F3}");
                    break;

                case ShopItemCategory.BulletSpeed:
                    if (v > 0f) _bulletSpeedMultiplier *= v;
                    Debug.Log($"[PlayerEffectState] BulletSpeed ×{v:F2} → 累積 ×{_bulletSpeedMultiplier:F3}");
                    break;

                case ShopItemCategory.BulletCount:
                    int addCount = Mathf.RoundToInt(v);
                    _bulletCountBonus += addCount;
                    Debug.Log($"[PlayerEffectState] BulletCount +{addCount} → 累積 +{_bulletCountBonus}");
                    break;

                case ShopItemCategory.Penetration:
                    int addPen = Mathf.RoundToInt(v);
                    _penetrationBonus += addPen;
                    Debug.Log($"[PlayerEffectState] Penetration +{addPen} → 累積 +{_penetrationBonus}");
                    break;

                case ShopItemCategory.PlayerSpeed:
                    if (v > 0f) _playerSpeedMultiplier *= v;
                    Debug.Log($"[PlayerEffectState] PlayerSpeed ×{v:F2} → 累積 ×{_playerSpeedMultiplier:F3}");
                    break;

                case ShopItemCategory.BlockReset:
                    if (boardManager == null) boardManager = FindObjectOfType<BoardManager>();
                    if (boardManager != null)
                    {
                        boardManager.ClearBoard();
                        _blockResetUsedCount++;
                        Debug.Log($"[PlayerEffectState] BlockReset 実行 (累計 {_blockResetUsedCount} 回)");
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerEffectState] BlockReset: BoardManager が見つからないのでスキップ");
                    }
                    break;

                default:
                    // 未実装カテゴリ (BlockRescue / MoneyBonus / LuckUp / SaveOnce) はここで何もしない
                    Debug.Log($"[PlayerEffectState] {item.category} は未実装カテゴリのためスキップ");
                    break;
            }

            OnEffectsChanged?.Invoke();
        }

        // ─────────────────────────────────────
        //  Reset (ゲームオーバー / 次プレイ用)
        // ─────────────────────────────────────

        /// <summary>すべての効果をリセットして初期状態に戻す。</summary>
        public void ResetAll()
        {
            _bulletSizeMultiplier  = 1f;
            _bulletSpeedMultiplier = 1f;
            _bulletCountBonus      = 0;
            _penetrationBonus      = 0;
            _playerSpeedMultiplier = 1f;
            _blockResetUsedCount   = 0;
            OnEffectsChanged?.Invoke();
        }
    }
}
