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
        const string EnabledPlayerPrefsKey = "HapticsEnabled";
        static bool? _enabled;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BlockBlast_PlayLightImpact();
#endif

        public static bool IsEnabled
        {
            get
            {
                if (!_enabled.HasValue)
                    _enabled = PlayerPrefs.GetInt(EnabledPlayerPrefsKey, 1) != 0;

                return _enabled.Value;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            PlayerPrefs.SetInt(EnabledPlayerPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void PlayLightImpact()
        {
            PlayImpact(MobileHapticImpactStyle.Light);
        }

        public static void PrepareImpact(MobileHapticImpactStyle style)
        {
            if (!IsEnabled)
                return;

            tsyk5.MobileHapticFeedback.MobileHapticFeedback.Prepare();
        }

        public static void PlayImpact(MobileHapticImpactStyle style)
        {
            if (!IsEnabled)
                return;

            var pluginStyle = ToPluginImpactStyle(style);
            tsyk5.MobileHapticFeedback.MobileHapticFeedback.PlayImpact(pluginStyle);
        }

        public static void PlayCoreImpact(float intensity, float sharpness, double durationSec)
        {
            if (!IsEnabled)
                return;

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
