using System;

namespace IdleMine
{
    /// <summary>Every spot in the game that can show a rewarded ad. The name is passed to the ad network
    /// as the placement, so revenue can be broken down per spot in its dashboard.</summary>
    public enum AdPlacement { OfflineDouble, IncomeBoost, OreCart, MotherlodeChest, SkillAssist }

    public enum AdResult
    {
        Rewarded,   // watched to the end: grant the reward
        Skipped,    // closed early: no reward
        Failed,     // nothing loaded, or the network errored
    }

    /// <summary>
    /// A rewarded-video network. AdManager only talks to this, so swapping AdMob for LevelPlay, Unity Ads
    /// or AppLovin is one new class. Implementations must call back on the main thread, exactly once.
    /// </summary>
    public interface IRewardedAdService
    {
        /// <summary>Start the SDK (after consent) and begin loading the first ad.</summary>
        void Initialize();
        /// <summary>An ad is loaded and can be shown right now.</summary>
        bool IsReady { get; }
        void Show(AdPlacement placement, Action<AdResult> done);
    }

    /// <summary>Release builds with no ad SDK compiled in: no ads are ever offered.</summary>
    public class NullAdService : IRewardedAdService
    {
        public void Initialize() { }
        public bool IsReady { get { return false; } }
        public void Show(AdPlacement placement, Action<AdResult> done) { done(AdResult.Failed); }
    }

    /// <summary>One-time / consumable store purchases (Google Play, App Store).</summary>
    public interface IPurchaseService
    {
        void Initialize(string[] nonConsumableIds);
        bool IsReady { get; }
        /// <summary>Localised price ("$4.99", "4,99 €"), or null until the store has answered.</summary>
        string Price(string productId);
        /// <summary>Calls back true once the product is owned (new purchase or already owned).</summary>
        void Purchase(string productId, Action<bool> done);
        /// <summary>Apple requires a visible Restore button. Google Play restores automatically on launch.</summary>
        void Restore(Action<bool> done);
        /// <summary>Raised whenever the store confirms the player owns a product, including at startup and on restore.</summary>
        event Action<string> Owned;
    }

    /// <summary>Release builds with no IAP package compiled in: nothing can be bought.</summary>
    public class NullPurchaseService : IPurchaseService
    {
        public void Initialize(string[] nonConsumableIds) { }
        public bool IsReady { get { return false; } }
        public string Price(string productId) { return null; }
        public void Purchase(string productId, Action<bool> done) { done(false); }
        public void Restore(Action<bool> done) { done(false); }
        public event Action<string> Owned { add { } remove { } }
    }
}
