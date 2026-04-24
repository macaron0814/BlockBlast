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

        SpriteRenderer _sr;
        float _animTimer;
        int _currentFrame;

        float _archRadius;
        Vector3 _archCenter;
        float _roadScrollSpeed;

        float _knockbackVelocity;
        const float KnockbackDamping = 4.5f;
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

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                gameObject.layer = enemyLayer;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingLayerName = "Enemy";
            _sr.sortingOrder = 5;
            _sr.color = data.tint;

            if (data.frames != null && data.frames.Length > 0)
                _sr.sprite = data.frames[0];
            else
                _sr.sprite = GetOrCreateCircleSprite();

            UpdateVisualPosition();
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
                distanceAngle -= _data.chaseSpeed * Time.deltaTime;
            }

            if (_knockbackVelocity > 0.5f)
            {
                distanceAngle += _knockbackVelocity * Time.deltaTime;
                _knockbackVelocity = Mathf.Lerp(_knockbackVelocity, 0f, KnockbackDamping * Time.deltaTime);
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
        /// knockbackMultiplier = 同時消しライン数（1ライン=1x, 2ライン=2x）。
        /// </summary>
        public void TakeSingleHit(float knockbackMultiplier = 1f)
        {
            float knockback = _data.knockbackPerHit * knockbackMultiplier;
            _knockbackVelocity += knockback;

            if (!_isStunned)
            {
                _currentHP--;
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
            float effectiveR = _archRadius + _data.hoverHeight;
            float x = Mathf.Sin(rad) * effectiveR;
            float y = -_archRadius + Mathf.Cos(rad) * effectiveR;

            transform.position = _archCenter + new Vector3(x, y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, -visualAngle);

            float depthScale = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(distanceAngle / 90f));
            transform.localScale = Vector3.one * _data.scale * depthScale;

            _sr.sortingOrder = 5 - Mathf.FloorToInt(distanceAngle / 10f);

            if (_isStunned)
            {
                float blink = Mathf.Sin(Time.time * 12f) > 0f ? 1f : 0.4f;
                Color c = _data.tint;
                c.a = blink;
                _sr.color = c;
            }
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
            sr.sortingLayerName = _sr.sortingLayerName;
            sr.sortingOrder     = _sr.sortingOrder + 2;
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
            if (_sr == null) yield break;
            Color orig = _sr.color;
            for (int i = 0; i < 2; i++)
            {
                _sr.color = Color.white;
                yield return new WaitForSeconds(0.06f);
                _sr.color = orig;
                yield return new WaitForSeconds(0.04f);
            }
        }

        IEnumerator ReviveFlash()
        {
            if (_sr == null) yield break;
            Color c = _data.tint;
            for (int i = 0; i < 4; i++)
            {
                c.a = 0.2f;
                _sr.color = c;
                yield return new WaitForSeconds(0.12f);
                c.a = 1f;
                _sr.color = c;
                yield return new WaitForSeconds(0.12f);
            }
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
