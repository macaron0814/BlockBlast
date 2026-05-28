using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BlockBlastGame
{
    /// <summary>
    /// 複数の Animator State を順番に再生する汎用コンポーネント。
    /// 各アニメ終了時に normalizedTime=1 の最終フレームをサンプルしてから次へ進める。
    /// 同じ Animator で次の Step を再生する場合は途中で speed=0 にせず、最後の Step だけ停止する。
    /// </summary>
    public class SequentialAnimatorPlayer : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            [Tooltip("再生対象 Animator。空ならこの GameObject の Animator を使う。")]
            public Animator animator;

            [Tooltip("Animator.Play で再生する State 名。例: Base Layer.Open")]
            public string stateName;

            [Tooltip("確実に前の結果を残したい場合に使う AnimationClip。\n" +
                     "設定すると Animator State ではなく、この Clip を直接 SampleAnimation で再生する。\n" +
                     "完了済み Clip は毎フレーム最終フレームを再サンプルするので、次の Clip に入っても見た目が戻りにくい。")]
            public AnimationClip clip;

            [Tooltip("Animator Layer。通常は 0。")]
            public int layer = 0;

            [Tooltip("再生開始位置。0 = 先頭。")]
            [Range(0f, 1f)]
            public float startNormalizedTime = 0f;

            [Tooltip("ON: このアニメの終了を待ってから次の Step へ進む。")]
            public bool waitForFinish = true;

            [Tooltip("ON: 終了後、この Animator を最終フレームで停止させる。\n" +
                     "同じ Animator で次 Step がある場合は、次の再生を止めないよう最終フレームのサンプルだけ行う。")]
            public bool holdAtEnd = true;

            [Tooltip("waitForFinish の安全タイマー。0 以下なら SequentialAnimatorPlayer 側の既定値を使う。")]
            public float maxWaitSeconds = 0f;
        }

        [Header("Sequence")]
        public List<Step> steps = new List<Step>();

        [Tooltip("ON: OnEnable で自動再生する。")]
        public bool playOnEnable = false;

        [Tooltip("ON: Time.timeScale=0 中でも進める。UI / ポーズ中アニメ向け。")]
        public bool useUnscaledTime = true;

        [Tooltip("Animator の updateMode を UnscaledTime に一時切り替える。useUnscaledTime が ON のときだけ有効。")]
        public bool forceAnimatorUnscaledTime = true;

        [Tooltip("各 Step の maxWaitSeconds が 0 以下の場合に使う安全タイマー。")]
        public float defaultMaxWaitSeconds = 5f;

        [Tooltip("ON: Step.clip が設定されている場合、Animator State ではなく AnimationClip.SampleAnimation で再生する。\n" +
                 "1つの Animator 内で GameOver → Score → ResultButton のように積み上げ表示したい場合はこちらを使う。")]
        public bool preferClipSampling = true;

        [Header("Events")]
        public UnityEvent onSequenceStarted;
        public UnityEvent onSequenceCompleted;

        [Header("Runtime (read only)")]
        [SerializeField] bool _isPlaying;
        [SerializeField] int _currentStepIndex = -1;

        readonly Dictionary<Animator, AnimatorUpdateMode> _savedUpdateModes = new Dictionary<Animator, AnimatorUpdateMode>();
        Coroutine _routine;

        public bool IsPlaying => _isPlaying;
        public int CurrentStepIndex => _currentStepIndex;

        void OnEnable()
        {
            if (playOnEnable)
                PlaySequence();
        }

        void OnDisable()
        {
            StopSequence();
            RestoreAnimatorUpdateModes();
        }

        [ContextMenu("Play Sequence")]
        public void PlaySequence()
        {
            StopSequence();
            _routine = StartCoroutine(PlaySequenceCo());
        }

        [ContextMenu("Stop Sequence")]
        public void StopSequence()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _isPlaying = false;
            _currentStepIndex = -1;
        }

        IEnumerator PlaySequenceCo()
        {
            _isPlaying = true;
            onSequenceStarted?.Invoke();

            if (preferClipSampling && HasAnyClipStep())
            {
                yield return PlayClipSampleSequenceCo();
                yield break;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                if (step == null || string.IsNullOrEmpty(step.stateName))
                    continue;

                Animator animator = ResolveAnimator(step);
                if (animator == null)
                    continue;

                _currentStepIndex = i;
                PrepareAnimator(animator);

                animator.speed = 1f;
                animator.Play(step.stateName, step.layer, step.startNormalizedTime);
                animator.Update(0f);

                if (step.waitForFinish)
                {
                    float maxWait = step.maxWaitSeconds > 0f ? step.maxWaitSeconds : defaultMaxWaitSeconds;
                    yield return WaitForStateFinish(animator, step.stateName, step.layer, maxWait);
                }

                if (step.holdAtEnd)
                {
                    bool sameAnimatorContinues = HasLaterStepUsingSameAnimator(i, animator);
                    HoldAnimatorAtEnd(animator, step.stateName, step.layer, freezeAnimator: !sameAnimatorContinues);
                }
            }

            _isPlaying = false;
            _currentStepIndex = -1;
            _routine = null;
            onSequenceCompleted?.Invoke();
        }

        IEnumerator PlayClipSampleSequenceCo()
        {
            var completed = new List<Step>();

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                if (step == null || step.clip == null)
                    continue;

                Animator animator = ResolveAnimator(step);
                if (animator != null)
                {
                    // Animator が同じ階層を毎フレーム書き戻すと SampleAnimation の結果が戻るため、
                    // クリップ直接サンプル時は Animator を止めて AnimationClip 側だけで制御する。
                    animator.enabled = false;
                }

                _currentStepIndex = i;

                float duration = Mathf.Max(0.0001f, step.clip.length);
                float timer = Mathf.Clamp01(step.startNormalizedTime) * duration;

                while (timer < duration)
                {
                    SampleCompletedSteps(completed);
                    step.clip.SampleAnimation(gameObject, timer);

                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    timer += dt;
                    yield return null;
                }

                step.clip.SampleAnimation(gameObject, duration);

                if (step.holdAtEnd)
                    completed.Add(step);
            }

            SampleCompletedSteps(completed);

            _isPlaying = false;
            _currentStepIndex = -1;
            _routine = null;
            onSequenceCompleted?.Invoke();
        }

        void SampleCompletedSteps(List<Step> completed)
        {
            if (completed == null) return;

            for (int i = 0; i < completed.Count; i++)
            {
                Step step = completed[i];
                if (step == null || step.clip == null) continue;
                step.clip.SampleAnimation(gameObject, step.clip.length);
            }
        }

        bool HasAnyClipStep()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].clip != null)
                    return true;
            }
            return false;
        }

        Animator ResolveAnimator(Step step)
        {
            if (step.animator != null)
                return step.animator;

            return GetComponent<Animator>();
        }

        void PrepareAnimator(Animator animator)
        {
            if (animator == null) return;
            if (!animator.enabled) animator.enabled = true;

            if (useUnscaledTime && forceAnimatorUnscaledTime)
            {
                if (!_savedUpdateModes.ContainsKey(animator))
                    _savedUpdateModes.Add(animator, animator.updateMode);
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        IEnumerator WaitForStateFinish(Animator animator, string stateName, int layer, float maxWait)
        {
            int hash = Animator.StringToHash(stateName);
            float started = Time.realtimeSinceStartup;

            yield return null;

            while (animator != null && animator.isActiveAndEnabled)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
                bool isTarget = info.shortNameHash == hash || info.fullPathHash == hash;
                if (isTarget && !animator.IsInTransition(layer) && info.normalizedTime >= 1f)
                    yield break;

                if (maxWait > 0f && Time.realtimeSinceStartup - started >= maxWait)
                {
                    Debug.LogWarning($"[SequentialAnimatorPlayer] '{stateName}' の完了待ちが {maxWait:F2}s を超えたので次へ進みます。");
                    yield break;
                }

                yield return null;
            }
        }

        bool HasLaterStepUsingSameAnimator(int currentIndex, Animator animator)
        {
            if (animator == null) return false;

            for (int i = currentIndex + 1; i < steps.Count; i++)
            {
                Step later = steps[i];
                if (later == null || string.IsNullOrEmpty(later.stateName))
                    continue;

                if (ResolveAnimator(later) == animator)
                    return true;
            }

            return false;
        }

        void HoldAnimatorAtEnd(Animator animator, string stateName, int layer, bool freezeAnimator)
        {
            if (animator == null) return;

            animator.Play(stateName, layer, 1f);
            animator.Update(0f);
            if (freezeAnimator)
                animator.speed = 0f;
        }

        void RestoreAnimatorUpdateModes()
        {
            foreach (var kvp in _savedUpdateModes)
            {
                if (kvp.Key != null)
                    kvp.Key.updateMode = kvp.Value;
            }
            _savedUpdateModes.Clear();
        }
    }
}
