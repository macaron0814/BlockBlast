using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class EnemyController : MonoBehaviour
    {
        /// <summary>プレイヤーからの角度距離（度）。大きいほど遠い。</summary>
        [HideInInspector] public float distanceAngle;

        [SerializeField] EnemyData _data;

        [Header("Editor Gizmo Preview")]
        [Tooltip("Play 前にギズモ確認したい場合は EnemyData をここにセット")]
        public EnemyData editorPreviewData;
        int _currentHP;
        bool _isStunned;
        float _stunTimer;

        SpriteRenderer _sr;                  // プレハブ未使用時の単一スプライト
        GameObject _visualInstance;          // プレハブ使用時のインスタンス (子)
        SpriteRenderer[] _visualRenderers;   // プレハブの子から取得した全 SpriteRenderer (tint/flash 用)
        Color[] _visualOriginalColors;       // プレハブ各 SR の元の色 (flash 復帰用)
        bool _usingPrefabVisual;
        float _animTimer;
        int _currentFrame;

        float _archRadius;
        Vector3 _archCenter;
        float _roadScrollSpeed;

        float _knockbackVelocity;
        // ノックバック速度がこの値を下回ったら停止扱い (微小残存を防ぐ)
        const float KnockbackStopThreshold = 0.05f;

        // 出現時に決まるランダムな高さオフセット (EnemySystem.enemyHoverHeightVariance に基づく)
        float _hoverOffset;

        static readonly List<GameObject> _activeHitEffects = new List<GameObject>();

        public bool IsStunned => _isStunned;
        public int CurrentHP => _currentHP;

        /// <summary>当たり判定の中心ワールド座標（hitOffset 適用済み）</summary>
        public Vector3 HitPosition
        {
            get
            {
                float visualAngle = -distanceAngle;
                float rad = visualAngle * Mathf.Deg2Rad;
                var tangent = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                var normal  = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
                return transform.position
                    + tangent * _data.hitOffset.x
                    + normal  * _data.hitOffset.y;
            }
        }

        /// <summary>衝突判定用: hitOffset.x を角度に換算した中心角度（度）</summary>
        public float HitAngleCenter
        {
            get
            {
                if (_archRadius <= 0f) return distanceAngle;
                // arc = r * θ(rad) → θ(deg) = (offset / r) * Rad2Deg
                float offsetDeg = (_data.hitOffset.x / _archRadius) * Mathf.Rad2Deg;
                return distanceAngle - offsetDeg;
            }
        }

        /// <summary>衝突判定用: データの hitAngleRadius（度）</summary>
        public float HitAngleRadius => _data != null ? _data.hitAngleRadius : 4f;

        // ────────────────────────────────────────
        //  Init
        // ────────────────────────────────────────

        public void Initialize(EnemyData data, float archRadius, Vector3 archCenter, float roadScrollSpeed)
        {
            _data = data;
            _archRadius = archRadius;
            _archCenter = archCenter;
            _roadScrollSpeed = roadScrollSpeed;
            _currentHP = data.maxHP;
            distanceAngle = data.spawnDistance;

            // 出現時に高さオフセットを決定 (EnemySystem.enemyHoverHeightVariance を消費)
            float variance = EnemySystem.CurrentHoverHeightVariance;
            _hoverOffset = variance > 0f ? Random.Range(-variance, variance) : 0f;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                gameObject.layer = enemyLayer;

            if (data.visualPrefab != null)
            {
                SetupPrefabVisual(data);
            }
            else
            {
                SetupSpriteVisual(data);
            }

            UpdateVisualPosition();
        }

        void SetupSpriteVisual(EnemyData data)
        {
            _usingPrefabVisual = false;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingLayerName = "Enemy";
            _sr.sortingOrder = 5;
            _sr.color = data.tint;

            if (data.frames != null && data.frames.Length > 0)
                _sr.sprite = data.frames[0];
            else
                _sr.sprite = GetOrCreateCircleSprite();
        }

        void SetupPrefabVisual(EnemyData data)
        {
            _usingPrefabVisual = true;

            _visualInstance = Instantiate(data.visualPrefab, transform);
            _visualInstance.transform.localPosition = data.visualPrefabOffset;
            _visualInstance.transform.localRotation = Quaternion.Euler(data.visualPrefabRotationEuler);
            float pScale = data.visualPrefabScale > 0f ? data.visualPrefabScale : 1f;
            _visualInstance.transform.localScale = Vector3.one * pScale;

            // プレハブ全 SR のレイヤーや tint を統一管理
            _visualRenderers = _visualInstance.GetComponentsInChildren<SpriteRenderer>(true);
            _visualOriginalColors = new Color[_visualRenderers.Length];
            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                var r = _visualRenderers[i];
                if (r == null) continue;

                // SortingLayer / Order は親 (Enemy) と統一
                r.sortingLayerName = "Enemy";
                r.sortingOrder     = 5 + r.sortingOrder; // プレハブ内の相対順を尊重しつつ底上げ

                if (data.applyTintToPrefab)
                    r.color = r.color * data.tint;

                _visualOriginalColors[i] = r.color;
            }

            // 子オブジェクトのレイヤーも合わせる
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                foreach (var t in _visualInstance.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = enemyLayer;
            }
        }

        // ────────────────────────────────────────
        //  Update
        // ────────────────────────────────────────

        void Update()
        {
            if (_data == null) return;

            if (_isStunned)
            {
                _stunTimer -= Time.deltaTime;
                // 道路スクロールと逆方向に同速度を加えることで
                // ワールド上で敵が静止し、プレイヤーが前進するほど後退して見える
                distanceAngle -= _roadScrollSpeed * Time.deltaTime;
                if (_stunTimer <= 0f) Revive();
            }
            else
            {
                // EnemySystem.enemyMoveSpeedMultiplier を全敵共通の速度倍率として乗算
                distanceAngle -= _data.chaseSpeed * EnemySystem.CurrentMoveSpeedMultiplier * Time.deltaTime;
            }

            // ノックバック処理: 弾を受けたとき distanceAngle を後退方向 (+) に押し戻す。
            // 減衰率 (damping) は EnemySystem 側でグローバル指定可能。
            if (_knockbackVelocity > KnockbackStopThreshold)
            {
                distanceAngle += _knockbackVelocity * Time.deltaTime;
                float damping = EnemySystem.CurrentKnockbackDamping;
                _knockbackVelocity = Mathf.Lerp(_knockbackVelocity, 0f, damping * Time.deltaTime);
            }
            else
            {
                _knockbackVelocity = 0f;
            }

            float maxDist = _data != null ? _data.spawnDistance * 2f : 360f;
            distanceAngle = Mathf.Min(distanceAngle, maxDist);

            UpdateAnimation();
            UpdateVisualPosition();
        }

        // ────────────────────────────────────────
        //  Combat
        // ────────────────────────────────────────

        /// <summary>
        /// 1発のヒットを処理する。
        /// hitMultiplier = 同時消しライン数 (1ライン=1x, 2ライン=2x, 3ライン=3x ...) に対応する倍率。
        ///                  EnemySystem.lineClearMultiplierTable で解決された値が渡される。
        /// この倍率は:
        ///   ・敵 HP の減少量 (damage)   = max(1, RoundToInt(hitMultiplier))
        ///   ・ノックバック量             = knockbackPerHit × hitMultiplier × 全体倍率
        /// の両方に効く。
        /// </summary>
        public void TakeSingleHit(float hitMultiplier = 1f)
        {
            // ノックバック (knockbackPerHit × ライン同時消し倍率 × 全体倍率)
            float knockback = _data.knockbackPerHit
                            * hitMultiplier
                            * EnemySystem.CurrentKnockbackMultiplier;
            _knockbackVelocity += knockback;

            // damage: 同時消しライン数倍 (= 倍率を丸めて HP から引く)
            //   1ライン: -1HP / 2ライン: -2HP / 3ライン: -3HP ...
            int damage = Mathf.Max(1, Mathf.RoundToInt(hitMultiplier));

            if (!_isStunned)
            {
                _currentHP -= damage;
                if (_currentHP <= 0)
                {
                    _currentHP = 0;
                    GameEvents.TriggerEnemyDefeated(transform.position, _data.defeatBonusAmount);
                    Stun();
                }
            }

            StartCoroutine(HitFlash());
            SpawnHitEffect();
        }

        void Stun()
        {
            _isStunned = true;
            _stunTimer = _data.stunDuration;
            _currentFrame = 0;
            _animTimer = 0f;
        }

        void Revive()
        {
            _isStunned = false;
            _currentHP = _data.maxHP;
            _currentFrame = 0;
            _animTimer = 0f;
            StartCoroutine(ReviveFlash());
        }

        // ────────────────────────────────────────
        //  Visual
        // ────────────────────────────────────────

        void UpdateAnimation()
        {
            // プレハブ使用時はプレハブ側の Animator/独自スクリプトに任せる
            if (_usingPrefabVisual) return;
            if (_sr == null) return;

            Sprite[] frames = (_isStunned && _data.stunFrames != null && _data.stunFrames.Length > 0)
                ? _data.stunFrames
                : _data.frames;
            if (frames == null || frames.Length == 0) return;

            _animTimer += Time.deltaTime;
            if (_animTimer >= _data.frameRate)
            {
                _animTimer -= _data.frameRate;
                _currentFrame = (_currentFrame + 1) % frames.Length;
                _sr.sprite = frames[_currentFrame];
            }
        }

        void UpdateVisualPosition()
        {
            float visualAngle = -distanceAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            // EnemyData.hoverHeight (軸) + 出現時に抽選した _hoverOffset (上下振れ)
            float effectiveR = _archRadius + _data.hoverHeight + _hoverOffset;
            float x = Mathf.Sin(rad) * effectiveR;
            float y = -_archRadius + Mathf.Cos(rad) * effectiveR;

            transform.position = _archCenter + new Vector3(x, y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, -visualAngle);

            float depthScale = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(distanceAngle / 90f));
            transform.localScale = Vector3.one * _data.scale * depthScale;

            // sortingOrder:
            //  ・Y ソート ON  … Y 座標が低いほど大きい order = 手前 (擬似 3D 奥行き)
            //  ・Y ソート OFF … 旧来通り distanceAngle ベース
            int order;
            if (EnemySystem.CurrentSortByY)
            {
                order = EnemySystem.CurrentYSortBaseOrder
                      - Mathf.RoundToInt(transform.position.y * EnemySystem.CurrentYSortScale);
            }
            else
            {
                order = 5 - Mathf.FloorToInt(distanceAngle / 10f);
            }
            ApplySortingOrder(order);

            if (_isStunned)
            {
                float blink = Mathf.Sin(Time.time * 12f) > 0f ? 1f : 0.4f;
                ApplyAlpha(blink);
            }
        }

        // ────────────────────────────────────────
        //  Visual helpers (sprite モード / prefab モード両対応)
        // ────────────────────────────────────────

        void ApplySortingOrder(int order)
        {
            if (_sr != null) _sr.sortingOrder = order;
            if (_visualRenderers != null)
            {
                for (int i = 0; i < _visualRenderers.Length; i++)
                    if (_visualRenderers[i] != null)
                        _visualRenderers[i].sortingOrder = order + i; // プレハブ内の相対順を維持
            }
        }

        void ApplyColor(Color c)
        {
            if (_sr != null) _sr.color = c;
            if (_visualRenderers != null)
            {
                for (int i = 0; i < _visualRenderers.Length; i++)
                    if (_visualRenderers[i] != null)
                        _visualRenderers[i].color = c;
            }
        }

        void ApplyAlpha(float alpha)
        {
            if (_sr != null)
            {
                Color c = _sr.color; c.a = alpha; _sr.color = c;
            }
            if (_visualRenderers != null)
            {
                for (int i = 0; i < _visualRenderers.Length; i++)
                {
                    if (_visualRenderers[i] == null) continue;
                    Color c = _visualRenderers[i].color;
                    c.a = alpha;
                    _visualRenderers[i].color = c;
                }
            }
        }

        void RestoreOriginalColors()
        {
            if (_sr != null) _sr.color = _data.tint;
            if (_visualRenderers != null && _visualOriginalColors != null)
            {
                for (int i = 0; i < _visualRenderers.Length && i < _visualOriginalColors.Length; i++)
                    if (_visualRenderers[i] != null)
                        _visualRenderers[i].color = _visualOriginalColors[i];
            }
        }

        string ResolveSortingLayerName()
        {
            if (_sr != null) return _sr.sortingLayerName;
            if (_visualRenderers != null && _visualRenderers.Length > 0 && _visualRenderers[0] != null)
                return _visualRenderers[0].sortingLayerName;
            return "Enemy";
        }

        int ResolveSortingOrder()
        {
            if (_sr != null) return _sr.sortingOrder;
            if (_visualRenderers != null && _visualRenderers.Length > 0 && _visualRenderers[0] != null)
                return _visualRenderers[0].sortingOrder;
            return 5;
        }

        void SpawnHitEffect()
        {
            if (_data.hitEffectFrames == null || _data.hitEffectFrames.Length == 0) return;

            var obj = new GameObject("HitEffect");
            obj.transform.position = HitPosition;
            obj.transform.rotation = transform.rotation;
            obj.transform.localScale = Vector3.one * _data.hitEffectScale;
            obj.layer = gameObject.layer;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite           = _data.hitEffectFrames[0];
            sr.color            = _data.hitEffectColor;
            sr.sortingLayerName = ResolveSortingLayerName();
            sr.sortingOrder     = ResolveSortingOrder() + 2;
            _activeHitEffects.Add(obj);

            StartCoroutine(PlayHitEffectAndDestroy(obj, sr));
        }

        IEnumerator PlayHitEffectAndDestroy(GameObject obj, SpriteRenderer sr)
        {
            foreach (var frame in _data.hitEffectFrames)
            {
                if (obj == null) yield break;
                sr.sprite = frame;
                yield return new WaitForSeconds(_data.hitEffectFrameRate);
            }
            if (obj != null)
            {
                _activeHitEffects.Remove(obj);
                Destroy(obj);
            }
        }

        IEnumerator HitFlash()
        {
            if (_sr == null && (_visualRenderers == null || _visualRenderers.Length == 0))
                yield break;

            for (int i = 0; i < 2; i++)
            {
                ApplyColor(Color.white);
                yield return new WaitForSeconds(0.06f);
                RestoreOriginalColors();
                yield return new WaitForSeconds(0.04f);
            }
        }

        IEnumerator ReviveFlash()
        {
            if (_sr == null && (_visualRenderers == null || _visualRenderers.Length == 0))
                yield break;

            for (int i = 0; i < 4; i++)
            {
                ApplyAlpha(0.2f);
                yield return new WaitForSeconds(0.12f);
                ApplyAlpha(1f);
                yield return new WaitForSeconds(0.12f);
            }
            RestoreOriginalColors();
        }

        // ────────────────────────────────────────
        //  Public helpers
        // ────────────────────────────────────────

        public void UpdateArchSettings(float radius, Vector3 center, float scrollSpeed)
        {
            _archRadius = radius;
            _archCenter = center;
            _roadScrollSpeed = scrollSpeed;
        }

        public static void ClearAllHitEffects()
        {
            for (int i = _activeHitEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeHitEffects[i];
                if (effect != null)
                    Destroy(effect);
            }
            _activeHitEffects.Clear();
        }

        public bool HasReachedPlayer(float gameOverAngle = 0.5f) => distanceAngle <= gameOverAngle;

        // ────────────────────────────────────────
        //  Gizmo
        // ────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            EnemyData d = _data != null ? _data : editorPreviewData;
            if (d == null) return;

            float r = _archRadius > 0f ? _archRadius : 50f;

            // 当たり判定オフセット位置（敵スプライト中心からの矢印）
            float visualAngle = -distanceAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            var tangent = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            var normal  = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            Vector3 hitCenter = transform.position
                + tangent * d.hitOffset.x
                + normal  * d.hitOffset.y;

            // hitOffset の方向を矢印で表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, hitCenter);
            Gizmos.DrawSphere(hitCenter, 0.05f);

            // 当たり判定円（角度 → ワールド半径に変換）
            float hitWorldRadius = r * d.hitAngleRadius * Mathf.Deg2Rad;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            DrawWireCircle(hitCenter, hitWorldRadius, 24);

            // 浮き高さ（地面上の投影点 → 敵位置の線）
            if (Mathf.Abs(d.hoverHeight) > 0.001f)
            {
                Vector3 groundPos = _archCenter + new Vector3(
                    Mathf.Sin(rad) * r,
                    -r + Mathf.Cos(rad) * r, 0f);
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
                Gizmos.DrawLine(groundPos, transform.position);
                Gizmos.DrawSphere(groundPos, 0.04f);
            }
        }

        static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        // ────────────────────────────────────────
        //  Default sprite
        // ────────────────────────────────────────

        static Sprite _cachedCircle;

        static Sprite GetOrCreateCircleSprite()
        {
            if (_cachedCircle != null) return _cachedCircle;

            const int size = 32;
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.45f;

            for (int py = 0; py < size; py++)
            for (int px2 = 0; px2 < size; px2++)
            {
                float dx = px2 - c, dy = py - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                px[py * size + px2] = dist <= r
                    ? new Color(1f, 1f, 1f, 1f)
                    : Color.clear;
            }

            tex.SetPixels(px);
            tex.Apply();
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }
    }
}
