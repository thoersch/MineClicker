#if IDLEMINE_ADMOB
using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Google AdMob rewarded video. Compiled only when the IDLEMINE_ADMOB scripting define is set and the
    /// Google Mobile Ads Unity plugin is installed (built against 11.5.0). The AdMob *app* id goes in
    /// Assets > Google Mobile Ads > Settings; the rewarded *ad unit* ids go on AdManager.
    ///
    /// Startup: Google's consent form (GDPR, and Apple's ATT prompt if enabled in the AdMob console) is
    /// shown first when required, then the SDK starts and keeps one ad preloaded, retrying with backoff.
    /// SDK callbacks can arrive on a background thread, so each one is hopped onto Unity's main thread.
    /// </summary>
    public class AdMobAdService : IRewardedAdService
    {
        readonly string _unitId;
        readonly MonoBehaviour _host;
        RewardedAd _ad;
        bool _started, _loading, _showing;
        float _retryDelay = 2f;
        string _status = "Not started";

        public string Status { get { return _showing ? "Showing" : IsReady ? "Ready" : _status; } }

        public AdMobAdService(string unitId, MonoBehaviour host)
        {
            _unitId = unitId;
            _host = host;
        }

        public bool IsReady { get { return _ad != null && _ad.CanShowAd(); } }

        public void Initialize()
        {
            if (string.IsNullOrEmpty(_unitId))
            {
                Debug.LogWarning("[IdleMine] AdMob: no rewarded ad unit id set on AdManager for this platform.");
                _status = "No ad unit id set";
                return;
            }

            // Consent from a previous session lets ads start immediately while the update runs.
            _status = "Checking consent";
            if (ConsentInformation.CanRequestAds()) StartSdk();
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => OnMain(() =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("[IdleMine] Consent update failed: " + updateError.Message);
                    if (!_started) _status = "Consent check failed: " + updateError.Message;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) Debug.LogWarning("[IdleMine] Consent form failed: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) StartSdk();
                    else if (!_started) _status = formError != null ? "Consent form failed: " + formError.Message : "No consent to request ads";
                }));
            }));
        }

        void StartSdk()
        {
            if (_started) return;
            _started = true;
            _status = "Starting AdMob";
            MobileAds.Initialize(status => OnMain(Load));
        }

        void Load()
        {
            if (_loading || _ad != null) return;
            _loading = true;
            _status = "Loading ad";
            RewardedAd.Load(_unitId, new AdRequest(), (RewardedAd ad, LoadAdError error) => OnMain(() =>
            {
                _loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[IdleMine] AdMob load failed: " + (error != null ? error.GetMessage() : "no ad"));
                    _status = (error != null ? "Load failed (code " + error.GetCode() + "): " + error.GetMessage() : "No ad returned")
                              + ", retrying in " + Mathf.RoundToInt(_retryDelay) + "s";
                    _host.StartCoroutine(RetryLater());
                    return;
                }
                _ad = ad;
                _retryDelay = 2f;
            }));
        }

        IEnumerator RetryLater()
        {
            yield return new WaitForSecondsRealtime(_retryDelay);
            _retryDelay = Mathf.Min(_retryDelay * 2f, 120f);
            Load();
        }

        public void Show(AdPlacement placement, Action<AdResult> done)
        {
            if (!IsReady) { done(AdResult.Failed); return; }

            var ad = _ad;
            _ad = null;
            _showing = true;
            bool earned = false, failed = false, finished = false;

            Action finish = () =>
            {
                if (finished) return;
                finished = true;
                _showing = false;
                ad.Destroy();
                Load();
                done(earned ? AdResult.Rewarded : failed ? AdResult.Failed : AdResult.Skipped);
            };
            // Some platforms deliver the reward just after "closed", so give it a moment before deciding.
            ad.OnAdFullScreenContentClosed += () => OnMain(() => _host.StartCoroutine(After(0.3f, finish)));
            ad.OnAdFullScreenContentFailed += (AdError e) => OnMain(() =>
            {
                Debug.LogWarning("[IdleMine] AdMob show failed: " + e.GetMessage());
                failed = true;
                finish();
            });
            ad.Show(reward => OnMain(() => earned = true));
        }

        static IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        static void OnMain(Action action) { MobileAdsEventExecutor.ExecuteInUpdate(action); }
    }
}
#endif
