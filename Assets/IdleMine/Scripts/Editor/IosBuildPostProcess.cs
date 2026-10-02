#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Tells Apple the app uses no non-exempt encryption (the game only talks HTTPS, through the ad and
    /// store SDKs), so App Store Connect stops asking the export compliance question on every upload.
    /// </summary>
    public static class IosBuildPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
