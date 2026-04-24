using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// HueShift シェーダーを使った色相・彩度・輝度フィルター。
    /// SpriteRenderer / Image / RawImage いずれにも対応。
    /// </summary>
    [ExecuteAlways]
    public class HueShiftFilter : MonoBehaviour
    {
        [Header("Color Filter")]
        [Tooltip("乗算カラー (白 = 変化なし)")]
        public Color color = Color.white;

        [Range(0f, 1f)]
        [Tooltip("色相シフト (0〜1 = 0〜360度)")]
        public float hueShift = 0f;

        [Range(0f, 2f)]
        [Tooltip("彩度 (1 = 元のまま, 0 = グレースケール)")]
        public float saturation = 1f;

        [Range(0f, 2f)]
        [Tooltip("輝度 (1 = 元のまま)")]
        public float brightness = 1f;

        [Range(0f, 1f)]
        [Tooltip("透明度")]
        public float alpha = 1f;

        [Header("Animation")]
        [Tooltip("自動で色相をアニメーションさせる")]
        public bool animateHue = false;

        [Tooltip("色相アニメーション速度 (1周/秒)")]
        public float hueAnimSpeed = 0.1f;

        static readonly int PropColor      = Shader.PropertyToID("_Color");
        static readonly int PropHue        = Shader.PropertyToID("_HueShift");
        static readonly int PropSaturation = Shader.PropertyToID("_Saturation");
        static readonly int PropBrightness = Shader.PropertyToID("_Brightness");
        static readonly int PropAlpha      = Shader.PropertyToID("_Alpha");

        Material _material;
        SpriteRenderer _spriteRenderer;
        Graphic _graphic;

        void Awake() => SetupMaterial();

        void OnEnable()
        {
            SetupMaterial();
            Apply();
        }

        void Update()
        {
            if (animateHue)
            {
                hueShift = Mathf.Repeat(hueShift + hueAnimSpeed * Time.deltaTime, 1f);
                Apply();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            SetupMaterial();
            Apply();
        }
#endif

        void SetupMaterial()
        {
            if (_material != null) return;

            Shader shader = Shader.Find("BlockBlast/HueShift");
            if (shader == null)
            {
                Debug.LogWarning("[HueShiftFilter] シェーダー 'BlockBlast/HueShift' が見つかりません。");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _graphic = GetComponent<Graphic>();

            if (_spriteRenderer != null)
                _spriteRenderer.material = _material;
            else if (_graphic != null)
                _graphic.material = _material;
        }

        public void Apply()
        {
            if (_material == null) SetupMaterial();
            if (_material == null) return;

            _material.SetColor(PropColor,      color);
            _material.SetFloat(PropHue,        hueShift);
            _material.SetFloat(PropSaturation, saturation);
            _material.SetFloat(PropBrightness, brightness);
            _material.SetFloat(PropAlpha,      alpha);
        }

        /// <summary>マテリアルを外部から直接取得する</summary>
        public Material GetMaterial() => _material;

        void OnDestroy()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }
    }
}
