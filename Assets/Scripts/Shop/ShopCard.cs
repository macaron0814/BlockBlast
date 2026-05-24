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

        [Tooltip("ショップ用アイテム定義 (CSV のアイテム1行分)。Apply() で反映される。")]
        public ShopItemData shopItemData;

        [Header("Item Visual References (Apply で書き換える対象)")]
        [Tooltip("アイテムアイコン (item.icon を流し込む)")]
        public Image iconImage;

        [Tooltip("ON: Apply 時に iconImage.SetNativeSize() を呼んで元画像サイズに合わせる。\n" +
                 "OFF: RectTransform のサイズはそのまま (Layout 等で固定したい場合)")]
        public bool useNativeIconSize = true;

        [Tooltip("レアリティ枠 (色 / スプライトを差し替える)")]
        public Image frameImage;

        [Tooltip("レアリティ背景 (色 / スプライトを差し替える)")]
        public Image backgroundImage;

        [Tooltip("レアリティバッジのアイコン (任意)。Sprite を差し替える")]
        public Image rarityBadgeImage;

        [Tooltip("レアリティバッジのテキスト (任意)。\"N\" / \"SSR\" などを書く")]
        public TMP_Text rarityBadgeText;

        [Tooltip("アイテム名テキスト (任意)")]
        public TMP_Text nameText;

        [Tooltip("説明文テキスト (任意)。{value} はテーブルから差し込まれる")]
        public TMP_Text descriptionText;

        [Tooltip("倍率 / 個数 (\"1.1倍\" \"+3個\" 等) を出す専用 TMP_Text (任意)。\n" +
                 "アサインすると、説明文中の {value} 部分とは別にここに値文字列が書き込まれ、\n" +
                 "ShopRarityVisualTable.valueTextOutline* で個別フチを当てられる。\n" +
                 "アサインしない場合は今まで通り descriptionText 内の {value} を <color> で囲むだけになる。")]
        public TMP_Text valueText;

        [Tooltip("ON: valueText を使う場合、説明文側の {value} は空文字に置換して二重表示を防ぐ。\n" +
                 "OFF: 説明文内にもインラインで値文字列が表示される (色のみ反映)。")]
        public bool valueTextReplacesDescriptionValue = true;

        [Header("Sale Visual (任意)")]
        [Tooltip("SSR セール時に表示する GameObject (\"半額！\" バッジ など)。\n" +
                 "Apply で SetActive(isOnSale) される")]
        public GameObject saleBadge;

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

        [Header("Selection Outline")]
        [Tooltip("選択中だけ表示する画像縁。UIImageOutline をカードの枠 Image などに付けてここへ割り当て")]
        public UIImageOutline selectionOutline;

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

        [Tooltip("旧仕様用。現在は買えない時も価格テキストは priceColorAffordable のまま維持し、カード全体のグレースケールで表現する")]
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
            if (selectionOutline != null)
                selectionOutline.SetVisible(selected);
        }

        public void ResetVisualToIdle()
        {
            _selected = false;
            ApplyScaleImmediate(idleScale);
            _animToScale = -1f;
            if (selectionOutline != null)
                selectionOutline.SetVisible(false);
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

        // ─────────────────────────────────────
        //  アイテム反映 API
        // ─────────────────────────────────────

        /// <summary>
        /// ShopItemData の中身をこのカードに反映する。
        /// 効果値テーブル + レアリティ見た目テーブルがあれば説明文/見た目も合わせて更新。
        /// finalPrice / isOnSale は ShopItemPool.Draw() の結果をそのまま渡せる。
        /// </summary>
        public void Apply(ShopItemData item,
                          ShopItemEffectTable effectTable = null,
                          ShopRarityVisualTable visualTable = null,
                          int? finalPrice = null,
                          bool isOnSale = false)
        {
            shopItemData = item;
            if (item == null)
            {
                if (saleBadge != null) saleBadge.SetActive(false);
                return;
            }

            // 価格
            cost = finalPrice ?? item.price;
            displayName = item.ResolveDisplayName();

            // アイコン
            if (iconImage != null)
            {
                iconImage.sprite  = item.icon;
                iconImage.enabled = (item.icon != null);

                // 元画像サイズに合わせる (useNativeIconSize=ON のとき)
                if (useNativeIconSize && item.icon != null)
                    iconImage.SetNativeSize();
            }

            // レアリティビジュアル (説明文の value 色決定に使うので、説明文より「先」に解決)
            ShopRarityVisualTable.RarityEntry ve = null;
            if (visualTable != null)
            {
                ve = visualTable.Get(item.rarity);
                if (ve != null)
                {
                    if (frameImage != null)
                    {
                        frameImage.color = ve.frameColor;
                        if (ve.frameSprite != null) frameImage.sprite = ve.frameSprite;
                    }
                    if (backgroundImage != null)
                    {
                        backgroundImage.color = ve.backgroundColor;
                        if (ve.backgroundSprite != null) backgroundImage.sprite = ve.backgroundSprite;
                    }
                    if (rarityBadgeImage != null && ve.badgeSprite != null)
                    {
                        rarityBadgeImage.sprite  = ve.badgeSprite;
                        rarityBadgeImage.enabled = true;
                    }
                    if (rarityBadgeText != null)
                    {
                        rarityBadgeText.text  = visualTable.GetLabel(item.rarity);
                        ApplyTmpStyle(rarityBadgeText, ve.textColor, ve.textOutlineColor, ve.textOutlineWidth);
                    }
                    if (nameText != null)
                    {
                        ApplyTmpStyle(nameText, ve.textColor, ve.textOutlineColor, ve.textOutlineWidth);
                    }
                    ApplyTmpStyle(descriptionText, ve.textColor, ve.textOutlineColor, ve.textOutlineWidth);
                    ApplyTmpStyle(priceTextTMP, ve.priceColor, ve.priceTextOutlineColor, ve.priceTextOutlineWidth);
                    // 値専用テキストは value 色 + value 専用フチを適用
                    ApplyTmpStyle(valueText, ve.valueColor, ve.valueTextOutlineColor, ve.valueTextOutlineWidth);

                    // 値段テキストの「買えるときの色」をレアリティ依存に上書き
                    // (買えないときの赤は priceColorUnaffordable 側で維持される)
                    priceColorAffordable = ve.priceColor;
                }
            }
            else if (rarityBadgeText != null)
            {
                rarityBadgeText.text = item.rarity.ToString();
            }

            // 名前
            if (nameText != null) nameText.text = item.ResolveDisplayName();

            // 値文字列 (倍率 / 個数) を解決
            string valueFormatted = null;
            if (effectTable != null)
                valueFormatted = effectTable.GetFormatted(item.category, item.tierIndex);

            // 値専用 TMP (任意): 値文字列をここに別出力。フチ・色は ApplyTmpStyle で既に当てている。
            if (valueText != null)
            {
                valueText.richText = true;
                valueText.text     = valueFormatted ?? "";
            }

            // 説明文 ({value} / {amount} を効果テーブルで差し替え、レアリティ色で <color> 囲み)
            if (descriptionText != null)
            {
                string desc = item.descriptionTemplate ?? "";
                if (valueFormatted != null)
                {
                    // valueText が割り当てられていて二重表示を避けたい場合は説明文側の {value} を空に置換
                    bool stripFromDescription = (valueText != null && valueTextReplacesDescriptionValue);

                    string colored;
                    if (stripFromDescription)
                    {
                        colored = "";
                    }
                    else if (ve != null)
                    {
                        string hex = ColorUtility.ToHtmlStringRGB(ve.valueColor);
                        colored = $"<color=#{hex}>{valueFormatted}</color>";
                    }
                    else
                    {
                        colored = valueFormatted;
                    }

                    desc = desc.Replace("{value}", colored)
                               .Replace("{amount}", colored);
                }
                descriptionText.richText = true; // 念のため明示的に有効化
                descriptionText.text     = desc;
            }

            // 価格テキスト
            string priceStr = cost.ToString();
            if (priceText    != null) priceText.text    = priceStr;
            if (priceTextTMP != null) priceTextTMP.text = priceStr;

            // セールバッジ
            if (saleBadge != null) saleBadge.SetActive(isOnSale);

            // 所持金との照合をやり直す (cost が変わるので)
            RefreshAffordability();
        }

        void ApplyTmpStyle(TMP_Text text, Color textColor, Color outlineColor, float outlineWidth)
        {
            if (text == null) return;

            text.color = textColor;

            // TMP_Text.outlineWidth の setter は、TMP 内部マテリアルが未初期化の状態だと
            // TextMeshProUGUI.SetOutlineThickness 側で NullReference を出すことがある。
            // そのためプロパティ直指定は避け、fontMaterial が取得できた場合だけ
            // shader property を直接更新する。
            float width = Mathf.Max(0f, outlineWidth);

            var mat = text.fontMaterial;
            if (mat != null)
            {
                mat.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, outlineColor);
                mat.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, width);
                text.fontMaterial = mat;
            }

            text.UpdateMeshPadding();
            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }

        /// <summary>アイテムを外す (空スロットにする)。</summary>
        public void ClearItem()
        {
            shopItemData = null;
            if (iconImage != null)        { iconImage.sprite = null; iconImage.enabled = false; }
            if (nameText  != null)        nameText.text = "";
            if (descriptionText != null)  descriptionText.text = "";
            if (valueText != null)        valueText.text = "";
            if (saleBadge != null)        saleBadge.SetActive(false);
            cost = 0;
            RefreshAffordability();
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

            // 価格テキストは買えない時もレアリティ由来の本来色を維持する。
            // 所持金不足の表現はカード全体のグレースケールで行う。
            if (priceText != null)    priceText.color    = priceColorAffordable;
            if (priceTextTMP != null) priceTextTMP.color = priceColorAffordable;
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
