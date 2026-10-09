using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Every balance knob in one place. Create one via Assets > Create > Idle Mine > Game Config
    /// and drag it onto GameBootstrap, or leave the slot empty to run on these defaults.
    ///
    /// Defaults were tuned with a greedy-buyer simulation: ~15 upgrades in the first minute,
    /// ~100 by ten minutes, ~170 by the first hour, and a long tail past two days of idling.
    /// </summary>
    [CreateAssetMenu(menuName = "Idle Mine/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Starting state")]
        public double startingMoney = 0;
        public int startingMiners = 0;

        [Header("Mine layers  (layer i = depth i, starting at 0)")]
        [Tooltip("Ore that must be dug out of layer 0 before layer 1 opens.")]
        public double baseOreRequired = 30;
        [Tooltip("Each layer needs this many times more ore than the one above.")]
        public double oreRequiredGrowth = 1.5;
        [Tooltip("Cash per ore on layer 0.")]
        public double baseOreValue = 1;
        [Tooltip("Ore gets this many times more valuable per layer.")]
        public double oreValueGrowth = 1.3;
        [Tooltip("Rock gets this many times harder per layer (miners and taps dig slower).")]
        public double hardnessGrowth = 1.12;
        [Tooltip("Miner slots on every layer before upgrades.")]
        public int baseSlotsPerLayer = 3;
        [Tooltip("Output multiplier for a layer after it has been broken through. 1 = keeps producing forever, 0 = depleted.")]
        [Range(0f, 1f)] public float clearedLayerYield = 1f;
        [Tooltip("Every Nth layer is a Motherlode worth x3.")]
        public int motherlodeEvery = 10;
        [Tooltip("Seed for per-layer richness rolls.")]
        public int layerSeed = 1337;

        [Header("Miners & tapping")]
        [Tooltip("Ore per second from one miner on layer 0 before upgrades.")]
        public double baseMinerOrePerSecond = 1;
        [Tooltip("Ore per tap on layer 0 before upgrades.")]
        public double baseTapOre = 1;
        public double baseCritChance = 0.02;
        public double baseCritMultiplier = 5;

        [Header("Skill tree generation")]
        [Tooltip("Tiers along each of the 6 branches. 40 tiers is ~540 nodes.")]
        public int tiersPerBranch = 40;
        [Tooltip("Cost of the very first node, 'Add 1 Miner'.")]
        public double rootCost = 15;
        [Tooltip("Cost of a tier-1 node.")]
        public double nodeBaseCost = 25;
        [Tooltip("Each tier outward costs this many times more.")]
        public double nodeCostGrowth = 4.5;
        [Tooltip("Distance from the root to tier 1, in canvas units.")]
        public float treeInnerRadius = 320f;
        [Tooltip("Distance between tiers, in canvas units.")]
        public float treeTierSpacing = 170f;
        public int treeSeed = 1337;

        [Header("Deep Core (prestige tree past the skill tree)")]
        [Tooltip("Cost of 'Breach the Core'. Should sit just past the normal tree's last keystones.")]
        public double deepRootCost = 5e27;
        [Tooltip("Cost of a tier-1 Deep Core node.")]
        public double deepBaseCost = 1e28;
        [Tooltip("Each Deep Core tier outward costs this many times more.")]
        public double deepCostGrowth = 3.2;
        [Tooltip("Seconds between dynamite blasts before upgrades.")]
        public float dynamiteCooldownSeconds = 20f;
        [Tooltip("Seconds to charge a fuse fully before Fuse Speed upgrades.")]
        public float dynamiteChargeSeconds = 1.5f;
        [Tooltip("Hold this long before a press becomes a fuse instead of a tap.")]
        public float dynamiteHoldDelay = 0.3f;
        [Tooltip("Each layer further from the blast gets this fraction of the one before.")]
        [Range(0f, 1f)] public float dynamiteFalloff = 0.6f;
        public float gemLifetimeSeconds = 8f;
        [Tooltip("Seconds of play to fill the overclock meter before Charge Speed upgrades.")]
        public float overclockChargeSeconds = 120f;
        public float swingLifetimeSeconds = 7f;
        [Tooltip("Marker sweeps across the power swing bar this many times per second.")]
        public float swingSpeed = 0.9f;
        [Tooltip("Seconds after a power swing appears before taps count, so a tap meant for a layer isn't an instant miss.")]
        public float swingArmSeconds = 0.6f;

        [Header("Offline progress")]
        [Range(0f, 1f)] public float baseOfflineEfficiency = 0.25f;
        public float baseOfflineCapHours = 2f;
        [Tooltip("Ignore absences shorter than this (seconds).")]
        public float minOfflineSeconds = 30f;

        [Header("Paragon")]
        [Tooltip("Lifetime earnings needed to reach Paragon level 1.")]
        public double paragonBaseRequirement = 1000000;
        [Tooltip("Each Paragon level needs this many times more lifetime earnings than the last.")]
        public double paragonRequirementGrowth = 8;
        [Tooltip("Permanent multiplier granted per Paragon level, applied to Ore Value, Miner Speed, Tap Power " +
                 "and Dig Speed. Compounds: level 2 is this squared, level 3 cubed, etc.")]
        public double paragonMultiplierPerLevel = 0.25;

        [Header("Ads: global rules")]
        [Tooltip("Master switch. Off hides every ad offer (Foreman Pass rewards still work).")]
        public bool adsEnabled = true;
        [Tooltip("Minutes of play before any ad is offered, so new players get hooked on the game first.")]
        public float adGraceMinutes = 6f;
        [Tooltip("Most rewarded ads one player can watch per day, across all placements.")]
        public int adDailyCap = 25;
        [Tooltip("Ad rewards are 'N minutes of income'. Before the mine earns anything, income counts as this many taps per second.")]
        public double rewardFloorTapsPerSecond = 2;

        [Header("Ads: income boost")]
        [Tooltip("Cash multiplier while a boost is running.")]
        public double boostMultiplier = 2;
        public float boostMinutesPerAd = 30f;
        [Tooltip("Boosts stack up to this much time remaining.")]
        public float boostMaxHours = 4f;

        [Header("Ads: ore cart (random visitor)")]
        public float cartMinIntervalSeconds = 240f;
        public float cartMaxIntervalSeconds = 480f;
        [Tooltip("Seconds the cart takes to roll across the screen. Miss it and it's gone.")]
        public float cartLifetimeSeconds = 2f;
        [Tooltip("Minutes of income for just tapping the cart.")]
        public float cartFreeMinutes = 2f;
        [Tooltip("Minutes of income for watching an ad instead.")]
        public float cartAdMinutes = 15f;

        [Header("Ads: motherlode chest")]
        [Tooltip("Minutes of income from the chest that appears when you reach a Motherlode.")]
        public float chestAdMinutes = 30f;
        public float chestLifetimeSeconds = 60f;

        [Header("Ads: skill assist")]
        [Tooltip("Offer 'watch an ad to buy now' once you have at least this fraction of a skill's cost.")]
        [Range(0f, 1f)] public float skillAssistThreshold = 0.7f;
        public float skillAssistCooldownMinutes = 5f;

        [Header("Foreman Pass (one-time purchase)")]
        [Tooltip("Store product id. Must match the id set up in Google Play Console / App Store Connect.")]
        public string foremanPassProductId = "foreman_pass_iap";
        [Tooltip("Extra offline earnings while the pass is owned (+50% = 0.5).")]
        public double foremanOfflineBonus = 0.5;

        [Header("Saving")]
        public float autosaveSeconds = 10f;
    }
}
