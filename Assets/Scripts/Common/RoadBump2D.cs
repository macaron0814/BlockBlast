using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// プレイヤーのスクーターについている「道路のガタガタ揺れ」と同じ Y 軸バンプ動作を、
    /// 単独で再利用できるようにした汎用コンポーネント。
    /// 元は <see cref="CharacterAnimator"/> の RoadBumpLoop 実装。
    ///
    /// ■ 動作 (1 サイクル)
    ///   1. 上に bumpAmplitude だけ持ち上げ (bumpEaseDuration 秒で補間)
    ///   2. bumpInterval/2 秒待つ
    ///   3. 下に bumpAmplitude * downRatio だけ落とす (bumpEaseDuration 秒で補間)
    ///   4. bumpInterval/2 秒待つ
    ///   5. 基準位置に戻す (bumpEaseDuration 秒で補間)
    ///   6. bumpInterval 秒待ってループへ
    ///
    /// ■ 対応モード
    ///   ・ワールド空間  … Transform.localPosition.y を揺らす (デフォルト)
    ///   ・UI           … RectTransform.anchoredPosition.y を揺らす (useUIRectTransform = true)
    ///
    /// ■ 推奨セットアップ (敵で使うとき)
    ///   敵本体 GameObject (EnemyController が transform.position を毎フレーム更新する)
    ///     └ Visual (← この子オブジェクトに RoadBump2D をアタッチ)
    ///         └ SpriteRenderer / Spinner2D / SpriteFrameAnimator2D ...
    ///   親の動きを EnemyController が制御し、子の localPosition だけが揺れるので競合しない。
    ///
    /// ■ 推奨セットアップ (プレイヤー UI で使うとき)
    ///   既存の CharacterAnimator の enableRoadBump を OFF にしてから、
    ///   プレイヤーのキャラ画像 (RectTransform) にこのスクリプトを追加し
    ///   useUIRectTransform = true にする。CharacterAnimator と同じ揺れになる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BlockBlast/Road Bump 2D (Y-axis Shake)")]
    public class RoadBump2D : MonoBehaviour
    {
        [Header("揺れ設定")]
        [Tooltip("Y 方向の振れ幅 (UI なら px / ワールドなら world unit)。\n" +
                 "CharacterAnimator デフォルトの UI 用が 5、ワールド用は 0.05〜0.2 程度が目安")]
        public float bumpAmplitude = 5f;

        [Tooltip("1 サイクルの間隔 (秒)。小さいほど速い")]
        public float bumpInterval = 0.18f;

        [Tooltip("揺れ上昇・下降の補間時間 (秒)")]
        public float bumpEaseDuration = 0.07f;

        [Tooltip("下向きの振れ幅は上向きの何倍か (CharacterAnimator と同じ既定値 0.4)")]
        [Range(0f, 2f)]
        public float downRatio = 0.4f;

        [Header("Mode")]
        [Tooltip("ON: RectTransform.anchoredPosition.y を揺らす (UI 用)\nOFF: Transform.localPosition.y を揺らす (ワールド 2D 用)")]
        public bool useUIRectTransform = false;

        [Header("時間軸")]
        [Tooltip("ON: Time.timeScale を無視 (Time.unscaledDeltaTime) / OFF: ゲームの TimeScale に従う")]
        public bool useUnscaledTime = false;

        [Header("初期位相")]
        [Tooltip("0〜1 で 1 サイクル中の開始位置を指定。複数オブジェクトで揃いすぎ防止用。")]
        [Range(0f, 1f)]
        public float startPhase = 0f;

        [Tooltip("ON: OnEnable 時に startPhase をランダム化")]
        public bool randomizeStartPhase = false;

        // ────────────────────────────────────────
        //  Runtime
        // ────────────────────────────────────────

        RectTransform _rect;
        Transform _t;
        Vector2 _basePosUI;
        Vector3 _basePosWorld;
        Coroutine _co;

        void Awake()
        {
            _t = transform;
            _rect = transform as RectTransform;
        }

        void OnEnable()
        {
            CaptureBase();
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(BumpLoop());
        }

        void OnDisable()
        {
            if (_co != null)
            {
                StopCoroutine(_co);
                _co = null;
            }
            RestoreBase();
        }

        // ────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────

        /// <summary>現在位置を新たな基準位置として再記録。動的に位置を変えたあとに呼ぶ。</summary>
        public void SetBaseToCurrent() => CaptureBase();

        /// <summary>強制的に基準位置に戻す (一時停止用)。</summary>
        public void SnapToBase() => RestoreBase();

        // ────────────────────────────────────────
        //  Internal
        // ────────────────────────────────────────

        void CaptureBase()
        {
            if (useUIRectTransform && _rect != null)
                _basePosUI = _rect.anchoredPosition;
            if (_t != null)
                _basePosWorld = _t.localPosition;
        }

        void RestoreBase()
        {
            if (useUIRectTransform && _rect != null)
            {
                _rect.anchoredPosition = _basePosUI;
            }
            else if (_t != null)
            {
                var p = _t.localPosition;
                p.y = _basePosWorld.y;
                _t.localPosition = p;
            }
        }

        IEnumerator BumpLoop()
        {
            // 初期位相で 1 サイクル相当の遅延を入れる
            float phase = randomizeStartPhase ? Random.value : Mathf.Clamp01(startPhase);
            float oneCycle = bumpEaseDuration * 3f + bumpInterval * 2f;
            float initialDelay = phase * oneCycle;
            if (initialDelay > 0f) yield return WaitFor(initialDelay);

            while (true)
            {
                yield return MoveYRelative(+bumpAmplitude,                bumpEaseDuration);
                yield return WaitFor(bumpInterval * 0.5f);

                yield return MoveYRelative(-bumpAmplitude * downRatio,    bumpEaseDuration);
                yield return WaitFor(bumpInterval * 0.5f);

                yield return MoveYRelative(0f,                            bumpEaseDuration);
                yield return WaitFor(bumpInterval);
            }
        }

        IEnumerator MoveYRelative(float deltaFromBase, float duration)
        {
            float startY  = GetCurrentY();
            float targetY = GetBaseY() + deltaFromBase;

            if (duration <= 0f)
            {
                SetY(targetY);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetY(Mathf.Lerp(startY, targetY, t));
                yield return null;
            }
            SetY(targetY);
        }

        IEnumerator WaitFor(float seconds)
        {
            if (!useUnscaledTime)
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float GetDeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        float GetCurrentY()
        {
            return useUIRectTransform && _rect != null
                ? _rect.anchoredPosition.y
                : (_t != null ? _t.localPosition.y : 0f);
        }

        float GetBaseY()
        {
            return useUIRectTransform && _rect != null
                ? _basePosUI.y
                : _basePosWorld.y;
        }

        void SetY(float y)
        {
            if (useUIRectTransform && _rect != null)
            {
                var p = _rect.anchoredPosition;
                p.y = y;
                _rect.anchoredPosition = p;
            }
            else if (_t != null)
            {
                var p = _t.localPosition;
                p.y = y;
                _t.localPosition = p;
            }
        }
    }
}
