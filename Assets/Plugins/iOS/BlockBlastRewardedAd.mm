#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static NSString *const BlockBlast_RewardedCallbackObject = @"RewardedAdService";
static GADRewardedAd *BlockBlast_RewardedAd = nil;
static NSString *BlockBlast_RewardedUnitId = nil;
static BOOL BlockBlast_RewardedLoading = NO;
static BOOL BlockBlast_RewardedPresentWhenLoaded = NO;

static void BlockBlast_SendRewardedCallback(NSString *method, NSString *message)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage(
            BlockBlast_RewardedCallbackObject.UTF8String,
            method.UTF8String,
            (message ?: @"").UTF8String);
    });
}

static BOOL BlockBlast_HasRewardedAdMobAppId()
{
    NSString *appId = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"GADApplicationIdentifier"];
    return [appId isKindOfClass:[NSString class]]
        && [appId hasPrefix:@"ca-app-pub-"];
}

static UIViewController *BlockBlast_RewardedRootViewController()
{
    UIWindow *targetWindow = nil;
    if (@available(iOS 13.0, *))
    {
        for (UIScene *scene in [UIApplication sharedApplication].connectedScenes)
        {
            if (scene.activationState != UISceneActivationStateForegroundActive
                || ![scene isKindOfClass:[UIWindowScene class]])
                continue;

            for (UIWindow *window in ((UIWindowScene *)scene).windows)
            {
                if (window.isKeyWindow)
                {
                    targetWindow = window;
                    break;
                }
            }
            if (targetWindow != nil)
                break;
        }
    }

    if (targetWindow == nil)
        targetWindow = [UIApplication sharedApplication].keyWindow;

    UIViewController *root = targetWindow.rootViewController;
    while (root.presentedViewController != nil)
        root = root.presentedViewController;
    return root;
}

@interface BlockBlastRewardedDelegate : NSObject <GADFullScreenContentDelegate>
@end

@implementation BlockBlastRewardedDelegate

- (void)adDidDismissFullScreenContent:(id<GADFullScreenPresentingAd>)ad
{
    (void)ad;
    BlockBlast_RewardedAd = nil;
    BlockBlast_SendRewardedCallback(@"OnRewardedAdDismissed", @"");
}

- (void)ad:(id<GADFullScreenPresentingAd>)ad
    didFailToPresentFullScreenContentWithError:(NSError *)error
{
    (void)ad;
    BlockBlast_RewardedAd = nil;
    BlockBlast_SendRewardedCallback(
        @"OnRewardedAdFailed",
        error.localizedDescription ?: @"Failed to present rewarded ad.");
}

@end

static BlockBlastRewardedDelegate *BlockBlast_RewardedDelegateInstance = nil;

static void BlockBlast_PresentRewardedAd()
{
    UIViewController *root = BlockBlast_RewardedRootViewController();
    if (BlockBlast_RewardedAd == nil || root == nil)
    {
        BlockBlast_SendRewardedCallback(@"OnRewardedAdFailed", @"Rewarded ad is not ready.");
        return;
    }

    if (BlockBlast_RewardedDelegateInstance == nil)
        BlockBlast_RewardedDelegateInstance = [BlockBlastRewardedDelegate new];

    BlockBlast_RewardedAd.fullScreenContentDelegate = BlockBlast_RewardedDelegateInstance;
    [BlockBlast_RewardedAd presentFromRootViewController:root
                               userDidEarnRewardHandler:^{
        BlockBlast_SendRewardedCallback(@"OnRewardedAdEarned", @"");
    }];
}

static void BlockBlast_LoadRewardedAd(NSString *unitId, BOOL presentWhenLoaded)
{
    if (!BlockBlast_HasRewardedAdMobAppId())
    {
        BlockBlast_SendRewardedCallback(
            @"OnRewardedAdFailed",
            @"GADApplicationIdentifier is missing.");
        return;
    }

    if (unitId.length == 0)
    {
        BlockBlast_SendRewardedCallback(@"OnRewardedAdFailed", @"Rewarded ad unit ID is empty.");
        return;
    }

    if (BlockBlast_RewardedAd != nil
        && [BlockBlast_RewardedUnitId isEqualToString:unitId])
    {
        if (presentWhenLoaded)
            BlockBlast_PresentRewardedAd();
        return;
    }

    if (BlockBlast_RewardedLoading)
    {
        BlockBlast_RewardedPresentWhenLoaded |= presentWhenLoaded;
        return;
    }

    BlockBlast_RewardedLoading = YES;
    BlockBlast_RewardedPresentWhenLoaded = presentWhenLoaded;
    BlockBlast_RewardedUnitId = [unitId copy];

    [GADRewardedAd loadWithAdUnitID:unitId
                           request:[GADRequest request]
                 completionHandler:^(GADRewardedAd *ad, NSError *error) {
        BlockBlast_RewardedLoading = NO;
        if (error != nil || ad == nil)
        {
            BlockBlast_RewardedAd = nil;
            BlockBlast_RewardedPresentWhenLoaded = NO;
            BlockBlast_SendRewardedCallback(
                @"OnRewardedAdFailed",
                error.localizedDescription ?: @"Failed to load rewarded ad.");
            return;
        }

        BlockBlast_RewardedAd = ad;
        BlockBlast_SendRewardedCallback(@"OnRewardedAdLoaded", @"");
        if (BlockBlast_RewardedPresentWhenLoaded)
        {
            BlockBlast_RewardedPresentWhenLoaded = NO;
            BlockBlast_PresentRewardedAd();
        }
    }];
}

extern "C" void BlockBlast_RewardedLoad(const char *adUnitId)
{
    if (adUnitId == NULL)
        return;

    NSString *unitId = [NSString stringWithUTF8String:adUnitId];
    dispatch_async(dispatch_get_main_queue(), ^{
        BlockBlast_LoadRewardedAd(unitId, NO);
    });
}

extern "C" void BlockBlast_RewardedShow(const char *adUnitId)
{
    if (adUnitId == NULL)
        return;

    NSString *unitId = [NSString stringWithUTF8String:adUnitId];
    dispatch_async(dispatch_get_main_queue(), ^{
        BlockBlast_LoadRewardedAd(unitId, YES);
    });
}
