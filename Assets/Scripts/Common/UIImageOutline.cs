using UnityEngine;
using UnityEngine.UI;

namespace BlockBlastGame
{
    /// <summary>
    /// UI Image / TMP 以外の Graphic に、スクリプトから縁 (Outline) を付ける小さな汎用コンポーネント。
    ///
    /// 使い方:
    /// 1. 縁を付けたい Image と同じ GameObject にこのコンポーネントを付ける
    /// 2. effectColor / effectDistance を調整
    /// 3. ShopCard.selectionOutline に割り当てると、カード選択時だけ表示される
    ///
    /// Unity 標準の Outline コンポーネントを内部で使うため、追加シェーダー不要。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UIImageOutline : MonoBehaviour
    {
        [Header("Outline")]
        [Tooltip("縁の色")]
        public Color effectColor = Color.white;

        [Tooltip("縁の太さ。UI 座標の px 相当")]
        public Vector2 effectDistance = new Vector2(4f, -4f);

        [Tooltip("Graphic 本体のアルファを縁に反映する")]
        public bool useGraphicAlpha = true;

        [Header("State")]
        [Tooltip("起動時に縁を表示するか")]
        public bool visibleOnAwake = false;

        Outline _outline;

        void Awake()
        {
            EnsureOutline();
            ApplySettings();
            SetVisible(visibleOnAwake);
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            EnsureOutline();
            ApplySettings();
        }

        void EnsureOutline()
        {
            if (_outline != null) return;

            _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
        }

        public void ApplySettings()
        {
            EnsureOutline();
            if (_outline == null) return;

            _outline.effectColor = effectColor;
            _outline.effectDistance = effectDistance;
            _outline.useGraphicAlpha = useGraphicAlpha;
        }

        public void SetVisible(bool visible)
        {
            EnsureOutline();
            ApplySettings();

            if (_outline != null)
                _outline.enabled = visible;
        }

        public void SetColor(Color color)
        {
            effectColor = color;
            ApplySettings();
        }

        public void SetDistance(Vector2 distance)
        {
            effectDistance = distance;
            ApplySettings();
        }
    }
}
