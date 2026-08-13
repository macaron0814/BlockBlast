using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockBlastGame
{
    /// <summary>
    /// iOS/AndroidネイティブAdMobリワード広告とコンティニュー回数を管理する。
    /// 回数はアプリ内セッションだけで保持し、Titleへ戻るたびに3回へ戻す。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RewardedAdService : MonoBehaviour
    {
        const string ObjectName = "RewardedAdService";
        const string IosTestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
        const string AndroidTestRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
        const string AndroidBridgeClass = "com.blockblast.ads.BlockBlastAdMob";

        public static RewardedAdService Instance { get; private set; }

        [Header("AdMob Rewarded")]
        [Tooltip("iOSでGoogleのテスト用広告ユニットIDを使う。")]
        public bool useTestAd = false;
        public string testRewardedAdUnitId = IosTestRewardedAdUnitId;
        [Tooltip("AdMob管理画面で作成したiOS本番リワード広告ユニットID。")]
        public string productionRewardedAdUnitId = "ca-app-pub-5945355481712765/5986821829";

        [Tooltip("AndroidでGoogleのテスト用広告ユニットIDを使う。")]
        public bool useAndroidTestAd = false;
        public string androidTestRewardedAdUnitId = AndroidTestRewardedAdUnitId;
        [Tooltip("Android本番用リワード広告ユニットID。リリース前に設定する。")]
        public string androidProductionRewardedAdUnitId = "ca-app-pub-5945355481712765/8582029723";

        [Header("Continue")]
        [Min(1)]
        public int maxContinueCount = 3;
        public string titleSceneName = "Title";
        public bool simulateRewardOutsideIOS = true;
        [Tooltip("広告の在庫切れ・読み込み失敗・表示失敗時に、広告なしでコンティニューを成立させる。")]
        public bool continueWhenAdUnavailable = true;

        public int RemainingAttempts { get; private set; } = 3;
        public bool IsShowing { get; private set; }

        public event Action RewardCompleted;
        public event Action<string> RewardFailed;

        bool _rewardEarned;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BlockBlast_RewardedLoad(string adUnitId);

        [DllImport("__Internal")]
        static extern void BlockBlast_RewardedShow(string adUnitId);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInstance()
        {
            if (Instance != null)
                return;

            var obj = new GameObject(ObjectName);
            DontDestroyOnLoad(obj);
            Instance = obj.AddComponent<RewardedAdService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            RemainingAttempts = Mathf.Max(1, maxContinueCount);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void Start()
        {
            Preload();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == titleSceneName)
                RemainingAttempts = Mathf.Max(1, maxContinueCount);
        }

        public bool CanContinue => RemainingAttempts > 0 && !IsShowing;

        public void ShowForContinue()
        {
            if (!CanContinue)
            {
                RewardFailed?.Invoke("コンティニュー可能回数が残っていません。");
                return;
            }

            string adUnitId = ResolveAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                CompleteUnavailable("リワード広告ユニットIDが未設定です。");
                return;
            }

            IsShowing = true;
            _rewardEarned = false;
            AdMobBannerController.Instance?.HideBanner();

#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_RewardedShow(adUnitId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass(AndroidBridgeClass))
                    bridge.CallStatic("showRewarded", adUnitId);
            }
            catch (Exception exception)
            {
                CompleteUnavailable($"Androidリワード広告の呼び出しに失敗しました: {exception.Message}");
            }
#else
            if (simulateRewardOutsideIOS)
                StartCoroutine(SimulateReward());
            else
                CompleteUnavailable("リワード広告はモバイル実機でのみ表示されます。");
#endif
        }

        public void Preload()
        {
            string adUnitId = ResolveAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
                return;

#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_RewardedLoad(adUnitId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass(AndroidBridgeClass))
                    bridge.CallStatic("loadRewarded", adUnitId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RewardedAdService] Android preload failed: {exception.Message}");
            }
#endif
        }

        string ResolveAdUnitId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return useAndroidTestAd
                ? androidTestRewardedAdUnitId
                : androidProductionRewardedAdUnitId;
#else
            return useTestAd ? testRewardedAdUnitId : productionRewardedAdUnitId;
#endif
        }

        IEnumerator SimulateReward()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            _rewardEarned = true;
            OnRewardedAdDismissed("");
        }

        // Native UnitySendMessage callbacks
        public void OnRewardedAdLoaded(string message)
        {
            Debug.Log("[RewardedAdService] Rewarded ad loaded.");
        }

        public void OnRewardedAdEarned(string message)
        {
            if (!IsShowing)
                return;

            _rewardEarned = true;
        }

        public void OnRewardedAdDismissed(string message)
        {
            if (!IsShowing)
                return;

            IsShowing = false;
            AdMobBannerController.Instance?.ShowBanner();

            if (_rewardEarned && RemainingAttempts > 0)
            {
                RemainingAttempts--;
                RewardCompleted?.Invoke();
            }
            else
            {
                RewardFailed?.Invoke("広告視聴が完了しなかったため復活できませんでした。");
            }

            _rewardEarned = false;
            Preload();
        }

        public void OnRewardedAdFailed(string message)
        {
            string failureMessage = string.IsNullOrWhiteSpace(message)
                ? "リワード広告を表示できませんでした。"
                : message;

            // iOS はバックグラウンドの事前読み込み失敗でもこのコールバックを返す。
            // ユーザーがコンティニューを要求していない場合は処理を進めない。
            if (!IsShowing)
            {
                Debug.LogWarning($"[RewardedAdService] Rewarded preload failed: {failureMessage}");
                return;
            }

            CompleteUnavailable(failureMessage);
        }

        void CompleteUnavailable(string message)
        {
            if (!continueWhenAdUnavailable)
            {
                CompleteFailure(message);
                return;
            }

            IsShowing = false;
            _rewardEarned = false;
            AdMobBannerController.Instance?.ShowBanner();

            if (RemainingAttempts > 0)
            {
                RemainingAttempts--;
                Debug.LogWarning(
                    $"[RewardedAdService] 広告を用意できなかったため、広告なしでコンティニューします: {message}");
                RewardCompleted?.Invoke();
            }
            else
            {
                RewardFailed?.Invoke("コンティニュー可能回数が残っていません。");
            }

            Preload();
        }

        void CompleteFailure(string message)
        {
            IsShowing = false;
            _rewardEarned = false;
            AdMobBannerController.Instance?.ShowBanner();
            RewardFailed?.Invoke(message);
            Debug.LogWarning($"[RewardedAdService] {message}");
        }
    }
}
