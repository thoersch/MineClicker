using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Idle Mine > Monetization > Set Up iOS SDKs.
    /// Turns on the real AdMob and Unity IAP code for iOS builds (scripting defines), fills in the
    /// Google Mobile Ads settings that iOS needs (the AdMob app id and the App Tracking Transparency
    /// prompt text) and puts Deepforge's rewarded ad unit on the AdManager in the open scene.
    /// Safe to run again. Editor play mode keeps using the test ad and test store either way.
    /// </summary>
    public static class SdkSetup
    {
        const string Defines = "IDLEMINE_ADMOB;IDLEMINE_UNITY_IAP";
        const string IosAppId = "ca-app-pub-3985317207511475~4199898139";
        const string IosRewardedUnit = "ca-app-pub-3985317207511475/1146445109";
        const string GoogleTestAppIdPrefix = "ca-app-pub-3940256099942544~";
        const string TrackingText = "Your data is used to show you ads that are more relevant to you.";

        [MenuItem("Idle Mine/Monetization/Set Up iOS SDKs")]
        public static void SetUpIos()
        {
            var report = new System.Text.StringBuilder();

            var target = NamedBuildTarget.iOS;
            var current = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var d in Defines.Split(';'))
                if (!current.Contains(d)) current.Add(d);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", current));
            report.AppendLine("iOS scripting defines: " + string.Join(";", current));

            // The settings class is internal to Google's editor assembly, so it's reached by reflection.
            var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            var load = type != null ? type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
            var settings = load != null ? load.Invoke(null, null) as ScriptableObject : null;
            if (settings == null)
            {
                report.AppendLine("Google Mobile Ads isn't installed yet: let Unity finish importing packages, then run this again.");
            }
            else
            {
                var so = new SerializedObject(settings);
                var idProp = so.FindProperty("adMobIOSAppId");
                if (idProp != null && (string.IsNullOrEmpty(idProp.stringValue) || idProp.stringValue.StartsWith(GoogleTestAppIdPrefix)))
                    idProp.stringValue = IosAppId;
                string appId = idProp != null ? idProp.stringValue : "(field 'adMobIOSAppId' not found in this plugin version)";
                string tracking = FillIfEmpty(so, "userTrackingUsageDescription", TrackingText);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                report.AppendLine("AdMob iOS app id: " + appId);
                report.AppendLine("Tracking prompt: " + tracking);
            }

            var ads = UnityEngine.Object.FindObjectOfType<AdManager>(true);
            if (ads == null)
            {
                report.AppendLine("No AdManager in the open scene: open Main.unity and run this again.");
            }
            else
            {
                var so = new SerializedObject(ads);
                so.FindProperty("iosRewardedId").stringValue = IosRewardedUnit;
                var android = so.FindProperty("androidRewardedId");
                if (android.stringValue.StartsWith("ca-app-pub-3940256099942544/")) android.stringValue = ""; // old test default
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(ads.gameObject.scene);
                report.AppendLine("AdManager iOS ad unit: " + IosRewardedUnit + " (release builds; dev builds use Google's test unit). Save the scene.");
            }

            string msg = report.ToString().TrimEnd();
            Debug.Log("[IdleMine] " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Idle Mine: iOS SDKs", msg, "OK");
        }

        static string FillIfEmpty(SerializedObject so, string field, string value)
        {
            var p = so.FindProperty(field);
            if (p == null) return "(field '" + field + "' not found in this plugin version)";
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = value;
            return p.stringValue;
        }
    }
}
