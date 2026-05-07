using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace BlockBlastGame
{
    /// <summary>
    /// ショップ UI の 1 枚分のカード。タップで自分自身を ShopCardSelector に通知する。
    ///
    /// ■ 仕様
    ///   ・非選択: idleScale (デフォルト 0.6)
    ///   ・選択中: selectedScale (デフォルト 0.7)
    ///   ・スケール変化は scaleAnimDuration 秒で smoothstep 補間
    ///
    /// ■ セットアップ
    ///   1. シーン上の各カード GameObject (Image / RectTransform) にアタッチ
    ///   2. cost (購入金額) を Inspector で設定。0 なら無料 (常に買える)
    ///   3. UI Button があれば targetButton に割り当て、無ければ
    ///      この GameObject 自体に Image (raycastTarget=true) を持たせれば
    ///      IPointerClickHandler 経由でタップを受け取る
    ///   4. ShopCardSelector.cards に 6 枚を登録
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopCard : MonoBehaviour, IPointerClickHandler
    {
        [Header("Identity")]
        [Tooltip("カードの表示名 (デバッグ用)")]
        public string displayName;

        [Tooltip("購入金額 (円)。0 で無料 = 常に買える。")]
        public int cost = 100;

        [Tooltip("カードに紐づくアイテム (任意)。購入時の参照用に使える。")]
        public ItemData itemData;

        [Header("Click Source")]
        [Tooltip("UI Button を使う場合はここに割り当て (省略時は IPointerClickHandler 経由)。")]
        public Button targetButton;

        [Header("Selection Scale")]
        [Tooltip("非選択時のスケール")]
        public float idleScale = 0.6f;

        [Tooltip("選択中のスケール")]
        public float selectedScale = 0.7f;

        [Tooltip("スケール変化にかける秒数 (0 = 即時)")]
        public float scaleAnimDuration = 0.12f;

        [Header("Affordability Visuals (所持金不足時の見た目)")]
        [Tooltip("カードのルート (枠) の HueShiftFilter。彩度 0 でグレースケール化。任意。")]
        public HueShiftFilter cardRootFilter;

        [Tooltip("HueShiftFilter が無いときに代用する Image (色 tint)。任意。")]
        public Image cardRootImage;

        [Tooltip("価格表示の uGUI Text (任意)")]
        public Text priceText;

        [Tooltip("価格表示の TMP_Text (任意)")]
        public TMP_Text priceTextTMP;

        [Tooltip("通常時 (買える) の価格テキスト色")]
        public Color priceColorAffordable = Color.white;

        [Tooltip("買えないとき (赤) の価格テキスト色")]
        public Color priceColorUnaffordable = new Color(1f, 0.25f, 0.25f, 1f);

        [Tooltip("HueShiftFilter 無いとき: 買えない時のルート tint (グレー代替)")]
        public Color rootDisabledTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("HueShiftFilter 無いとき: 買える時のルート tint")]
        public Color rootEnabledTint = Color.white;

        [Header("Runtime (read only)")]
        [SerializeField] bool _selected;
        [SerializeField] bool _affordable = true;

        public bool IsSelected => _selected;
        public bool IsAffordable => _affordable;

        public event System.Action<ShopCard> OnClicked;

        RectTransform _rt;
        float _animTimer;
        float _animFromScale;
        float _animToScale = -1f;

        void Awake()
        {
            _rt = transform as RectTransform;
            ApplyScaleImmediate(idleScale);
        }

        void OnEnable()
        {
            if (targetButton != null)
            {
                targetButton.onClick.RemoveListener(HandleClick);
                targetButton.onClick.AddListener(HandleClick);
            }
            GameEvents.OnTotalAssetsChanged -= HandleTotalAssetsChanged;
            GameEvents.OnTotalAssetsChanged += HandleTotalAssetsChanged;

            ApplyScaleImmediate(_selected ? selectedScale : idleScale);
            RefreshAffordability();
        }

        void OnDisable()
        {
            if (targetButton != null)
                targetButton.onClick.RemoveListener(HandleClick);

            GameEvents.OnTotalAssetsChanged -= HandleTotalAssetsChanged;
        }

        void Update()
        {
            if (scaleAnimDuration <= 0f) return;
            if (_animToScale < 0f)        return;
            if (_animTimer >= scaleAnimDuration) return;

            _animTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_animTimer / scaleAnimDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            ApplyScaleImmediate(Mathf.Lerp(_animFromScale, _animToScale, t));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Button が割り当てられている場合は Button.onClick で処理されるので二重発火を避ける
            if (targetButton != null) return;
            // 所持金不足のカードはタップしてもピックアップさせない
            if (!_affordable) return;
            HandleClick();
        }

        void HandleClick()
        {
            // 所持金不足のカードは Button.onClick からの呼び出しでも反応させない
            if (!_affordable) return;
            OnClicked?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            BeginScaleAnim(selected ? selectedScale : idleScale);
        }

        public void ResetVisualToIdle()
        {
            _selected = false;
            ApplyScaleImmediate(idleScale);
            _animToScale = -1f;
        }

        // ─────────────────────────────────────
        //  Affordability (所持金が足りているか)
        // ─────────────────────────────────────

        void HandleTotalAssetsChanged(int total)
        {
            UpdateAffordabilityFromTotal(total);
        }

        /// <summary>外部から強制的に再評価したいときに呼ぶ。</summary>
        public void RefreshAffordability()
        {
            var w = PlayerWallet.Instance;
            int total = w != null ? w.TotalAssets : int.MaxValue;
            UpdateAffordabilityFromTotal(total);
        }

        void UpdateAffordabilityFromTotal(int totalAssets)
        {
            bool affordable = cost <= 0 || totalAssets >= cost;
            _affordable = affordable;
            ApplyAffordabilityVisuals();
        }

        void ApplyAffordabilityVisuals()
        {
            // ルート (カード枠) のグレースケール
            if (cardRootFilter != null)
            {
                cardRootFilter.saturation = _affordable ? 1f : 0f;
                cardRootFilter.brightness = _affordable ? 1f : 0.85f;
                cardRootFilter.Apply();
            }
            else if (cardRootImage != null)
            {
                cardRootImage.color = _affordable ? rootEnabledTint : rootDisabledTint;
            }

            // 価格テキスト色 (赤 / 通常)
            Color priceColor = _affordable ? priceColorAffordable : priceColorUnaffordable;
            if (priceText != null)    priceText.color    = priceColor;
            if (priceTextTMP != null) priceTextTMP.color = priceColor;
        }

        void BeginScaleAnim(float targetScale)
        {
            float currentScale = _rt != null ? _rt.localScale.x : transform.localScale.x;
            _animFromScale = currentScale;
            _animToScale   = targetScale;
            _animTimer     = 0f;
            if (scaleAnimDuration <= 0f)
                ApplyScaleImmediate(targetScale);
        }

        void ApplyScaleImmediate(float scale)
        {
            var v = new Vector3(scale, scale, 1f);
            if (_rt != null) _rt.localScale = v;
            else             transform.localScale = v;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (idleScale < 0.01f)     idleScale = 0.01f;
            if (selectedScale < 0.01f) selectedScale = 0.01f;
            if (scaleAnimDuration < 0f) scaleAnimDuration = 0f;
        }
#endif
    }
}
