using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ライン消去時にプレイヤーから発射される弾。
    /// 道路アーチに沿って飛び、地面でバウンドするポップな動き。
    /// </summary>
    public class PlayerBullet : MonoBehaviour
    {
        float _speed;
        float _bounceHeight;
        float _bounceFrequency;
        float _archRadius;
        Vector3 _archCenter;

        float _currentAngle;
        float _prevAngle;
        float _elapsed;
        bool _alive = true;
        bool _hasEnteredViewport;

        float _hitAngleOffset;
        float _hitAngleRadius;
        float _viewportExitMargin = 0.05f;

        // 発射起点からアーチに乗るまでのブレンド
        Vector3 _launchStartPos;
        bool _hasLaunchBlend;
        const float LaunchBlendDuration = 0.15f;
        const float HitSnapVisibleDuration = 0.04f;

        SpriteRenderer _sr;
        Vector3 _baseScale;

        public float CurrentAngle => _currentAngle;
        public float PrevAngle    => _prevAngle;
        public bool  IsAlive      => _alive;

        /// <summary>ヒット判定中心角度（hitAngleOffset 適用済み）</summary>
        public float HitAngle       => _currentAngle + _hitAngleOffset;
        public float PrevHitAngle   => _prevAngle    + _hitAngleOffset;
        public float HitAngleRadius => _hitAngleRadius;

        /// <param name="overrideStartPos">指定時、この位置からアーチ軌道へ短くブレンドして飛び出す</param>
        /// <param name="sprite">弾のスプライト。null の場合はデフォルト丸を使用</param>
        /// <param name="color">弾の色</param>
        /// <param name="scale">弾のスケール</param>
        public void Initialize(float speed, float bounceHeight, float bounceFrequency,
                               float archRadius, Vector3 archCenter, float startAngle,
                               Vector3? overrideStartPos = null,
                               Sprite sprite = null, Color? color = null, float scale = 0.35f,
                               float hitAngleOffset = 0f, float hitAngleRadius = 2f,
                               float viewportExitMargin = 0.05f)
        {
            _speed = speed;
            _bounceHeight = bounceHeight;
            _bounceFrequency = bounceFrequency;
            _archRadius = archRadius;
            _archCenter = archCenter;
            _currentAngle = startAngle;
            _prevAngle = startAngle;
            _hitAngleOffset = hitAngleOffset;
            _hitAngleRadius = hitAngleRadius;
            _viewportExitMargin = Mathf.Max(0f, viewportExitMargin);

            if (overrideStartPos.HasValue)
            {
                _hasLaunchBlend = true;
                _launchStartPos = overrideStartPos.Value;
                transform.position = _launchStartPos;
            }

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = sprite != null ? sprite : GetOrCreateBulletSprite();
            _sr.sortingLayerName = "UI";
            _sr.sortingOrder = 7;
            _sr.color = color ?? new Color(1f, 0.95f, 0.35f);

            transform.localScale = Vector3.one * scale;
            _baseScale = transform.localScale;

            if (!_hasLaunchBlend)
                UpdateVisualPosition(0f);
        }

        void Update()
        {
            if (!_alive) return;

            _elapsed += Time.deltaTime;
            _prevAngle = _currentAngle;
            _currentAngle += _speed * Time.deltaTime;

            float bounce = CalculateBounce();
            Vector3 arcPos = CalcArcPosition(bounce);

            if (_hasLaunchBlend && _elapsed < LaunchBlendDuration)
            {
                float t = _elapsed / LaunchBlendDuration;
                t = t * t * (3f - 2f * t); // smoothstep
                transform.position = Vector3.Lerp(_launchStartPos, arcPos, t);
            }
            else
            {
                _hasLaunchBlend = false;
                UpdateVisualPosition(bounce);
            }

            if (IsInsideViewport())
            {
                _hasEnteredViewport = true;
            }
            else if (_hasEnteredViewport)
            {
                Kill();
            }
        }

        /// <summary>放物線バウンス: 地面に素早く接触→空中で滞空するポップな動き。</summary>
        float CalculateBounce()
        {
            float t = Mathf.Repeat(_elapsed * _bounceFrequency, 1f);
            return 4f * t * (1f - t) * _bounceHeight;
        }

        Vector3 CalcArcPosition(float bounceOffset)
        {
            float visualAngle = -_currentAngle;
            float effectiveR = _archRadius + bounceOffset;
            float rad = visualAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * effectiveR;
            float y = -_archRadius + Mathf.Cos(rad) * effectiveR;
            return _archCenter + new Vector3(x, y, 0f);
        }

        void UpdateVisualPosition(float bounceOffset)
        {
            transform.position = CalcArcPosition(bounceOffset);
            transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);

            float normalizedBounce = bounceOffset / Mathf.Max(_bounceHeight, 0.01f);
            float groundness = 1f - normalizedBounce;
            transform.localScale = new Vector3(
                _baseScale.x * (1f + groundness * 0.5f),
                _baseScale.y * (1f - groundness * 0.3f),
                1f);

            float depthScale = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(_currentAngle / 100f));
            transform.localScale *= depthScale;

            _sr.sortingOrder = 7 - Mathf.FloorToInt(_currentAngle / 10f);
        }

        public void Kill()
        {
            _alive = false;
            Destroy(gameObject);
        }

        /// <summary>
        /// 指定のワールド座標にスナップしてから破棄する。
        /// ヒット時に呼ぶことで「敵の当たり位置で弾が消える」見た目になる。
        /// </summary>
        public void SnapAndKill(Vector3 worldPos)
        {
            _alive = false;
            transform.position = worldPos;
            _hasLaunchBlend = false;
            StartCoroutine(DestroyAfterHitSnap());
        }

        IEnumerator DestroyAfterHitSnap()
        {
            // 1フレームぶん当たり位置に残してから消すことで、
            // 「手前で消えた」ように見えるのを防ぐ。
            yield return null;
            yield return new WaitForSeconds(HitSnapVisibleDuration);
            if (this != null)
                Destroy(gameObject);
        }

        bool IsInsideViewport()
        {
            if (Camera.main == null)
                return true;

            Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
            if (viewportPos.z < 0f)
                return false;

            return viewportPos.x >= -_viewportExitMargin
                && viewportPos.x <= 1f + _viewportExitMargin
                && viewportPos.y >= -_viewportExitMargin
                && viewportPos.y <= 1f + _viewportExitMargin;
        }

        // ────────────────────────────────────────
        //  Default sprite (circle with glow)
        // ────────────────────────────────────────

        static Sprite _cached;

        static Sprite GetOrCreateBulletSprite()
        {
            if (_cached != null) return _cached;

            const int size = 16;
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
            var px = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.45f;

            for (int py = 0; py < size; py++)
            for (int px2 = 0; px2 < size; px2++)
            {
                float dx = px2 - center, dy = py - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    float bright = 1f - (dist / radius) * 0.4f;
                    px[py * size + px2] = new Color(bright, bright, bright, 1f);
                }
                else
                {
                    px[py * size + px2] = Color.clear;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _cached = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cached;
        }
    }
}
