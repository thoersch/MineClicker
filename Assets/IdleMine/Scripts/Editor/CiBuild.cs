using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Command-line iOS export used by the GitHub Actions workflow (.github/workflows/ios-app-store.yml).
    /// GameCI passes the output folder as -customBuildPath. Produces an Xcode project; the workflow then
    /// compiles, signs and uploads it on a Mac runner.
    /// </summary>
    public static class CiBuild
    {
        // Unity IAP 5 uses StoreKit 2, which needs iOS 15.
        const string MinIosVersion = "15.0";

        public static void BuildIos()
        {
            if (new Version(PlayerSettings.iOS.targetOSVersionString) < new Version(MinIosVersion))
                PlayerSettings.iOS.targetOSVersionString = MinIosVersion;

            string path = Arg("-customBuildPath") ?? "build/iOS/iOS";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });

            Debug.Log("[IdleMine] iOS export " + report.summary.result + " -> " + path);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
