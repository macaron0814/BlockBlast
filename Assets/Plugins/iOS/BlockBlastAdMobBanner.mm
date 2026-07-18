#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

static GADBannerView *BlockBlast_BannerView = nil;
static BOOL BlockBlast_AdMobStarted = NO;
static const CGFloat BlockBlast_BannerDownwardOffset = 12.0;

// GADApplicationIdentifier が Info.plist に無い/空/不正な場合、
// Google Mobile Ads SDK は起動時に内部で強制終了 (abort) する仕様がある。
// このプロジェクトは Objective-C 例外が無効なため @try/@catch では捕まえられないので、
// SDK を呼び出す前に必ず自前でチェックし、ダメなら広告処理そのものをスキップする。
static BOOL BlockBlast_HasValidAdMobAppId()
{
    NSString *appId = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"GADApplicationIdentifier"];
    if (appId == nil || ![appId isKindOfClass:[NSString class]])
        return NO;

    NSString *trimmed = [appId stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    if (trimmed.length == 0)
        return NO;

    return [trimmed hasPrefix:@"ca-app-pub-"];
}

static UIViewController *BlockBlast_RootViewController()
{
    UIWindow *targetWindow = nil;

    if (@available(iOS 13.0, *))
    {
        NSSet<UIScene *> *scenes = [UIApplication sharedApplication].connectedScenes;
        for (UIScene *scene in scenes)
        {
            if (scene.activationState != UISceneActivationStateForegroundActive ||
                ![scene isKindOfClass:[UIWindowScene class]])
            {
                continue;
            }

            UIWindowScene *windowScene = (UIWindowScene *)scene;
            for (UIWindow *window in windowScene.windows)
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

    if (targetWindow == nil)
        return nil;

    UIViewController *root = targetWindow.rootViewController;
    while (root.presentedViewController != nil)
        root = root.presentedViewController;

    return root;
}

static void BlockBlast_LayoutBanner(int position)
{
    if (BlockBlast_BannerView == nil || BlockBlast_BannerView.superview == nil)
        return;

    UIView *parent = BlockBlast_BannerView.superview;
    CGSize bannerSize = BlockBlast_BannerView.bounds.size;
    if (bannerSize.width <= 0.01 || bannerSize.height <= 0.01)
        bannerSize = CGSizeMake(320.0, 50.0);

    UIEdgeInsets safeInsets = UIEdgeInsetsZero;
    if (@available(iOS 11.0, *))
        safeInsets = parent.safeAreaInsets;

    CGFloat safeLeft = safeInsets.left;
    CGFloat safeRight = parent.bounds.size.width - safeInsets.right;
    CGFloat x = safeLeft + (safeRight - safeLeft - bannerSize.width) * 0.5;
    CGFloat y = 0.0;
    if (position == 1)
    {
        y = safeInsets.top;
    }
    else
    {
        CGFloat bottomInset = MAX(0.0, safeInsets.bottom - BlockBlast_BannerDownwardOffset);
        y = parent.bounds.size.height - bannerSize.height - bottomInset;
    }

    BlockBlast_BannerView.frame = CGRectMake(x, y, bannerSize.width, bannerSize.height);
}

static void BlockBlast_CreateAndLoadBanner(NSString *unitId, int position)
{
    NSLog(@"[BlockBlastAdMobBanner] STEP: CreateAndLoadBanner start");

    UIViewController *root = BlockBlast_RootViewController();
    if (root == nil || root.view == nil)
    {
        NSLog(@"[BlockBlastAdMobBanner] STEP: CreateAndLoadBanner abort (root view controller is nil)");
        return;
    }

    NSLog(@"[BlockBlastAdMobBanner] STEP: got root view controller %@", root);

    if (BlockBlast_BannerView != nil)
    {
        [BlockBlast_BannerView removeFromSuperview];
        BlockBlast_BannerView = nil;
    }

    // Unity の描画領域は変更せず、画面最下部のSafe Area直上へ
    // 固定320x50ptの標準バナーを中央配置する。
    NSLog(@"[BlockBlastAdMobBanner] STEP: alloc fixed 320x50 GADBannerView");
    BlockBlast_BannerView = [[GADBannerView alloc] initWithAdSize:GADAdSizeBanner];
    NSLog(@"[BlockBlastAdMobBanner] STEP: GADBannerView allocated %@", BlockBlast_BannerView);

    BlockBlast_BannerView.adUnitID = unitId;
    BlockBlast_BannerView.rootViewController = root;
    BlockBlast_BannerView.backgroundColor = UIColor.clearColor;
    BlockBlast_BannerView.autoresizingMask =
        UIViewAutoresizingFlexibleTopMargin |
        UIViewAutoresizingFlexibleBottomMargin |
        UIViewAutoresizingFlexibleLeftMargin |
        UIViewAutoresizingFlexibleRightMargin;

    NSLog(@"[BlockBlastAdMobBanner] STEP: addSubview");
    [root.view addSubview:BlockBlast_BannerView];
    NSLog(@"[BlockBlastAdMobBanner] STEP: layoutBanner");
    BlockBlast_LayoutBanner(position);
    NSLog(@"[BlockBlastAdMobBanner] STEP: loadRequest (adUnitId=%@)", unitId);
    [BlockBlast_BannerView loadRequest:[GADRequest request]];
    NSLog(@"[BlockBlastAdMobBanner] STEP: CreateAndLoadBanner done");
}

static void BlockBlast_StartAdMobAndLoadBanner(NSString *unitId, int position)
{
    NSLog(@"[BlockBlastAdMobBanner] STEP: StartAdMobAndLoadBanner start");

    if (!BlockBlast_HasValidAdMobAppId())
    {
        NSLog(@"[BlockBlastAdMobBanner] GADApplicationIdentifier が Info.plist に見つからないため広告処理をスキップします。");
        return;
    }

    NSLog(@"[BlockBlastAdMobBanner] STEP: calling [GADMobileAds sharedInstance]");
    GADMobileAds *mobileAds = [GADMobileAds sharedInstance];
    NSLog(@"[BlockBlastAdMobBanner] STEP: got GADMobileAds instance %@, calling startWithCompletionHandler", mobileAds);

    [mobileAds startWithCompletionHandler:^(GADInitializationStatus *status) {
        NSLog(@"[BlockBlastAdMobBanner] STEP: startWithCompletionHandler callback fired, status=%@", status);
        (void)status;
        BlockBlast_AdMobStarted = YES;
        dispatch_async(dispatch_get_main_queue(), ^{
            BlockBlast_CreateAndLoadBanner(unitId, position);
        });
    }];

    NSLog(@"[BlockBlastAdMobBanner] STEP: startWithCompletionHandler call returned (async, waiting for callback)");
}

static void BlockBlast_RequestTrackingThenStartAdMob(NSString *unitId, int position)
{
    NSLog(@"[BlockBlastAdMobBanner] STEP: RequestTrackingThenStartAdMob start");

    if (@available(iOS 14.0, *))
    {
        NSLog(@"[BlockBlastAdMobBanner] STEP: checking ATTrackingManager.trackingAuthorizationStatus");
        ATTrackingManagerAuthorizationStatus status = [ATTrackingManager trackingAuthorizationStatus];
        NSLog(@"[BlockBlastAdMobBanner] STEP: current ATT status=%ld", (long)status);

        if (status == ATTrackingManagerAuthorizationStatusNotDetermined)
        {
            NSLog(@"[BlockBlastAdMobBanner] STEP: requesting ATT authorization");
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus authorizationStatus) {
                NSLog(@"[BlockBlastAdMobBanner] STEP: ATT authorization callback fired, status=%ld", (long)authorizationStatus);
                (void)authorizationStatus;
                dispatch_async(dispatch_get_main_queue(), ^{
                    BlockBlast_StartAdMobAndLoadBanner(unitId, position);
                });
            }];
            return;
        }
    }
    else
    {
        NSLog(@"[BlockBlastAdMobBanner] STEP: iOS < 14, skipping ATT check");
    }

    BlockBlast_StartAdMobAndLoadBanner(unitId, position);
}

