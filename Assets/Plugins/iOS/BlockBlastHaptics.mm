#import <UIKit/UIKit.h>

extern "C" void BlockBlast_PlayLightImpact()
{
    if (@available(iOS 10.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurred];
        });
    }
}
