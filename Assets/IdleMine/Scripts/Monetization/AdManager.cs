using System;
using System.Collections;
using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// The one place that decides whether an ad may be offered, shows it, and enforces the rules that keep
    /// ads friendly: rewarded only (never forced), a grace period for new players, a daily cap, per-placement
    /// cooldowns, and no button at all when no ad is loaded. Foreman Pass owners skip the video and get the
    /// reward straight away.
    ///
    /// Views ask CanOffer(placement) to decide whether to show an ad button, then call ShowRewarded. Which
    /// network runs is picked in CreateAdService: the test ad in the editor and dev builds, the real SDK in
    /// release builds once its scripting define is set (see the SDK section of the README).
    /// </summary>
    [DefaultExecutionOrder(-90)] // after GameManager, before the views
    public class AdManager : MonoBehaviour
    {
        [SerializeField] GameManager game;

        // Some of these are only read in certain builds (AdMob ids with IDLEMINE_ADMOB, the mock settings in the editor).
#pragma warning disable 0414
        [Header("AdMob rewarded ad unit ids (release builds with IDLEMINE_ADMOB)")]
        [Tooltip("Your real ad unit. Development builds always use Google's test unit instead: AdMob can ban accounts for views or taps on real ads during testing.")]
        [SerializeField] string androidRewardedId = "";
        [Tooltip("Your real ad unit. Development builds always use Google's test unit instead: AdMob can ban accounts for views or taps on real ads during testing.")]
        [SerializeField] string iosRewardedId = "ca-app-pub-3985317207511475/1146445109";

        [Header("Editor & development builds")]
        [Tooltip("Length of the fake test ad.")]
        [SerializeField] float mockAdSeconds = 3f;
        [Tooltip("Seconds the fake network takes to 'load' the next ad.")]
        [SerializeField] float mockLoadSeconds = 1f;
        [Tooltip("Chance the fake network has an ad. Lower it to test the no-fill path.")]
        [Range(0f, 1f)] [SerializeField] float mockFillRate = 1f;
        [Tooltip("Skip the new-player grace period and daily cap in the editor so ads can be tested right away.")]
        [SerializeField] bool ignoreGraceAndCapInEditor = true;
#pragma warning restore 0414

        public static AdManager Instance { get; private set; }

        public IPurchaseService Store { get; private set; }
        public bool IsShowing { get; private set; }
        public bool HasForemanPass { get { return game.ForemanPass; } }

        /// <summary>A reward was granted (ad watched, or skipped by the Foreman Pass). For analytics.</summary>
        public event Action<AdPlacement> Rewarded;

        IRewardedAdService _ads;
        float _saveTimer;

        void Awake()
        {
            Instance = this;
            _ads = CreateAdService();
            Store = CreatePurchaseService();
            Store.Owned += OnOwned;
        }

        void Start()
        {
            // Real SDKs ask for GDPR / ATT consent inside Initialize before requesting the first ad.
            _ads.Initialize();
            Store.Initialize(new[] { game.Config.foremanPassProductId });
        }

        IRewardedAdService CreateAdService()
        {
#if IDLEMINE_ADMOB && !UNITY_EDITOR
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            // Google's public test units: always fill, never pay, and safe to tap while testing.
            string unit = Debug.isDebugBuild
                ? (ios ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917")
                : (ios ? iosRewardedId : androidRewardedId);
            return new AdMobAdService(unit, this);
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return new MockAdService(mockAdSeconds, mockLoadSeconds, mockFillRate);
#else
            return new NullAdService();
#endif
        }

        IPurchaseService CreatePurchaseService()
        {
#if IDLEMINE_UNITY_IAP && !UNITY_EDITOR
            return new UnityIapPurchaseService();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return new MockPurchaseService();
#else
            return new NullPurchaseService();
#endif
        }

        void Update()
        {
            MonetizationStore.Data.playSeconds += Time.unscaledDeltaTime;
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer >= 10f) { _saveTimer = 0; MonetizationStore.Save(); }
        }

        void OnApplicationPause(bool paused) { if (paused) MonetizationStore.Save(); }
        void OnApplicationQuit() { MonetizationStore.Save(); }

        // ================================================================== rules

        /// <summary>Should this placement show its ad button right now? (Cooldowns and caps passed, ad loaded.)</summary>
        public bool CanOffer(AdPlacement p)
        {
            if (IsShowing) return false;
            if (CooldownLeft(p) > 0) return false;
            if (HasForemanPass) return true;
            var cfg = game.Config;
            if (!cfg.adsEnabled || !_ads.IsReady) return false;
            if (IgnoreLimits) return true;
            if (MonetizationStore.Data.playSeconds < cfg.adGraceMinutes * 60f) return false;
            return AdsToday < cfg.adDailyCap;
        }

        public double CooldownLeft(AdPlacement p)
        {
            long until = MonetizationStore.CooldownUntil(p.ToString());
            return Math.Max(0, (until - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond);
        }

        public void StartCooldown(AdPlacement p, double seconds)
        {
            MonetizationStore.SetCooldownUntil(p.ToString(), DateTime.UtcNow.Ticks + (long)(seconds * TimeSpan.TicksPerSecond));
        }

        int AdsToday { get { RollDay(); return MonetizationStore.Data.adsToday; } }

        static void RollDay()
        {
            var d = MonetizationStore.Data;
            if (d.adDay != Today) { d.adDay = Today; d.adsToday = 0; }
        }

        static string Today { get { return DateTime.Now.ToString("yyyy-MM-dd"); } }

        bool IgnoreLimits
        {
            get
            {
#if UNITY_EDITOR
                return ignoreGraceAndCapInEditor;
#else
                return false;
#endif
            }
        }

        /// <summary>Why ads are or aren't being offered right now, for the readout in Settings.</summary>
        public string StatusText
        {
            get
            {
                var cfg = game.Config;
                if (HasForemanPass) return "Foreman Pass: rewards are instant";
                if (!cfg.adsEnabled) return "Ads turned off in GameConfig";
                string net = _ads.Status;
                if (!IgnoreLimits)
                {
                    double grace = cfg.adGraceMinutes * 60.0 - MonetizationStore.Data.playSeconds;
                    if (grace > 0) return "Unlock after " + NumberFormat.Time(grace) + " more play (" + net + ")";
                    if (AdsToday >= cfg.adDailyCap) return "Daily limit reached (" + cfg.adDailyCap + ")";
                }
                return net;
            }
        }

        // ================================================================== showing

        /// <summary>Shows a rewarded ad (or skips it for pass owners) and calls back true if the reward should
        /// be granted. Grant the reward inside the callback; it never fires twice.</summary>
        public void ShowRewarded(AdPlacement p, Action<bool> done)
        {
            if (!CanOffer(p)) { done(false); return; }
            if (HasForemanPass) { Grant(p, done); return; }

            IsShowing = true;
            game.SuppressOfflineProgress = true;
            AudioListener.pause = true;
            _ads.Show(p, result =>
            {
                AudioListener.pause = false;
                IsShowing = false;
                // The app's resume event can arrive just after the ad's close callback, so keep offline
                // progress suppressed for a moment longer.
                StartCoroutine(ReleaseOfflineSuppression());

                if (result == AdResult.Rewarded)
                {
                    RollDay();
                    MonetizationStore.Data.adsToday++;
                    MonetizationStore.Data.lifetimeAds++;
                    Grant(p, done);
                }
                else
                {
                    Debug.Log("[IdleMine] Ad " + p + ": " + result);
                    done(false);
                }
            });
        }

        void Grant(AdPlacement p, Action<bool> done)
        {
            MonetizationStore.Save();
            if (Rewarded != null) Rewarded(p);
            done(true);
        }

        IEnumerator ReleaseOfflineSuppression()
        {
            yield return new WaitForSecondsRealtime(1f);
            if (!IsShowing) game.SuppressOfflineProgress = false;
        }

        // ================================================================== Foreman Pass

        public string ForemanPassPrice { get { return Store.Price(game.Config.foremanPassProductId); } }

        public void BuyForemanPass(Action<bool> done)
        {
            Store.Purchase(game.Config.foremanPassProductId, done);
        }

        public void RestorePurchases(Action<bool> done) { Store.Restore(done); }

        void OnOwned(string productId)
        {
            if (productId != game.Config.foremanPassProductId) return;
            MonetizationStore.Data.foremanPass = true;
            MonetizationStore.Save();
            game.SetForemanPass(true);
        }

        // ================================================================== debug helpers (right-click the component)

        [ContextMenu("Debug/Toggle Foreman Pass")]
        void DebugTogglePass()
        {
            bool owned = !game.ForemanPass;
            MonetizationStore.Data.foremanPass = owned;
            MonetizationStore.Save();
            game.SetForemanPass(owned);
            Debug.Log("[IdleMine] Foreman Pass " + (owned ? "on" : "off"));
        }

        [ContextMenu("Debug/Reset ad caps and cooldowns")]
        void DebugResetCaps()
        {
            var d = MonetizationStore.Data;
            d.adsToday = 0;
            d.cooldownKeys.Clear();
            d.cooldownUntilTicks.Clear();
            MonetizationStore.Save();
        }
    }
}
