using System.Runtime.InteropServices;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// Mobile haptic feedback wrapper.
    /// iOS では UIImpactFeedbackGeneratorStyleLight を使い、その他の環境では何もしない。
    /// </summary>
    public static class MobileHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BlockBlast_PlayLightImpact();
#endif

        public static void PlayLightImpact()
        {
#if UNITY_IOS && !UNITY_EDITOR
            BlockBlast_PlayLightImpact();
#else
            // Editor / Android / unsupported platforms: intentionally no-op.
#endif
        }
    }
}
