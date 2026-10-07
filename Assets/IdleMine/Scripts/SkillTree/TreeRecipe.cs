using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Everything that makes a generated tree *this* tree: branch names and colours, what each node does,
    /// names, costs and an id prefix (so two trees can share one save list). SkillTreeGenerator supplies the
    /// shared radial layout.
    /// </summary>
    public abstract class TreeRecipe
    {
        public abstract string IdPrefix { get; }
        public abstract string[] BranchNames { get; }
        public abstract string[] BranchColors { get; }
        public abstract string[][] KeystoneNames { get; }
        public virtual string KeystoneFlavor { get { return "Keystone. A permanent leap for the whole operation."; } }

        public abstract SkillNode MakeRoot(GameConfig cfg);
        public abstract IEnumerable<SkillEffect> SpineEffects(int b, int t);
        public abstract IEnumerable<SkillEffect> SideEffects(int b, int t);
        public abstract IEnumerable<SkillEffect> KeystoneEffects(int b, int t);
        public abstract string SpineName(int b, int t);
        public abstract string SideName(int b, int t);
        public virtual string SpineFlavor(int b, int t) { return null; }
        /// <summary>Full name of a non-keystone spine node. Defaults to the name plus the tier in Roman numerals.</summary>
        public virtual string SpineTitle(int b, int t) { return SpineName(b, t) + " " + NumberFormat.Roman(t); }
        public abstract double Cost(GameConfig cfg, double tier, double mult);
        public virtual int Seed(GameConfig cfg) { return cfg.treeSeed; }
    }

    /// <summary>The normal skill tree: six branches around "Add 1 Miner". Node ids have no prefix (saves predate recipes).</summary>
    public class MineTreeRecipe : TreeRecipe
    {
        static readonly string[] Names = { "Workforce", "Pickaxes", "Prospecting", "Tapping", "Excavation", "Logistics" };
        static readonly string[] Colors = { "F2A33A", "7FB2E5", "F5D547", "F06292", "8BC34A", "B39DDB" };

        static readonly string[][] Keystones =
        {
            new[] { "Miners' Guild", "Company Town", "Labor Union", "Boomtown", "Deep Crews", "Legion of Picks", "Tunnel Nation", "Endless Shifts" },
            new[] { "Steam Drills", "Diamond Bits", "Pneumatic Hammers", "Laser Cutters", "Plasma Bores", "Quantum Picks", "Graviton Drills", "Singularity Drill" },
            new[] { "Gold Rush", "Ore Refinery", "Smelting Works", "Gem Cutters", "Commodity Exchange", "Cornered Market", "Precious Futures", "Philosopher's Stone" },
            new[] { "Iron Grip", "Sledgehammer", "Seismic Tap", "Earthshaker", "Tectonic Fist", "Planet Cracker", "Core Breaker", "Worldsplitter" },
            new[] { "Dynamite", "Tunnel Borer", "Hydraulic Rams", "Nitro Charges", "Thermal Lance", "Mole Machines", "Bunker Busters", "Drill of Ages" },
            new[] { "Rail Network", "Ore Elevator", "Automated Carts", "Logistics Hub", "Maglev Lines", "Teleport Pads", "Supply Singularity", "Omnilogistics" },
        };

        public override string IdPrefix { get { return ""; } }
        public override string[] BranchNames { get { return Names; } }
        public override string[] BranchColors { get { return Colors; } }
        public override string[][] KeystoneNames { get { return Keystones; } }

        public override SkillNode MakeRoot(GameConfig cfg)
        {
            var root = new SkillNode
            {
                Id = "root",
                Name = "Add 1 Miner",
                Flavor = "Every empire starts with one pickaxe.",
                Kind = NodeKind.Root,
                Tier = 0,
                Position = Vector2.zero,
                BaseCost = cfg.rootCost,
                Glyph = "+1",
            };
            root.Effects.Add(new SkillEffect(StatType.MinerCount, ModOp.Flat, 1));
            return root;
        }

        public override double Cost(GameConfig cfg, double tier, double mult)
        {
            return Math.Round(cfg.nodeBaseCost * Math.Pow(cfg.nodeCostGrowth, tier - 1) * mult);
        }


        // The branch's "signature" upgrade, used for spine nodes and bridges.
        public override IEnumerable<SkillEffect> SpineEffects(int b, int t)
        {
            switch (b)
            {
                case 0: yield return new SkillEffect(StatType.MinerCount, ModOp.Flat, 1 + t / 6); break;
                case 1: yield return new SkillEffect(StatType.MinerSpeed, ModOp.Percent, 0.10 + 0.02 * t); break;
                case 2: yield return new SkillEffect(StatType.OreValue, ModOp.Percent, 0.10 + 0.02 * t); break;
                case 3: yield return new SkillEffect(StatType.TapPower, ModOp.Percent, 0.5 + 0.1 * t); break;
                case 4: yield return new SkillEffect(StatType.DigSpeed, ModOp.Percent, 0.15 + 0.03 * t); break;
                default:
                    switch (t % 3)
                    {
                        case 1: yield return new SkillEffect(StatType.OfflineEfficiency, ModOp.Flat, 0.05); break;
                        case 2: yield return new SkillEffect(StatType.OfflineCapHours, ModOp.Flat, 1); break;
                        default:
                            yield return new SkillEffect(StatType.MinerSpeed, ModOp.Percent, 0.05);
                            yield return new SkillEffect(StatType.OreValue, ModOp.Percent, 0.05);
                            break;
                    }
                    break;
            }
        }

        public override IEnumerable<SkillEffect> SideEffects(int b, int t)
        {
            switch (b)
            {
                case 0:
                    if (t % 3 == 0) yield return new SkillEffect(StatType.LayerSlots, ModOp.Flat, 1);
                    else if (t % 3 == 1) yield return new SkillEffect(StatType.MinerCount, ModOp.Flat, 1 + t / 5);
                    else yield return new SkillEffect(StatType.MinerSpeed, ModOp.Percent, 0.05);
                    break;
                case 1:
                    if (t % 2 == 1) yield return new SkillEffect(StatType.MinerSpeed, ModOp.Percent, 0.05 + 0.01 * t);
                    else yield return new SkillEffect(StatType.TapPower, ModOp.Percent, 0.25 + 0.05 * t);
                    break;
                case 2:
                    if (t % 2 == 1) yield return new SkillEffect(StatType.DepthBonus, ModOp.Flat, 0.01);
                    else yield return new SkillEffect(StatType.OreValue, ModOp.Percent, 0.05 + 0.01 * t);
                    break;
                case 3:
                    if (t % 3 == 0) yield return new SkillEffect(StatType.CritChance, ModOp.Flat, 0.02);
                    else if (t % 3 == 1) yield return new SkillEffect(StatType.CritMultiplier, ModOp.Flat, 1);
                    else yield return new SkillEffect(StatType.AutoTapRate, ModOp.Flat, 0.25);
                    break;
                case 4:
                    if (t % 2 == 1) yield return new SkillEffect(StatType.DigSpeed, ModOp.Percent, 0.08 + 0.015 * t);
                    else yield return new SkillEffect(StatType.LayerSlots, ModOp.Flat, 1);
                    break;
                default:
                    if (t % 2 == 1) yield return new SkillEffect(StatType.SkillDiscount, ModOp.Flat, 0.01);
                    else yield return new SkillEffect(StatType.OreValue, ModOp.Percent, 0.05 + 0.01 * t);
                    break;
            }
        }

        public override IEnumerable<SkillEffect> KeystoneEffects(int b, int t)
        {
            switch (b)
            {
                case 0:
                    yield return new SkillEffect(StatType.MinerCount, ModOp.Flat, t / 2);
                    yield return new SkillEffect(StatType.LayerSlots, ModOp.Flat, 1);
                    break;
                case 1: yield return new SkillEffect(StatType.MinerSpeed, ModOp.Multiply, 2); break;
                case 2: yield return new SkillEffect(StatType.OreValue, ModOp.Multiply, 2); break;
                case 3:
                    yield return new SkillEffect(StatType.TapPower, ModOp.Multiply, 2);
                    yield return new SkillEffect(StatType.AutoTapRate, ModOp.Flat, 0.25);
                    break;
                case 4: yield return new SkillEffect(StatType.DigSpeed, ModOp.Multiply, 2); break;
                default:
                    yield return new SkillEffect(StatType.MinerSpeed, ModOp.Multiply, 1.5);
                    yield return new SkillEffect(StatType.OreValue, ModOp.Multiply, 1.5);
                    break;
            }
        }

        public override string SpineName(int b, int t)
        {
            switch (b)
            {
                case 0: return "Recruitment";
                case 1: return "Sharpened Picks";
                case 2: return "Assay Office";
                case 3: return "Strong Arms";
                case 4: return "Blasting Charges";
                default: return t % 3 == 1 ? "Night Watch" : (t % 3 == 2 ? "Long Haul" : "Mine Carts");
            }
        }

        public override string SideName(int b, int t)
        {
            switch (b)
            {
                case 0: return t % 3 == 0 ? "Wider Tunnels" : (t % 3 == 1 ? "Night Shift" : "Hot Meals");
                case 1: return t % 2 == 1 ? "Tempered Steel" : "Heavy Heads";
                case 2: return t % 2 == 1 ? "Geologist" : "Polished Nuggets";
                case 3: return t % 3 == 0 ? "Lucky Strike" : (t % 3 == 1 ? "Critical Swing" : "Tap Assistant");
                case 4: return t % 2 == 1 ? "Shoring" : "Side Shafts";
                default: return t % 2 == 1 ? "Bulk Deals" : "Trade Routes";
            }
        }
    }
}
