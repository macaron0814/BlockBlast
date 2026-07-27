using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// iOS AdMob banner bridge. Google Mobile Ads Unity plugin ではなく、
    /// iOS native + CocoaPods の Google-Mobile-Ads-SDK でバナーを常時表示する。
    /// </summary>
    public class AdMobBannerController : MonoBehaviour
    {
        public enum BannerPosition
        {
            Bottom = 0,
            Top = 1,
        }

        const string ObjectName = "AdMobBannerController";
        const string TestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        const string ProductionBannerAdUnitId = "ca-app-pub-5945355481712765/9051349341";

        [Header("AdMob Banner")]
        [Tooltip("ON: Google のテスト用広告ユニットIDを使う。リリース前に OFF にすると productionBannerAdUnitId を使う。")]
        public bool useTestAd = true;

        [Tooltip("本番用バナー広告ユニットID")]
        public string productionBannerAdUnitId = ProductionBannerAdUnitId;

        [Tooltip("テスト用バナー広告ユニットID")]
        public string testBannerAdUnitId = TestBannerAdUnitId;

        [Tooltip("バナー表示位置")]
        public BannerPosition bannerPosition = BannerPosition.Bottom;

        [Tooltip("起動直後に native view がまだ準備できていない端末があるため、少し遅らせて表示する。")]
        [Min(0f)]
        public float showDelaySeconds = 3f;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BlockBlast_AdMobShowBanner(string adUnitId, int position);

        [DllImport("__Internal")]
        static extern void BlockBlast_AdMobHideBanner();
#endif

        static AdMobBannerController _instance;
        public static AdMobBannerController Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var obj = new GameObject(ObjectName);
            DontDestroyOnLoad(obj);
            _instance = obj.AddComponent<AdMobBannerController>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            Debug.Log($"[AdMobBannerController] Start: waiting {showDelaySeconds}s before ShowBanner()");

            if (showDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(showDelaySeconds);
            else
                yield return null;

            Debug.Log("[AdMobBannerController] Delay elapsed, calling ShowBanner()");
            ShowBanner();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void ShowBanner()
        {
            string adUnitId = useTestAd ? testBannerAdUnitId : productionBannerAdUnitId;
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                Debug.LogWarning("[AdMobBannerController] ShowBanner aborted: adUnitId is empty.");
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"[AdMobBannerController] Calling native BlockBlast_AdMobShowBanner({adUnitId}, {bannerPosition})");
            BlockBlast_AdMobShowBanner(adUnitId, (int)bannerPosition);
            Debug.Log("[AdMobBannerController] Native BlockBlast_AdMobShowBanner call returned.");
#else
            Debug.Log($"[AdMobBannerController] ShowBanner: {adUnitId} ({bannerPosition})");
#endif
        }

        public void HideBanner()
        {
#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_AdMobHideBanner();
#else
            Debug.Log("[AdMobBannerController] HideBanner");
#endif
        }
    }
}
