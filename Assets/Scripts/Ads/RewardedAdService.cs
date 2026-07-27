using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockBlastGame
{
    /// <summary>
    /// iOSネイティブAdMobリワード広告とコンティニュー回数を管理する。
    /// 回数はアプリ内セッションだけで保持し、Titleへ戻るたびに3回へ戻す。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RewardedAdService : MonoBehaviour
    {
        const string ObjectName = "RewardedAdService";
        const string TestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

        public static RewardedAdService Instance { get; private set; }

        [Header("AdMob Rewarded")]
        public bool useTestAd = true;
        public string testRewardedAdUnitId = TestRewardedAdUnitId;
        [Tooltip("AdMob管理画面で作成した本番リワード広告ユニットID。")]
        public string productionRewardedAdUnitId = "";

        [Header("Continue")]
        [Min(1)]
        public int maxContinueCount = 3;
        public string titleSceneName = "Title";
        public bool simulateRewardOutsideIOS = true;

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
                RewardFailed?.Invoke("リワード広告ユニットIDが未設定です。");
                return;
            }

            IsShowing = true;
            _rewardEarned = false;
            AdMobBannerController.Instance?.HideBanner();

#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_RewardedShow(adUnitId);
#else
            if (simulateRewardOutsideIOS)
                StartCoroutine(SimulateReward());
            else
                CompleteFailure("リワード広告はiOS実機でのみ表示されます。");
#endif
        }

        public void Preload()
        {
            string adUnitId = ResolveAdUnitId();
            if (string.IsNullOrWhiteSpace(adUnitId))
                return;

#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_RewardedLoad(adUnitId);
#endif
        }

        string ResolveAdUnitId()
        {
            return useTestAd ? testRewardedAdUnitId : productionRewardedAdUnitId;
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
            _rewardEarned = true;
        }

        public void OnRewardedAdDismissed(string message)
        {
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
            CompleteFailure(string.IsNullOrWhiteSpace(message)
                ? "リワード広告を表示できませんでした。"
                : message);
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
