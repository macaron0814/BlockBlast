using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// iOS/AndroidネイティブAdMobバナーを常時表示する。
    /// </summary>
    public class AdMobBannerController : MonoBehaviour
    {
        public enum BannerPosition
        {
            Bottom = 0,
            Top = 1,
        }

        const string ObjectName = "AdMobBannerController";
        const string IosTestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        const string IosProductionBannerAdUnitId = "ca-app-pub-5945355481712765/9051349341";
        const string AndroidTestBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        const string AndroidBridgeClass = "com.blockblast.ads.BlockBlastAdMob";

        [Header("AdMob Banner")]
        [Tooltip("iOSでGoogleのテスト用広告ユニットIDを使う。")]
        public bool useTestAd = false;

        [Tooltip("iOS本番用バナー広告ユニットID")]
        public string productionBannerAdUnitId = IosProductionBannerAdUnitId;

        [Tooltip("iOSテスト用バナー広告ユニットID")]
        public string testBannerAdUnitId = IosTestBannerAdUnitId;

        [Tooltip("AndroidでGoogleのテスト用広告ユニットIDを使う。")]
        public bool useAndroidTestAd = false;

        [Tooltip("Androidテスト用バナー広告ユニットID")]
        public string androidTestBannerAdUnitId = AndroidTestBannerAdUnitId;

        [Tooltip("Android本番用バナー広告ユニットID。リリース前に設定する。")]
        public string androidProductionBannerAdUnitId = "ca-app-pub-5945355481712765/2290499875";

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
            string adUnitId = ResolveAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                Debug.LogWarning("[AdMobBannerController] ShowBanner aborted: adUnitId is empty.");
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"[AdMobBannerController] Calling native BlockBlast_AdMobShowBanner({adUnitId}, {bannerPosition})");
            BlockBlast_AdMobShowBanner(adUnitId, (int)bannerPosition);
            Debug.Log("[AdMobBannerController] Native BlockBlast_AdMobShowBanner call returned.");
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass(AndroidBridgeClass))
                bridge.CallStatic("showBanner", adUnitId, (int)bannerPosition);
#else
            Debug.Log($"[AdMobBannerController] ShowBanner: {adUnitId} ({bannerPosition})");
#endif
        }

        public void HideBanner()
        {
#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_AdMobHideBanner();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass(AndroidBridgeClass))
                bridge.CallStatic("hideBanner");
#else
            Debug.Log("[AdMobBannerController] HideBanner");
#endif
        }

        string ResolveAdUnitId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return useAndroidTestAd
                ? androidTestBannerAdUnitId
                : androidProductionBannerAdUnitId;
#else
            return useTestAd ? testBannerAdUnitId : productionBannerAdUnitId;
#endif
        }
    }
}
