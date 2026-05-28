using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// 現在の画面をキャプチャして RawImage に表示し、UI shader でぼかす。
    /// ポーズ画面の背面に置くことで、既存背景を直接差し替えずに blur 表現を作る。
    /// </summary>
    public class PauseBlurOverlay : MonoBehaviour
    {
        static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        [Header("References")]
        [Tooltip("フルスクリーン表示する RawImage。未設定なら自動生成する。")]
        public RawImage targetImage;

        [Tooltip("targetImage に付ける blur マテリアル。未設定なら BlockBlast/UI/PauseBlur から自動生成する。")]
        public Material blurMaterial;

        [Header("Blur")]
        [Tooltip("ポーズ背景が最終的に到達する blur 量。")]
        [Range(0f, 8f)]
        public float targetBlurSize = 4f;

        [Tooltip("背景がふわっとぼやけるまでの秒数。Time.timeScale=0 中でも進む。")]
        [Min(0f)]
        public float blurInDuration = 0.25f;

        [Tooltip("blur の補間カーブ。横軸 0→1 / 縦軸 0→1。")]
        public AnimationCurve blurInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("blur 画像に重ねる色。少し暗くしたい場合は RGB を下げる。")]
        public Color tintColor = Color.white;

        [Header("Runtime (read only)")]
        [SerializeField] bool _visible;

        Texture2D _capturedTexture;
        Material _runtimeMaterial;

        public bool IsVisible => _visible;
        public float BlurInDuration => blurInDuration;

        void Awake()
        {
            EnsureReady();
            HideImmediate();
        }

        void OnDisable()
        {
            HideImmediate();
        }

        public IEnumerator CaptureAndBlurIn()
        {
            EnsureReady();
            if (targetImage == null)
                yield break;

            // Overlay 自身を写り込ませないため、一度確実に非表示にしてからフレーム終端で撮る。
            targetImage.gameObject.SetActive(false);
            yield return new WaitForEndOfFrame();

            ReplaceCapturedTexture(ScreenCapture.CaptureScreenshotAsTexture());

            targetImage.texture = _capturedTexture;
            targetImage.color = Color.white;
            targetImage.gameObject.SetActive(true);
            _visible = true;

            SetBlur(0f);
            SetTint(tintColor);

            if (blurInDuration <= 0f)
            {
                SetBlur(targetBlurSize);
                yield break;
            }

            float timer = 0f;
            while (timer < blurInDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / blurInDuration);
                float curved = blurInCurve != null ? Mathf.Clamp01(blurInCurve.Evaluate(t)) : t;
                SetBlur(Mathf.Lerp(0f, targetBlurSize, curved));
                yield return null;
            }

            SetBlur(targetBlurSize);
        }

        public void HideImmediate()
        {
            _visible = false;

            if (targetImage != null)
            {
                targetImage.texture = null;
                targetImage.gameObject.SetActive(false);
            }

            ReplaceCapturedTexture(null);
            SetBlur(0f);
        }

        void EnsureReady()
        {
            if (targetImage == null)
                targetImage = CreateFullscreenRawImage();

            if (_runtimeMaterial == null)
            {
                if (blurMaterial != null)
                {
                    _runtimeMaterial = new Material(blurMaterial);
                }
                else
                {
                    Shader shader = Shader.Find("BlockBlast/UI/PauseBlur");
                    if (shader != null)
                        _runtimeMaterial = new Material(shader);
                }
            }

            if (targetImage != null && _runtimeMaterial != null)
            {
                targetImage.material = _runtimeMaterial;
                targetImage.raycastTarget = false;
            }
        }

        RawImage CreateFullscreenRawImage()
        {
            var obj = new GameObject("PauseBlurOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            obj.transform.SetParent(transform, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            return obj.GetComponent<RawImage>();
        }

        void ReplaceCapturedTexture(Texture2D next)
        {
            if (_capturedTexture != null)
                Destroy(_capturedTexture);

            _capturedTexture = next;
        }

        void SetBlur(float value)
        {
            if (_runtimeMaterial != null)
                _runtimeMaterial.SetFloat(BlurSizeId, Mathf.Max(0f, value));
        }

        void SetTint(Color color)
        {
            if (_runtimeMaterial != null)
                _runtimeMaterial.SetColor(TintColorId, color);
        }
    }
}
