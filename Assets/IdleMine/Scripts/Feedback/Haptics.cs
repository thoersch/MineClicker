using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace IdleMine
{
    /// <summary>Haptic strengths, lightest to heaviest, plus the three system notification patterns.</summary>
    public enum Haptic { Selection, Light, Medium, Heavy, Soft, Rigid, Success, Warning, Error }

    /// <summary>
    /// Taptic Engine feedback on iOS (Assets/Plugins/iOS/IdleMineHaptics.mm); silent in the editor and on
    /// other platforms. Each strength has its own minimum gap so rapid taps feel crisp instead of buzzy.
    /// The player can turn haptics off in Settings; the choice is remembered per device.
    /// </summary>
    public static class Haptics
    {
        const string Key = "haptics.on";
        static readonly float[] MinGap = { 0.03f, 0.05f, 0.07f, 0.1f, 0.05f, 0.05f, 0.25f, 0.25f, 0.25f };
        static readonly float[] Last = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        static int _enabled = -1;

        public static bool Enabled
        {
            get
            {
                if (_enabled < 0) _enabled = PlayerPrefs.GetInt(Key, 1);
                return _enabled == 1;
            }
            set
            {
                _enabled = value ? 1 : 0;
                PlayerPrefs.SetInt(Key, _enabled);
                PlayerPrefs.Save();
            }
        }

        public static void Play(Haptic h)
        {
            if (!Enabled) return;
            int i = (int)h;
            float now = Time.unscaledTime;
            if (Last[i] >= 0 && now - Last[i] < MinGap[i]) return;
            Last[i] = now;
#if UNITY_IOS && !UNITY_EDITOR
            _IdleMineHaptic(i);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _IdleMineHaptic(int type);
#endif
    }
}
