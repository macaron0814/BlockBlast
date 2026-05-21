using TMPro;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// PlayerWallet の現在所持金を TMP_Text に表示するための単体コンポーネント。
    ///
    /// 使い方:
    /// 1. 所持金表示用の TextMeshProUGUI にこのコンポーネントを付ける
    /// 2. targetText が空なら同じ GameObject の TMP_Text を自動取得
    /// 3. format / textColor / outline を Inspector で調整
    ///
    /// PlayerWallet の Add / TrySpend / SetAssets による
    /// GameEvents.OnTotalAssetsChanged を購読して自動更新する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class WalletTMPDisplay : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("所持金を表示する TMP_Text。空なら同じ GameObject から自動取得")]
        public TMP_Text targetText;

        [Header("Text")]
        [Tooltip("表示フォーマット。{0} に現在所持金が入る。例: \"{0}\" / \"所持金 {0}\" / \"¥{0:N0}\"")]
        public string format = "{0}";

        [Tooltip("3桁区切りで表示する")]
        public bool useThousandsSeparator = false;

        [Tooltip("表示文字色")]
        public Color textColor = Color.white;

        [Header("Outline")]
        [Tooltip("アウトラインを使う")]
        public bool useOutline = true;

        [Tooltip("アウトライン色")]
        public Color outlineColor = Color.black;

        [Tooltip("アウトライン幅。0 = なし。TMP では 0.05〜0.15 あたりから調整推奨")]
        [Range(0f, 1f)]
        public float outlineWidth = 0.08f;

        void Awake()
        {
            EnsureTarget();
            ApplyStyle();
        }

        void OnEnable()
        {
            GameEvents.OnTotalAssetsChanged -= HandleTotalAssetsChanged;
            GameEvents.OnTotalAssetsChanged += HandleTotalAssetsChanged;
            Refresh();
        }

        void OnDisable()
        {
            GameEvents.OnTotalAssetsChanged -= HandleTotalAssetsChanged;
        }

        void OnValidate()
        {
            EnsureTarget();
            ApplyStyle();
            Refresh();
        }

        void EnsureTarget()
        {
            if (targetText == null)
                targetText = GetComponent<TMP_Text>();
        }

        void HandleTotalAssetsChanged(int totalAssets)
        {
            SetValue(totalAssets);
        }

        public void Refresh()
        {
            int total = PlayerWallet.Instance != null ? PlayerWallet.Instance.TotalAssets : 0;
            SetValue(total);
        }

        public void SetValue(int totalAssets)
        {
            EnsureTarget();
            if (targetText == null) return;

            string value = useThousandsSeparator ? totalAssets.ToString("N0") : totalAssets.ToString();
            targetText.text = string.Format(format, value);
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            EnsureTarget();
            if (targetText == null) return;

            targetText.color = textColor;

            float width = useOutline ? Mathf.Max(0f, outlineWidth) : 0f;
            var mat = targetText.fontMaterial;
            if (mat != null)
            {
                mat.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, outlineColor);
                mat.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, width);
                targetText.fontMaterial = mat;
            }

            targetText.UpdateMeshPadding();
            targetText.SetMaterialDirty();
            targetText.SetVerticesDirty();
        }
    }
}