extern "C" void BlockBlast_AdMobShowBanner(const char *adUnitId, int position)
{
    NSLog(@"[BlockBlastAdMobBanner] STEP: BlockBlast_AdMobShowBanner called from Unity (position=%d)", position);

    if (adUnitId == NULL)
    {
        NSLog(@"[BlockBlastAdMobBanner] STEP: abort, adUnitId is NULL");
        return;
    }

    NSString *unitId = [NSString stringWithUTF8String:adUnitId];
    if (unitId.length == 0)
    {
        NSLog(@"[BlockBlastAdMobBanner] STEP: abort, adUnitId is empty");
        return;
    }

    NSLog(@"[BlockBlastAdMobBanner] STEP: unitId=%@", unitId);

    // 遅延はC#側 (AdMobBannerController.showDelaySeconds) の1箇所のみで管理する。
    // ネイティブ側で追加の待機を入れると合計時間が把握しづらくなるため、
    // ここでは即時(メインスレッドディスパッチのみ)で処理する。
    dispatch_async(dispatch_get_main_queue(), ^{
        NSLog(@"[BlockBlastAdMobBanner] STEP: on main queue, AdMobStarted=%d", BlockBlast_AdMobStarted);
        if (BlockBlast_AdMobStarted)
        {
            BlockBlast_CreateAndLoadBanner(unitId, position);
            return;
        }

        BlockBlast_RequestTrackingThenStartAdMob(unitId, position);
    });
}

extern "C" void BlockBlast_AdMobHideBanner()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (BlockBlast_BannerView != nil)
        {
            [BlockBlast_BannerView removeFromSuperview];
            BlockBlast_BannerView = nil;
        }
    });
}
