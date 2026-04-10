using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    // ─────────────────────────────────────────────────
    //  State 定義
    // ─────────────────────────────────────────────────
    public enum CharaState
    {
        Idle,     // 通常（瞬きモード）
        Alert,    // ライン消去直前の予備動作
        Attack,   // ライン消去（攻撃）
        Hit,      // ダメージ・やられ
        Custom1,
        Custom2,
        Custom3,
    }

    [Serializable]
    public class CharaAnimState
    {
        [Tooltip("このステートに対応する状態")]
        public CharaState state = CharaState.Idle;

        [Tooltip("コマ送りするスプライト配列（順番通りに再生）")]
        public Sprite[] frames;

        [Tooltip("1コマあたりの秒数（Blink Mode OFF 時に使用）")]
        public float frameRate = 0.1f;

        [Tooltip("ループするか（false = 1回再生後に NextState へ戻る）")]
        public bool loop = true;

        [Tooltip("非ループ再生が終わった後に自動遷移するステート")]
        public CharaState nextState = CharaState.Idle;

        [Tooltip("全コマ再生後の追加待機時間（秒）")]
        public float holdDuration = 0f;

        [Header("瞬きモード（Idle 用）")]
        [Tooltip("frames[0]=通常顔, frames[1]=瞬き顔 で自然な瞬きを行う")]
        public bool useBlinkMode = false;

        [Tooltip("瞬き間隔の最小秒数")]
        public float blinkIntervalMin = 2.0f;

        [Tooltip("瞬き間隔の最大秒数")]
        public float blinkIntervalMax = 4.5f;

        [Tooltip("瞬き顔を表示する秒数")]
        public float blinkDuration = 0.08f;
    }

    // ─────────────────────────────────────────────────
    //  CharacterAnimator
    // ─────────────────────────────────────────────────
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("アニメーションステート一覧")]
        public List<CharaAnimState> states = new List<CharaAnimState>();

        [Header("初期ステート")]
        public CharaState defaultState = CharaState.Idle;

        [Header("自動トリガー")]
        [Tooltip("ライン消去時に Alert → Attack → Idle の順で再生する")]
        public bool autoOnLineClear = true;

        [Header("Image（UI）")]
        [Tooltip("空の場合は同 GameObject の Image を自動取得")]
        public Image targetImage;

        // ── スクーター揺れ ──
        [Header("スクーター揺れ（Y軸ガタガタ）")]
        [Tooltip("揺れを有効にする")]
        public bool enableRoadBump = true;

        [Tooltip("Y 方向の振れ幅（px）")]
        public float bumpAmplitude = 5f;

        [Tooltip("1回の揺れにかかる秒数（小さいほど速い）")]
        public float bumpInterval = 0.18f;

        [Tooltip("揺れ上昇・下降の補間時間（秒）")]
        public float bumpEaseDuration = 0.07f;

        // ── Alert パルス ──
        [Header("Alert パルス（拡大縮小）")]
        [Tooltip("Alert 時にスケールをパルスさせる")]
        public bool enableAlertPulse = true;

        [Tooltip("パルス最大スケール倍率（例: 1.08 = 8% 拡大）")]
        public float alertPulseScale = 1.08f;

        [Tooltip("1回のパルスにかかる秒数")]
        public float alertPulseDuration = 0.25f;

        // ──── runtime ────
        CharaState _current;
        Coroutine  _animCoroutine;
        Coroutine  _bumpCoroutine;
        Coroutine  _pulseCoroutine;

        RectTransform _rect;
        Vector2 _basePosition;
        Vector3 _baseScale;

        readonly Dictionary<CharaState, CharaAnimState> _stateMap
            = new Dictionary<CharaState, CharaAnimState>();

        // ─────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────
        void Awake()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();

            _rect = GetComponent<RectTransform>();
            if (_rect != null)
            {
                _basePosition = _rect.anchoredPosition;
                _baseScale    = _rect.localScale;
            }

            foreach (var s in states)
                _stateMap[s.state] = s;
        }

        void OnEnable()
        {
            GameEvents.OnLineClear += HandleLineClear;
            GameEvents.OnGameOver  += HandleGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnLineClear -= HandleLineClear;
            GameEvents.OnGameOver  -= HandleGameOver;
        }

        void Start()
        {
            Play(defaultState);

            if (enableRoadBump && _rect != null)
                _bumpCoroutine = StartCoroutine(RoadBumpLoop());
        }

        // ─────────────────────────────────────────────────
        //  GameEvents
        // ─────────────────────────────────────────────────
        void HandleLineClear(int lines, int combo)
        {
            if (!autoOnLineClear) return;

            if (_stateMap.ContainsKey(CharaState.Alert))
                Play(CharaState.Alert);
            else
                Play(CharaState.Attack);
        }

        void HandleGameOver(GameOverType type) => Play(CharaState.Hit);

        // ─────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────
        public void Play(CharaState state)
        {
            if (!_stateMap.ContainsKey(state)) return;

            _current = state;
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);

            var animState = _stateMap[state];
            _animCoroutine = animState.useBlinkMode
                ? StartCoroutine(RunBlinkAnimation(animState))
                : StartCoroutine(RunAnimation(animState));

            // Alert 時だけパルスを起動
            if (state == CharaState.Alert && enableAlertPulse && _rect != null)
            {
                if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = StartCoroutine(AlertPulseLoop());
            }
            else
            {
                // Alert 以外に遷移したらパルスを止めてスケールを戻す
                if (_pulseCoroutine != null)
                {
                    StopCoroutine(_pulseCoroutine);
                    _pulseCoroutine = null;
                }
                if (_rect != null) _rect.localScale = _baseScale;
            }
        }

        public void Play(string stateName)
        {
            if (Enum.TryParse(stateName, out CharaState s)) Play(s);
        }

        public void TriggerHit()    => Play(CharaState.Hit);
        public void TriggerAttack() => Play(CharaState.Attack);
        public void TriggerCustom(int idx)
        {
            Play(idx switch
            {
                2 => CharaState.Custom2,
                3 => CharaState.Custom3,
                _ => CharaState.Custom1,
            });
        }

        // ─────────────────────────────────────────────────
        //  コルーチン — 通常コマ送り
        // ─────────────────────────────────────────────────
        IEnumerator RunAnimation(CharaAnimState animState)
        {
            if (animState.frames == null || animState.frames.Length == 0)
                yield break;

            do
            {
                foreach (var frame in animState.frames)
                {
                    SetSprite(frame);
                    yield return new WaitForSeconds(animState.frameRate);
                }

                if (animState.holdDuration > 0f)
                    yield return new WaitForSeconds(animState.holdDuration);

            } while (animState.loop);

            Play(animState.nextState);
        }

        // ─────────────────────────────────────────────────
        //  コルーチン — 瞬きモード
        //  frames[0] = 通常顔, frames[1] = 瞬き顔
        // ─────────────────────────────────────────────────
        IEnumerator RunBlinkAnimation(CharaAnimState animState)
        {
            if (animState.frames == null || animState.frames.Length < 2)
            {
                yield return StartCoroutine(RunAnimation(animState));
                yield break;
            }

            SetSprite(animState.frames[0]);

            while (true)
            {
                float interval = UnityEngine.Random.Range(
                    animState.blinkIntervalMin,
                    animState.blinkIntervalMax);
                yield return new WaitForSeconds(interval);

                SetSprite(animState.frames[1]);
                yield return new WaitForSeconds(animState.blinkDuration);

                SetSprite(animState.frames[0]);
            }
        }

        // ─────────────────────────────────────────────────
        //  コルーチン — スクーター道路揺れ
        //  上 → 下 → 上 を一定周期でループ
        // ─────────────────────────────────────────────────
        IEnumerator RoadBumpLoop()
        {
            while (true)
            {
                // 上に移動
                yield return StartCoroutine(MoveY(_basePosition.y + bumpAmplitude, bumpEaseDuration));
                yield return new WaitForSeconds(bumpInterval * 0.5f);

                // 下に戻す
                yield return StartCoroutine(MoveY(_basePosition.y - bumpAmplitude * 0.4f, bumpEaseDuration));
                yield return new WaitForSeconds(bumpInterval * 0.5f);

                // 基準位置に戻す
                yield return StartCoroutine(MoveY(_basePosition.y, bumpEaseDuration));
                yield return new WaitForSeconds(bumpInterval);
            }
        }

        IEnumerator MoveY(float targetY, float duration)
        {
            float elapsed = 0f;
            float startY  = _rect.anchoredPosition.y;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                var pos = _rect.anchoredPosition;
                pos.y = Mathf.Lerp(startY, targetY, t);
                _rect.anchoredPosition = pos;
                yield return null;
            }

            var final = _rect.anchoredPosition;
            final.y = targetY;
            _rect.anchoredPosition = final;
        }

        // ─────────────────────────────────────────────────
        //  コルーチン — Alert パルス（拡大縮小ループ）
        //  Attack 等で Play が呼ばれると自動停止
        // ─────────────────────────────────────────────────
        IEnumerator AlertPulseLoop()
        {
            while (true)
            {
                // 拡大
                yield return StartCoroutine(ScaleTo(_baseScale * alertPulseScale, alertPulseDuration * 0.5f));
                // 縮小
                yield return StartCoroutine(ScaleTo(_baseScale, alertPulseDuration * 0.5f));
            }
        }

        IEnumerator ScaleTo(Vector3 target, float duration)
        {
            float elapsed = 0f;
            Vector3 start = _rect.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _rect.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }

            _rect.localScale = target;
        }

        // ─────────────────────────────────────────────────
        //  ヘルパー
        // ─────────────────────────────────────────────────
        void SetSprite(Sprite sprite)
        {
            if (targetImage != null && sprite != null)
                targetImage.sprite = sprite;
        }
    }
}
