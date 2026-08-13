package com.blockblast.ads;

import android.app.Activity;
import android.graphics.Color;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.AdSize;
import com.google.android.gms.ads.AdView;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;
import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;
import java.util.List;

public final class BlockBlastAdMob {
    private static final String TAG = "BlockBlastAdMob";
    private static final String REWARDED_CALLBACK_OBJECT = "RewardedAdService";

    private static final List<Runnable> initializationQueue = new ArrayList<>();
    private static boolean initialized;
    private static boolean initializing;

    private static AdView bannerView;
    private static String bannerUnitId;

    private static RewardedAd rewardedAd;
    private static String rewardedUnitId;
    private static boolean rewardedLoading;
    private static boolean showRewardedWhenLoaded;

    private BlockBlastAdMob() {
    }

    public static void showBanner(final String adUnitId, final int position) {
        runOnUiThread(() -> initialize(() -> showBannerInitialized(adUnitId, position)));
    }

    public static void hideBanner() {
        runOnUiThread(() -> {
            if (bannerView != null) {
                bannerView.setVisibility(View.GONE);
                bannerView.pause();
            }
        });
    }

    public static void loadRewarded(final String adUnitId) {
        runOnUiThread(() -> initialize(() -> loadRewardedInitialized(adUnitId, false)));
    }

    public static void showRewarded(final String adUnitId) {
        runOnUiThread(() -> initialize(() -> {
            showRewardedWhenLoaded = true;

            if (rewardedAd != null && adUnitId.equals(rewardedUnitId)) {
                presentRewarded();
                return;
            }

            loadRewardedInitialized(adUnitId, true);
        }));
    }

    private static void initialize(Runnable continuation) {
        if (initialized) {
            continuation.run();
            return;
        }

        initializationQueue.add(continuation);
        if (initializing) {
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            initializationQueue.clear();
            sendRewardedCallback("OnRewardedAdFailed", "Android Activityが見つかりません。");
            return;
        }

        initializing = true;
        MobileAds.initialize(activity, status -> runOnUiThread(() -> {
            initialized = true;
            initializing = false;

            List<Runnable> queued = new ArrayList<>(initializationQueue);
            initializationQueue.clear();
            for (Runnable runnable : queued) {
                runnable.run();
            }
        }));
    }

    private static void showBannerInitialized(String adUnitId, int position) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            return;
        }

        if (bannerView != null && adUnitId.equals(bannerUnitId)) {
            updateBannerPosition(position);
            bannerView.resume();
            bannerView.setVisibility(View.VISIBLE);
            return;
        }

        destroyBanner();

        bannerUnitId = adUnitId;
        bannerView = new AdView(activity);
        bannerView.setAdSize(AdSize.BANNER);
        bannerView.setAdUnitId(adUnitId);
        bannerView.setBackgroundColor(Color.TRANSPARENT);
        bannerView.setAdListener(new AdListener() {
            @Override
            public void onAdFailedToLoad(LoadAdError error) {
                Log.w(TAG, "Banner load failed: " + error);
            }
        });

        FrameLayout.LayoutParams params = createBannerLayoutParams(position);
        activity.addContentView(bannerView, params);
        bannerView.loadAd(new AdRequest.Builder().build());
    }

    private static void updateBannerPosition(int position) {
        if (bannerView == null) {
            return;
        }

        ViewGroup.LayoutParams current = bannerView.getLayoutParams();
        FrameLayout.LayoutParams updated = createBannerLayoutParams(position);
        if (current instanceof FrameLayout.LayoutParams) {
            updated.leftMargin = ((FrameLayout.LayoutParams) current).leftMargin;
            updated.rightMargin = ((FrameLayout.LayoutParams) current).rightMargin;
        }
        bannerView.setLayoutParams(updated);
    }

    private static FrameLayout.LayoutParams createBannerLayoutParams(int position) {
        int gravity = Gravity.CENTER_HORIZONTAL;
        gravity |= position == 1 ? Gravity.TOP : Gravity.BOTTOM;
        return new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                gravity);
    }

    private static void destroyBanner() {
        if (bannerView == null) {
            return;
        }

        ViewGroup parent = (ViewGroup) bannerView.getParent();
        if (parent != null) {
            parent.removeView(bannerView);
        }
        bannerView.destroy();
        bannerView = null;
        bannerUnitId = null;
    }

    private static void loadRewardedInitialized(String adUnitId, boolean requestedForShow) {
        if (rewardedAd != null && adUnitId.equals(rewardedUnitId)) {
            if (requestedForShow || showRewardedWhenLoaded) {
                presentRewarded();
            }
            return;
        }

        if (rewardedLoading && adUnitId.equals(rewardedUnitId)) {
            showRewardedWhenLoaded |= requestedForShow;
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            if (requestedForShow) {
                sendRewardedCallback("OnRewardedAdFailed", "Android Activityが見つかりません。");
            }
            return;
        }

        rewardedAd = null;
        rewardedUnitId = adUnitId;
        rewardedLoading = true;
        showRewardedWhenLoaded |= requestedForShow;

        RewardedAd.load(
                activity,
                adUnitId,
                new AdRequest.Builder().build(),
                new RewardedAdLoadCallback() {
                    @Override
                    public void onAdLoaded(RewardedAd ad) {
                        rewardedLoading = false;
                        rewardedAd = ad;
                        configureRewardedCallbacks(ad);
                        sendRewardedCallback("OnRewardedAdLoaded", "");

                        if (showRewardedWhenLoaded) {
                            presentRewarded();
                        }
                    }

                    @Override
                    public void onAdFailedToLoad(LoadAdError error) {
                        boolean wasWaitingToShow = showRewardedWhenLoaded;
                        rewardedLoading = false;
                        rewardedAd = null;
                        showRewardedWhenLoaded = false;
                        Log.w(TAG, "Rewarded load failed: " + error);

                        if (wasWaitingToShow) {
                            sendRewardedCallback("OnRewardedAdFailed", error.toString());
                        }
                    }
                });
    }

    private static void configureRewardedCallbacks(RewardedAd ad) {
        ad.setFullScreenContentCallback(new FullScreenContentCallback() {
            @Override
            public void onAdDismissedFullScreenContent() {
                rewardedAd = null;
                showRewardedWhenLoaded = false;
                sendRewardedCallback("OnRewardedAdDismissed", "");
            }

            @Override
            public void onAdFailedToShowFullScreenContent(AdError error) {
                rewardedAd = null;
                showRewardedWhenLoaded = false;
                sendRewardedCallback("OnRewardedAdFailed", error.toString());
            }
        });
    }

    private static void presentRewarded() {
        Activity activity = UnityPlayer.currentActivity;
        RewardedAd ad = rewardedAd;
        if (activity == null || ad == null) {
            sendRewardedCallback("OnRewardedAdFailed", "リワード広告の準備ができていません。");
            return;
        }

        rewardedAd = null;
        showRewardedWhenLoaded = false;
        ad.show(activity, rewardItem ->
                sendRewardedCallback("OnRewardedAdEarned", rewardItem.getType()));
    }

    private static void sendRewardedCallback(String method, String message) {
        UnityPlayer.UnitySendMessage(
                REWARDED_CALLBACK_OBJECT,
                method,
                message == null ? "" : message);
    }

    private static void runOnUiThread(Runnable runnable) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "UnityPlayer.currentActivity is null.");
            return;
        }
        activity.runOnUiThread(runnable);
    }
}
