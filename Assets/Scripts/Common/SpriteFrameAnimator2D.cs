using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// 配置した <see cref="Sprite"/> 配列を一定間隔で順番に切り替えてループ再生するだけの汎用コマアニメーター。
    ///
    /// ■ 主な用途
    ///   ・パラパラアニメ的な 2D スプライト演出 (キャラの待機モーション、UI のループ装飾、エフェクト等)
    ///   ・Unity の Animator を組むほどでも無い軽量アニメ
    ///
    /// ■ 対応コンポーネント
    ///   ・SpriteRenderer (2D スプライト)
    ///   ・UnityEngine.UI.Image (Canvas 上の UI)
    ///   どちらか片方を Inspector に割り当てれば OK。空のときは同じ GameObject から自動取得します。
    ///
    /// ■ 仕様
    ///   ・frames が 1 枚以下なら何もしない
    ///   ・FPS / 秒あたりコマ数で速度指定 (frameRate)
    ///   ・playOnEnable / loop / startFrame / randomStartFrame で再生制御
    ///   ・useUnscaledTime で TimeScale=0 中も再生可能
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BlockBlast/Sprite Frame Animator 2D")]
    public class SpriteFrameAnimator2D : MonoBehaviour
    {
        [Header("Frames")]
        [Tooltip("再生するコマ画像 (順番どおりに並べる)")]
        public List<Sprite> frames = new List<Sprite>();

        [Header("Speed")]
        [Tooltip("1 秒あたりに進めるコマ数 (FPS)。\n例) 12 = 1秒で12コマ進む / 4 = 0.25秒に1コマ")]
        [Min(0f)]
        public float frameRate = 8f;

        [Tooltip("再生速度の倍率 (外部から動的に変更可)。0 = 一時停止、負 = 逆再生")]
        public float speedMultiplier = 1f;

        [Header("Playback")]
        [Tooltip("OnEnable で自動的に再生開始する")]
        public bool playOnEnable = true;

        [Tooltip("最後まで行ったら最初に戻る (false なら最後のコマで停止)")]
        public bool loop = true;

        [Tooltip("逆再生方向で進める (時間軸を反転)")]
        public bool reverse = false;

        [Header("Start Frame")]
        [Tooltip("再生開始時のコマ番号 (0 始まり)")]
        public int startFrame = 0;

        [Tooltip("ON: 再生開始時に startFrame ではなくランダムなコマから始める。\n複数オブジェクトを同じプレハブで並べたとき揃いすぎないように。")]
        public bool randomStartFrame = false;

        [Header("Time")]
        [Tooltip("ON: Time.timeScale を無視して進める (ポーズ中も再生)")]
        public bool useUnscaledTime = false;

        [Header("Renderer (空なら自動取得)")]
        [Tooltip("SpriteRenderer に描画する場合に割り当て")]
        public SpriteRenderer spriteRenderer;

        [Tooltip("UI.Image に描画する場合に割り当て")]
        public Image uiImage;

        // ────────────────────────────────────────
        //  Runtime
        // ────────────────────────────────────────

        bool _isPlaying;
        int _currentFrame;
        float _accumulated;

        public bool IsPlaying => _isPlaying;
        public int  CurrentFrame => _currentFrame;
        public int  FrameCount => frames != null ? frames.Count : 0;

        // ────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────

        public void Play()
        {
            if (frames == null || frames.Count == 0) return;
            _isPlaying  = true;
            _accumulated = 0f;
            ApplyCurrentFrame();
        }

        public void Pause() => _isPlaying = false;

        public void Resume() => _isPlaying = frames != null && frames.Count > 0;

        public void Stop()
        {
            _isPlaying = false;
            _accumulated = 0f;
            _currentFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, FrameCount - 1));
            ApplyCurrentFrame();
        }

        /// <summary>指定コマに即ジャンプ (再生状態は維持)。</summary>
        public void SetFrame(int index)
        {
            if (FrameCount == 0) return;
            _currentFrame = ((index % FrameCount) + FrameCount) % FrameCount;
            _accumulated = 0f;
            ApplyCurrentFrame();
        }

        // ────────────────────────────────────────
        //  Lifecycle
        // ────────────────────────────────────────

        void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            uiImage        = GetComponent<Image>();
        }

        void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (uiImage == null)        uiImage        = GetComponent<Image>();
        }

        void OnEnable()
        {
            if (FrameCount == 0) return;

            _currentFrame = randomStartFrame
                ? Random.Range(0, FrameCount)
                : Mathf.Clamp(startFrame, 0, FrameCount - 1);
            _accumulated  = 0f;
            ApplyCurrentFrame();

            if (playOnEnable) _isPlaying = true;
        }

        void Update()
        {
            if (!_isPlaying) return;
            if (FrameCount <= 1) return;
            if (frameRate <= 0f) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float effective = frameRate * speedMultiplier * (reverse ? -1f : 1f);
            _accumulated += effective * dt;

            // 1 コマぶん溜まるまで進める
            while (_accumulated >= 1f)
            {
                _accumulated -= 1f;
                if (!StepFrame(+1)) return;
            }
            while (_accumulated <= -1f)
            {
                _accumulated += 1f;
                if (!StepFrame(-1)) return;
            }
        }

        /// <summary>1 コマ進める。loop=false で終端到達時は停止。継続するなら true。</summary>
        bool StepFrame(int direction)
        {
            int next = _currentFrame + direction;
            int last = FrameCount - 1;

            if (next > last)
            {
                if (loop) next = 0;
                else { _currentFrame = last; ApplyCurrentFrame(); _isPlaying = false; return false; }
            }
            else if (next < 0)
            {
                if (loop) next = last;
                else { _currentFrame = 0; ApplyCurrentFrame(); _isPlaying = false; return false; }
            }

            _currentFrame = next;
            ApplyCurrentFrame();
            return true;
        }

        void ApplyCurrentFrame()
        {
            if (FrameCount == 0) return;
            var sprite = frames[Mathf.Clamp(_currentFrame, 0, FrameCount - 1)];
            if (sprite == null) return;

            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
            if (uiImage != null)        uiImage.sprite        = sprite;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (frameRate < 0f) frameRate = 0f;
            if (FrameCount > 0)
                startFrame = Mathf.Clamp(startFrame, 0, FrameCount - 1);
        }
#endif
    }
}
