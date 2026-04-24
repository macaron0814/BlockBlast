using System.Collections;
using TMPro;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// SuperChat プレハブの 1 インスタンス。
    /// Animator が CanvasGroup の alpha をアニメーションするので、
    /// こちらはテキスト・色を設定して一定時間後に破棄するだけ。
    /// </summary>
    public class SuperChatInstance : MonoBehaviour
    {
        [Tooltip("プレハブ内の金額表示テキスト (TMP)。未指定時は子階層から自動探索")]
        public TMP_Text amountText;

        [Tooltip("プレハブ内のカード本体の HueShiftFilter。未指定時は子階層から自動探索")]
        public HueShiftFilter cardFilter;

        [Tooltip("破棄するまでの秒数。Animator 長さ以上にすること")]
        public float lifetime = 2.0f;

        void Awake()
        {
            // 未割り当てでも自動で探す（プレハブを使い回したときの保険）
            if (amountText == null)
                amountText = GetComponentInChildren<TMP_Text>(true);
            if (cardFilter == null)
                cardFilter = GetComponentInChildren<HueShiftFilter>(true);
        }

        public void Setup(int amount, Color cardColor)
        {
            if (amountText != null)
                amountText.text = amount.ToString();

            if (cardFilter != null)
            {
                cardFilter.color = cardColor;
                cardFilter.Apply();
            }
        }

        public void Play()
        {
            StartCoroutine(DestroyAfterLifetime());
        }

        IEnumerator DestroyAfterLifetime()
        {
            yield return new WaitForSeconds(lifetime);
            if (this != null) Destroy(gameObject);
        }
    }
}
