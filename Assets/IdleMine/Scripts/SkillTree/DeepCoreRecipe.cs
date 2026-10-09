using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// The Deep Core: the prestige tree past the normal skill tree. Same radial layout, but every branch is built
    /// around a new mechanic. Each branch's first node switches its mechanic on; everything after it makes that
    /// mechanic stronger:
    ///   Demolition  - dynamite: hold a layer to charge, release to blast
    ///   Gemcraft    - glowing gem veins to tap for big payouts
    ///   Drillworks  - drill rigs that stack on the deepest layer, digging and speeding up breakthroughs
    ///   Overdrive   - an overclock meter that fills as you play, then a burst of extra production
    ///   Timing      - power swings: hit the gold zone at the right moment for a huge strike
    /// Dynamite and power swings pay in seconds of income, so they stay meaningful however rich the mine gets.
    ///   Core        - raw multipliers that keep cash climbing
    /// Costs pick up where the normal tree ends. Node ids are prefixed "d" so both trees share the save list.
    /// </summary>
    public class DeepCoreRecipe : TreeRecipe
    {
        static readonly string[] Names = { "Demolition", "Gemcraft", "Drillworks", "Overdrive", "Timing", "Core" };
        static readonly string[] Colors = { "FF7A3D", "4DE1E8", "B8C4CF", "FF4F8B", "FFD54F", "E8443A" };

        static readonly string[][] Keystones =
        {
            new[] { "Nitro Crates", "Shaped Charges", "Thermite", "Seismic Charges", "Mountain Breaker", "Quarry Storm", "Fault Line", "Big Bang" },
            new[] { "Gem Eye", "Crystal Garden", "Prism Heart", "Royal Cut", "Star Sapphire", "Hope Vault", "Living Geode", "Crown of Facets" },
            new[] { "Twin Rigs", "Diamond Bits", "Hydraulic Mast", "Tunnel Train", "Rig Swarm", "Mantle Bore", "Plasma Rigs", "World Drill" },
            new[] { "Redline", "Afterburner", "Turbo Core", "Flux Capacitor", "Hyperdrive", "Overload", "Meltdown", "Event Horizon" },
            new[] { "Perfect Form", "Muscle Memory", "Flow State", "Zen Strike", "Lightning Reflex", "Time Dilation", "Bullet Time", "One With Stone" },
            new[] { "Magma Heart", "Core Tap", "Mantle Market", "Molten Gold", "Planet Forge", "Star Metal", "Neutron Ore", "The Core" },
        };

        public override string IdPrefix { get { return "d"; } }
        public override string[] BranchNames { get { return Names; } }
        public override string[] BranchColors { get { return Colors; } }
        public override string[][] KeystoneNames { get { return Keystones; } }
        public override string KeystoneFlavor { get { return "Keystone. The Deep Core answers."; } }
        public override int Seed(GameConfig cfg) { return cfg.treeSeed * 31 + 7; }

        public override SkillNode MakeRoot(GameConfig cfg)
        {
            var root = new SkillNode
            {
                Id = "droot",
                Name = "Breach the Core",
                Flavor = "Below the last skill, the mountain glows. Needs this run's skill tree complete.",
                Kind = NodeKind.Root,
                Tier = 0,
                Position = Vector2.zero,
                BaseCost = cfg.deepRootCost,
                Glyph = "CORE",
            };
            root.Effects.Add(new SkillEffect(StatType.OreValue, ModOp.Multiply, 2));
            root.Effects.Add(new SkillEffect(StatType.MinerSpeed, ModOp.Multiply, 2));
            return root;
        }

        public override double Cost(GameConfig cfg, double tier, double mult)
        {
            return Math.Round(cfg.deepBaseCost * Math.Pow(cfg.deepCostGrowth, tier - 1) * mult);
        }

        static SkillEffect E(StatType s, ModOp op, double v) { return new SkillEffect(s, op, v); }

        public override string SpineFlavor(int b, int t)
        {
            if (t != 1) return null;
            switch (b)
            {
                case 0: return "Unlocks dynamite: hold a layer to light the fuse, release to blast.";
                case 1: return "Unlocks gem veins: glowing gems appear in the mine. Tap them before they fade.";
                case 2: return "Unlocks drill rigs: drills work the deepest layer, and each one makes it break through faster (x3 with one, x5 with two...).";
                case 3: return "Unlocks overclock: fill the meter, then tap for a burst of extra production.";
                case 4: return "Unlocks power swings: tap when the marker crosses the gold zone for a huge strike.";
                default: return null;
            }
        }

        public override IEnumerable<SkillEffect> SpineEffects(int b, int t)
        {
            switch (b)
            {
                case 0:
                    if (t == 1) { yield return E(StatType.DynamitePower, ModOp.Flat, 25); break; }
                    yield return E(StatType.DynamitePower, ModOp.Percent, 0.15 + 0.03 * t);
                    break;
                case 1:
                    if (t == 1) { yield return E(StatType.GemRate, ModOp.Flat, 1); yield return E(StatType.GemValue, ModOp.Flat, 3); break; }
                    yield return E(StatType.GemValue, ModOp.Percent, 0.10 + 0.02 * t);
                    break;
                case 2:
                    if (t == 1) { yield return E(StatType.DrillCount, ModOp.Flat, 1); yield return E(StatType.DrillPower, ModOp.Flat, 5); yield return E(StatType.DrillBore, ModOp.Flat, 2); break; }
                    if (t % 3 == 0) yield return E(StatType.DrillBore, ModOp.Percent, 0.15);
                    else yield return E(StatType.DrillPower, ModOp.Percent, 0.10 + 0.03 * t);
                    break;
                case 3:
                    if (t == 1) { yield return E(StatType.OverclockPower, ModOp.Flat, 1); yield return E(StatType.OverclockDuration, ModOp.Flat, 10); break; }
                    if (t % 2 == 0) yield return E(StatType.OverclockPower, ModOp.Flat, 0.1);
                    else yield return E(StatType.OverclockDuration, ModOp.Flat, 1);
                    break;
                case 4:
                    if (t == 1) { yield return E(StatType.SwingRate, ModOp.Flat, 1); yield return E(StatType.SwingPower, ModOp.Flat, 60); yield return E(StatType.SwingWindow, ModOp.Flat, 0.1); break; }
                    yield return E(StatType.SwingPower, ModOp.Percent, 0.15 + 0.03 * t);
                    break;
                default:
                    yield return E(StatType.OreValue, ModOp.Percent, 0.20 + 0.04 * t);
                    break;
            }
        }

        public override IEnumerable<SkillEffect> SideEffects(int b, int t)
        {
            switch (b)
            {
                case 0:
                    if (t % 6 == 3) yield return E(StatType.DynamiteRadius, ModOp.Flat, 1);
                    else if (t % 2 == 0) yield return E(StatType.DynamiteCharge, ModOp.Percent, 0.10);
                    else yield return E(StatType.DynamiteCooldownCut, ModOp.Flat, 0.03);
                    break;
                case 1:
                    if (t % 2 == 0) yield return E(StatType.GemRate, ModOp.Percent, 0.15);
                    else yield return E(StatType.GemValue, ModOp.Flat, 1);
                    break;
                case 2:
                    if (t % 4 == 0) yield return E(StatType.DrillCount, ModOp.Flat, 1);
                    else if (t % 4 == 2) yield return E(StatType.DrillBore, ModOp.Flat, 0.25);
                    else yield return E(StatType.DrillPower, ModOp.Percent, 0.10 + 0.02 * t);
                    break;
                case 3:
                    if (t % 2 == 0) yield return E(StatType.OverclockCharge, ModOp.Percent, 0.10);
                    else yield return E(StatType.OverclockDuration, ModOp.Flat, 2);
                    break;
                case 4:
                    if (t % 2 == 0) yield return E(StatType.SwingRate, ModOp.Percent, 0.15);
                    else yield return E(StatType.SwingWindow, ModOp.Flat, 0.01);
                    break;
                default:
                    if (t % 3 == 0) yield return E(StatType.MinerCount, ModOp.Flat, 2);
                    else if (t % 3 == 1) yield return E(StatType.MinerSpeed, ModOp.Percent, 0.10 + 0.02 * t);
                    else yield return E(StatType.TapPower, ModOp.Percent, 1.0 + 0.2 * t);
                    break;
            }
        }

        public override IEnumerable<SkillEffect> KeystoneEffects(int b, int t)
        {
            bool alt = (t / 5) % 2 == 0;
            switch (b)
            {
                case 0:
                    yield return E(StatType.DynamitePower, ModOp.Multiply, 2);
                    if (alt) yield return E(StatType.DynamiteRadius, ModOp.Flat, 1);
                    break;
                case 1:
                    yield return E(StatType.GemValue, ModOp.Multiply, 2);
                    if (alt) yield return E(StatType.GemRate, ModOp.Percent, 0.5);
                    break;
                case 2:
                    yield return E(StatType.DrillCount, ModOp.Flat, 1);
                    yield return E(StatType.DrillPower, ModOp.Multiply, 2);
                    if (alt) yield return E(StatType.DrillBore, ModOp.Multiply, 1.5);
                    break;
                case 3:
                    yield return E(StatType.OverclockPower, ModOp.Multiply, 1.5);
                    yield return E(StatType.OverclockCharge, ModOp.Percent, 0.25);
                    break;
                case 4:
                    yield return E(StatType.SwingPower, ModOp.Multiply, 2);
                    if (alt) yield return E(StatType.SwingWindow, ModOp.Flat, 0.02);
                    break;
                default:
                    yield return E(alt ? StatType.OreValue : StatType.MinerSpeed, ModOp.Multiply, 3);
                    break;
            }
        }

        // A branch's first node is its mechanic's name ("Light the Fuse"), without a tier numeral.
        public override string SpineTitle(int b, int t)
        {
            return t == 1 && b < 5 ? SpineName(b, t) : base.SpineTitle(b, t);
        }

        public override string SpineName(int b, int t)
        {
            if (t == 1)
            {
                switch (b)
                {
                    case 0: return "Light the Fuse";
                    case 1: return "Gem Sense";
                    case 2: return "First Drill Rig";
                    case 3: return "Overclock";
                    case 4: return "Power Swing";
                }
            }
            switch (b)
            {
                case 0: return "Bigger Bang";
                case 1: return "Brilliant Cut";
                case 2: return "Torque";
                case 3: return t % 2 == 0 ? "Overvolt" : "Long Burn";
                case 4: return "Heavy Swing";
                default: return "Molten Ore";
            }
        }

        public override string SideName(int b, int t)
        {
            switch (b)
            {
                case 0: return t % 6 == 3 ? "Wide Blast" : (t % 2 == 0 ? "Quick Fuse" : "Spare Crates");
                case 1: return t % 2 == 0 ? "Vein Finder" : "Polished Facets";
                case 2: return t % 4 == 0 ? "Extra Rig" : "Hardened Bits";
                case 3: return t % 2 == 0 ? "Capacitors" : "Heat Sinks";
                case 4: return t % 2 == 0 ? "Keen Eye" : "Steady Hands";
                default: return t % 3 == 0 ? "Core Crew" : (t % 3 == 1 ? "Magma Tools" : "Iron Fists");
            }
        }
    }
}
