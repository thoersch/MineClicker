using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Builds the massive radial tree procedurally so you get hundreds of nodes without hand-authoring.
    ///
    /// Layout: "Add 1 Miner" sits in the middle. Six branches radiate out, one per theme.
    ///   * Spine nodes run straight out along each branch, one per tier.
    ///   * Every 5th spine node is a Keystone: a big multiplicative jump (x2, x1.5 ...).
    ///   * Side nodes hang off the spine between tiers; some get a second "Master" node.
    ///   * Bridge nodes sit between neighbouring branches and need BOTH spines, rewarding breadth.
    ///
    /// Node ids are derived from branch/tier (not from random order), so saves stay valid if you
    /// tweak tier counts. Every random choice uses Hash.Hash01 for the same reason.
    ///
    /// To hand-author content instead, build your own List&lt;SkillNode&gt; and pass it to new SkillTree(...).
    /// </summary>
    public static class SkillTreeGenerator
    {
        public const int BranchCount = 6;

        static readonly string[] BranchNames = { "Workforce", "Pickaxes", "Prospecting", "Tapping", "Excavation", "Logistics" };
        static readonly string[] BranchColors = { "F2A33A", "7FB2E5", "F5D547", "F06292", "8BC34A", "B39DDB" };

        static readonly string[][] KeystoneNames =
        {
            new[] { "Miners' Guild", "Company Town", "Labor Union", "Boomtown", "Deep Crews", "Legion of Picks", "Tunnel Nation", "Endless Shifts" },
            new[] { "Steam Drills", "Diamond Bits", "Pneumatic Hammers", "Laser Cutters", "Plasma Bores", "Quantum Picks", "Graviton Drills", "Singularity Drill" },
            new[] { "Gold Rush", "Ore Refinery", "Smelting Works", "Gem Cutters", "Commodity Exchange", "Cornered Market", "Precious Futures", "Philosopher's Stone" },
            new[] { "Iron Grip", "Sledgehammer", "Seismic Tap", "Earthshaker", "Tectonic Fist", "Planet Cracker", "Core Breaker", "Worldsplitter" },
            new[] { "Dynamite", "Tunnel Borer", "Hydraulic Rams", "Nitro Charges", "Thermal Lance", "Mole Machines", "Bunker Busters", "Drill of Ages" },
            new[] { "Rail Network", "Ore Elevator", "Automated Carts", "Logistics Hub", "Maglev Lines", "Teleport Pads", "Supply Singularity", "Omnilogistics" },
        };

        public static SkillTree Generate(GameConfig cfg)
        {
            var nodes = new List<SkillNode>();
            var byId = new Dictionary<string, SkillNode>();
            var branches = new List<BranchInfo>();

            float gap = Mathf.PI * 2f / BranchCount;
            for (int b = 0; b < BranchCount; b++)
            {
                Color c;
                ColorUtility.TryParseHtmlString("#" + BranchColors[b], out c);
                branches.Add(new BranchInfo { Name = BranchNames[b], Color = c, Angle = Mathf.PI / 2f - b * gap });
            }

            // ---- Root: the node that starts the whole game loop ----
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
            Add(nodes, byId, root);

            int T = Mathf.Max(1, cfg.tiersPerBranch);
            float r0 = cfg.treeInnerRadius, step = cfg.treeTierSpacing;

            for (int b = 0; b < BranchCount; b++)
            {
                float angle = branches[b].Angle;
                for (int t = 1; t <= T; t++)
                {
                    bool keystone = t % 5 == 0;
                    float radius = r0 + (t - 1) * step;

                    // Spine
                    var spine = new SkillNode
                    {
                        Id = "b" + b + "_s" + t,
                        Kind = keystone ? NodeKind.Keystone : NodeKind.Spine,
                        Branch = b,
                        Tier = t,
                        Position = Polar(radius, angle),
                        BaseCost = Cost(cfg, t, keystone ? 3.0 : 1.0),
                    };
                    spine.Effects.AddRange(keystone ? KeystoneEffects(b, t) : SpineEffects(b, t));
                    if (keystone)
                    {
                        int k = t / 5 - 1;
                        spine.Name = KeystoneNames[b][k % KeystoneNames[b].Length] + (k >= KeystoneNames[b].Length ? " " + NumberFormat.Roman(k / KeystoneNames[b].Length + 1) : "");
                        spine.Flavor = "Keystone. A permanent leap for the whole operation.";
                    }
                    else
                    {
                        spine.Name = SpineName(b, t) + " " + NumberFormat.Roman(t);
                    }
                    spine.Parents.Add(t == 1 ? root : byId["b" + b + "_s" + (t - 1)]);
                    FinishNode(spine);
                    Add(nodes, byId, spine);

                    // Side node (+ optional Master chain)
                    if (!keystone)
                    {
                        float sideSign = (t % 2 == 0) ? 1f : -1f;
                        float sideAngle = angle + sideSign * 0.22f * gap;
                        var side = new SkillNode
                        {
                            Id = "b" + b + "_d" + t,
                            Kind = NodeKind.Side,
                            Branch = b,
                            Tier = t,
                            Position = Polar(radius + step * 0.5f, sideAngle),
                            BaseCost = Cost(cfg, t + 0.5, 0.8),
                        };
                        side.Effects.AddRange(SideEffects(b, t));
                        side.Name = SideName(b, t) + " " + NumberFormat.Roman(t);
                        side.Parents.Add(spine);
                        FinishNode(side);
                        Add(nodes, byId, side);

                        if (Hash.Hash01(cfg.treeSeed, b, t, 3) < 0.35)
                        {
                            var master = new SkillNode
                            {
                                Id = "b" + b + "_d" + t + "_2",
                                Kind = NodeKind.Side,
                                Branch = b,
                                Tier = t + 1,
                                Position = Polar(radius + step * 1.5f, sideAngle + sideSign * 0.06f * gap),
                                BaseCost = Cost(cfg, t + 1, 0.8),
                            };
                            foreach (var e in SideEffects(b, t)) master.Effects.Add(e.Scaled(1.5));
                            master.Name = "Master " + SideName(b, t) + " " + NumberFormat.Roman(t);
                            master.Parents.Add(side);
                            FinishNode(master);
                            Add(nodes, byId, master);
                        }
                    }
                }
            }

            // ---- Bridges between neighbouring branches (need both spines) ----
            for (int b = 0; b < BranchCount; b++)
            {
                int nb = (b + 1) % BranchCount;
                for (int t = 3; t <= T; t += 6)
                {
                    float radius = r0 + (t - 1) * step;
                    var bridge = new SkillNode
                    {
                        Id = "x" + b + "_" + t,
                        Kind = NodeKind.Bridge,
                        Branch = b,
                        Tier = t,
                        Position = Polar(radius, branches[b].Angle - 0.5f * gap),
                        BaseCost = Cost(cfg, t + 1, 2.0),
                        RequireAll = true,
                        Name = "Synergy: " + BranchNames[b] + " & " + BranchNames[nb] + " " + NumberFormat.Roman(t / 6 + 1),
                        Flavor = "Links two disciplines. Needs both neighbours.",
                        Glyph = "&",
                    };
                    bridge.Effects.AddRange(SpineEffects(b, t));
                    bridge.Effects.AddRange(SpineEffects(nb, t));
                    bridge.Parents.Add(byId["b" + b + "_s" + t]);
                    bridge.Parents.Add(byId["b" + nb + "_s" + t]);
                    Add(nodes, byId, bridge);
                }
            }

            return new SkillTree(nodes, branches, root);
        }

        // ------------------------------------------------------------------ effect tables

        // The branch's "signature" upgrade, used for spine nodes and bridges.
        static IEnumerable<SkillEffect> SpineEffects(int b, int t)
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

        static IEnumerable<SkillEffect> SideEffects(int b, int t)
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

        static IEnumerable<SkillEffect> KeystoneEffects(int b, int t)
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

        static string SpineName(int b, int t)
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

        static string SideName(int b, int t)
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

        // ------------------------------------------------------------------ helpers

        static double Cost(GameConfig cfg, double tier, double mult)
        {
            return Math.Round(cfg.nodeBaseCost * Math.Pow(cfg.nodeCostGrowth, tier - 1) * mult);
        }

        static Vector2 Polar(float r, float a) { return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }

        static void FinishNode(SkillNode n)
        {
            if (string.IsNullOrEmpty(n.Glyph) && n.Effects.Count > 0)
                n.Glyph = StatText.Glyph(n.Effects[0]);
        }

        static void Add(List<SkillNode> list, Dictionary<string, SkillNode> byId, SkillNode n)
        {
            list.Add(n);
            byId[n.Id] = n;
        }
    }
}
