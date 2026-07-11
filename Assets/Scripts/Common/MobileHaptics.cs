using System.Runtime.InteropServices;
using UnityEngine;

namespace BlockBlastGame
{
    public enum MobileHapticImpactStyle
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Soft = 3,
        Rigid = 4,
    }

    /// <summary>
    /// Mobile haptic feedback wrapper.
    /// 専用プラグイン tsyk5.MobileHapticFeedback を優先して呼ぶ。
    /// </summary>
    public static class MobileHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BlockBlast_PlayLightImpact();
#endif

        public static void PlayLightImpact()
        {
            PlayImpact(MobileHapticImpactStyle.Light);
        }

        public static void PrepareImpact(MobileHapticImpactStyle style)
        {
            tsyk5.MobileHapticFeedback.MobileHapticFeedback.Prepare();
        }

        public static void PlayImpact(MobileHapticImpactStyle style)
        {
            var pluginStyle = ToPluginImpactStyle(style);
            tsyk5.MobileHapticFeedback.MobileHapticFeedback.PlayImpact(pluginStyle);
        }

        public static void PlayCoreImpact(float intensity, float sharpness, double durationSec)
        {
            tsyk5.MobileHapticFeedback.MobileHapticFeedback.PlayImpact(intensity, sharpness, durationSec);
        }

        static tsyk5.MobileHapticFeedback.ImpactStyle ToPluginImpactStyle(MobileHapticImpactStyle style)
        {
            switch (style)
            {
                case MobileHapticImpactStyle.Medium:
                    return tsyk5.MobileHapticFeedback.ImpactStyle.Medium;
                case MobileHapticImpactStyle.Heavy:
                    return tsyk5.MobileHapticFeedback.ImpactStyle.Heavy;
                case MobileHapticImpactStyle.Soft:
                    return tsyk5.MobileHapticFeedback.ImpactStyle.Soft;
                case MobileHapticImpactStyle.Rigid:
                    return tsyk5.MobileHapticFeedback.ImpactStyle.Rigid;
                default:
                    return tsyk5.MobileHapticFeedback.ImpactStyle.Light;
            }
        }
    }
}
