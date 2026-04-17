using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// ライン消去で消えたセル数に応じて「セル数 × caloriesPerBlock」カロリーを加算し、
    /// sprite 数字で右揃え表示する UI コンポーネント。
    ///
    /// ■ セットアップ
    /// 1. Canvas 上の任意の GameObject にアタッチ
    /// 2. digitSprites[0..9] に数字スプライトを順番にアサイン
    /// 3. kcalLabelSprite にラベル画像をアサイン（省略可）
    /// 4. Inspector の各パラメータで見た目・位置を調整
    ///
    /// ■ 位置調整
    ///   digitOffset  … 数字グループ全体の追加オフセット (px)
    ///   labelOffset  … kcal ラベルの追加オフセット (px)
    ///   labelOnLeft  … true にするとラベルを数字の左に配置
    /// </summary>
    public class CalorieDisplay : MonoBehaviour
    {
        // ────────────────────────────────────────
        //  Inspector
        // ────────────────────────────────────────

        [Header("桁スプライト (0〜9 の順)")]
        public Sprite[] digitSprites = new Sprite[10];

        [Header("kcal ラベルスプライト（null で非表示）")]
        public Sprite kcalLabelSprite;

        [Header("カロリー設定")]
        [Tooltip("ブロック 1 セルを消したときに加算するカロリー")]
        public int caloriesPerBlock = 100;

        [Header("表示設定")]
        [Tooltip("最大桁数。例: 5 → 0〜99999")]
        public int maxDigits = 5;

        [Tooltip("桁の縦横サイズ (px)。useNativeSize が true のときは無視")]
        public Vector2 digitSize = new Vector2(60f, 80f);

        [Tooltip("スプライト本来のサイズを使うか")]
        public bool useNativeSize = true;

        [Tooltip("スケール倍率（useNativeSize 時も有効）")]
        public float digitScale = 1f;

        [Tooltip("桁同士の隙間 (px)")]
        public float digitGap = 4f;

        [Tooltip("kcal ラベルと数字の隙間 (px)")]
        public float labelGap = 8f;

        [Tooltip("表示桁数まで 0 で埋めるか")]
        public bool zeroPadToMaxDigits = false;

        [Header("位置微調整")]
        [Tooltip("数字グループ全体を追加でずらすオフセット (px)。X: 左右, Y: 上下")]
        public Vector2 digitOffset = Vector2.zero;

        [Tooltip("kcal ラベルを追加でずらすオフセット (px)。X: 左右, Y: 上下")]
        public Vector2 labelOffset = Vector2.zero;

        [Tooltip("true: kcal ラベルを数字の左に配置 / false: 数字の右に配置（デフォルト）")]
        public bool labelOnLeft = false;

        // ────────────────────────────────────────
        //  Runtime
        // ────────────────────────────────────────

        int _totalCalories;
        int _displayedCalories = -1;

        Image[] _digitImages;
        Image   _labelImage;

        // ────────────────────────────────────────
        //  Lifecycle
        // ────────────────────────────────────────

        void Awake()
        {
            BuildImages();
        }

        void OnEnable()
        {
            GameEvents.OnLineClearWithCells += HandleLineClear;
            GameEvents.OnCalorieChanged     += HandleCalorieChanged;
        }

        void OnDisable()
        {
            GameEvents.OnLineClearWithCells -= HandleLineClear;
            GameEvents.OnCalorieChanged     -= HandleCalorieChanged;
        }

        // ────────────────────────────────────────
        //  Event handlers
        // ────────────────────────────────────────

        void HandleLineClear(int linesCleared, int cellsCleared, int comboCount)
        {
            AddCalories(cellsCleared * caloriesPerBlock);
        }

        void HandleCalorieChanged(int total)
        {
            _totalCalories = total;
            RefreshDisplay();
        }

        // ────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────

        public int TotalCalories => _totalCalories;

        public void AddCalories(int amount)
        {
            _totalCalories += amount;
            GameEvents.TriggerCalorieChanged(_totalCalories);
        }

        public void ResetCalories()
        {
            _totalCalories = 0;
            GameEvents.TriggerCalorieChanged(_totalCalories);
        }

        // ────────────────────────────────────────
        //  Build UI — 子 Image を生成
        // ────────────────────────────────────────

        void BuildImages()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            _digitImages = new Image[maxDigits];
            for (int i = 0; i < maxDigits; i++)
            {
                var go  = new GameObject($"Digit_{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget  = false;
                _digitImages[i]    = img;
            }

            if (kcalLabelSprite != null)
            {
                var go  = new GameObject("KcalLabel", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _labelImage                = go.AddComponent<Image>();
                _labelImage.preserveAspect = true;
                _labelImage.raycastTarget  = false;
                _labelImage.sprite         = kcalLabelSprite;
            }

            _displayedCalories = -1;
            RefreshDisplay();
        }

        // ────────────────────────────────────────
        //  Refresh — 右揃えレイアウト
        //
        //  座標系: pivot = (1, 0.5) を前提として右端を 0 基準に左方向へ積む。
        //  labelOnLeft = false (デフォルト): [数字] [kcal]
        //  labelOnLeft = true             : [kcal] [数字]
        // ────────────────────────────────────────

        void RefreshDisplay()
        {
            if (_digitImages == null) BuildImages();
            if (_displayedCalories == _totalCalories) return;
            _displayedCalories = _totalCalories;

            string valueText = Mathf.Max(0, _totalCalories).ToString();
            if (valueText.Length > maxDigits)
                valueText = valueText.Substring(valueText.Length - maxDigits);
            else if (zeroPadToMaxDigits)
                valueText = valueText.PadLeft(maxDigits, '0');

            // 桁の基準サイズ
            float dw = digitSize.x;
            float dh = digitSize.y;
            if (useNativeSize && digitSprites != null && digitSprites.Length > 0 && digitSprites[0] != null)
            {
                dw = digitSprites[0].rect.width  * digitScale;
                dh = digitSprites[0].rect.height * digitScale;
            }

            // ラベルサイズ
            float lw = 0f, lh = 0f;
            if (_labelImage != null && kcalLabelSprite != null)
            {
                lw = useNativeSize ? kcalLabelSprite.rect.width  * digitScale : dw;
                lh = useNativeSize ? kcalLabelSprite.rect.height * digitScale : dh;
            }

            foreach (var img in _digitImages)
                img.gameObject.SetActive(false);

            float cursor = 0f;

            if (!labelOnLeft && _labelImage != null)
            {
                PlaceElement(_labelImage.rectTransform, lw, lh, cursor + labelOffset.x, labelOffset.y);
                cursor -= lw + labelGap;
            }

            int slot = 0;
            for (int charIndex = valueText.Length - 1; charIndex >= 0 && slot < _digitImages.Length; charIndex--, slot++)
            {
                int digit = valueText[charIndex] - '0';
                var img = _digitImages[slot];
                var sp = (digitSprites != null && digit < digitSprites.Length) ? digitSprites[digit] : null;

                float cw = dw;
                float ch = dh;
                if (sp != null)
                {
                    img.sprite = sp;
                    if (useNativeSize)
                    {
                        cw = sp.rect.width  * digitScale;
                        ch = sp.rect.height * digitScale;
                    }
                }
                else
                {
                    img.sprite = null;
                }

                PlaceElement(img.rectTransform, cw, ch, cursor + digitOffset.x, digitOffset.y);
                img.gameObject.SetActive(true);
                cursor -= cw + digitGap;
            }

            if (labelOnLeft && _labelImage != null)
                PlaceElement(_labelImage.rectTransform, lw, lh, cursor + labelOffset.x, labelOffset.y);
        }

        void PlaceElement(RectTransform rectTransform, float width, float height, float x, float y)
        {
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = new Vector2(x, y);
        }

        // ────────────────────────────────────────
        //  Editor preview
        // ────────────────────────────────────────

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying) return;
            BuildImages();
        }
#endif
    }
}
