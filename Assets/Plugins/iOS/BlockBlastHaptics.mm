#import <UIKit/UIKit.h>

static UIImpactFeedbackStyle BlockBlast_ImpactStyle(int style)
{
    if (@available(iOS 13.0, *))
    {
        if (style == 3) return UIImpactFeedbackStyleSoft;
        if (style == 4) return UIImpactFeedbackStyleRigid;
    }

    if (style == 1) return UIImpactFeedbackStyleMedium;
    if (style == 2) return UIImpactFeedbackStyleHeavy;
    return UIImpactFeedbackStyleLight;
}

extern "C" void BlockBlast_PlayLightImpact()
{
    if (@available(iOS 10.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:BlockBlast_ImpactStyle(0)];
            [generator prepare];
            [generator impactOccurred];
        });
    }
}

extern "C" void MHF_PrepareCoreHaptics()
{
    if (@available(iOS 10.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
        });
    }
}

extern "C" void MHF_PlayUIKitImpact(int style)
{
    if (@available(iOS 10.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:BlockBlast_ImpactStyle(style)];
            [generator prepare];
            [generator impactOccurred];
        });
    }
}

extern "C" void MHF_PlayCoreImpact(float intensity, float sharpness, double duration)
{
    // The external MobileHapticFeedback C# package expects this symbol.
    // Keep the native side intentionally weak for BlockHover by mapping it to Light impact.
    (void)intensity;
    (void)sharpness;
    (void)duration;
    MHF_PlayUIKitImpact(0);
}
