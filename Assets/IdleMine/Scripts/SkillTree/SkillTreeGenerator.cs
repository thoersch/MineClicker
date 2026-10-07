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
    /// What the nodes do, what they're called and what they cost comes from a TreeRecipe: MineTreeRecipe for the
    /// normal skill tree, DeepCoreRecipe for the Deep Core. To hand-author content instead, build your own
    /// List&lt;SkillNode&gt; and pass it to new SkillTree(...).
    /// </summary>
    public static class SkillTreeGenerator
    {
        public const int BranchCount = 6;

        public static SkillTree Generate(GameConfig cfg) { return Generate(cfg, new MineTreeRecipe()); }

        public static SkillTree Generate(GameConfig cfg, TreeRecipe recipe)
        {
            string P = recipe.IdPrefix;
            var nodes = new List<SkillNode>();
            var byId = new Dictionary<string, SkillNode>();
            var branches = new List<BranchInfo>();

            float gap = Mathf.PI * 2f / BranchCount;
            for (int b = 0; b < BranchCount; b++)
            {
                Color c;
                ColorUtility.TryParseHtmlString("#" + recipe.BranchColors[b], out c);
                branches.Add(new BranchInfo { Name = recipe.BranchNames[b], Color = c, Angle = Mathf.PI / 2f - b * gap });
            }

            // ---- Root: the node that starts the whole game loop ----
            var root = recipe.MakeRoot(cfg);
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
                        Id = P + "b" + b + "_s" + t,
                        Kind = keystone ? NodeKind.Keystone : NodeKind.Spine,
                        Branch = b,
                        Tier = t,
                        Position = Polar(radius, angle),
                        BaseCost = recipe.Cost(cfg, t, keystone ? 3.0 : 1.0),
                    };
                    spine.Effects.AddRange(keystone ? recipe.KeystoneEffects(b, t) : recipe.SpineEffects(b, t));
                    if (keystone)
                    {
                        int k = t / 5 - 1;
                        spine.Name = recipe.KeystoneNames[b][k % recipe.KeystoneNames[b].Length] + (k >= recipe.KeystoneNames[b].Length ? " " + NumberFormat.Roman(k / recipe.KeystoneNames[b].Length + 1) : "");
                        spine.Flavor = recipe.KeystoneFlavor;
                    }
                    else
                    {
                        spine.Name = recipe.SpineTitle(b, t);
                        spine.Flavor = recipe.SpineFlavor(b, t);
                    }
                    spine.Parents.Add(t == 1 ? root : byId[P + "b" + b + "_s" + (t - 1)]);
                    FinishNode(spine);
                    Add(nodes, byId, spine);

                    // Side node (+ optional Master chain)
                    if (!keystone)
                    {
                        float sideSign = (t % 2 == 0) ? 1f : -1f;
                        float sideAngle = angle + sideSign * 0.22f * gap;
                        var side = new SkillNode
                        {
                            Id = P + "b" + b + "_d" + t,
                            Kind = NodeKind.Side,
                            Branch = b,
                            Tier = t,
                            Position = Polar(radius + step * 0.5f, sideAngle),
                            BaseCost = recipe.Cost(cfg, t + 0.5, 0.8),
                        };
                        side.Effects.AddRange(recipe.SideEffects(b, t));
                        side.Name = recipe.SideName(b, t) + " " + NumberFormat.Roman(t);
                        side.Parents.Add(spine);
                        FinishNode(side);
                        Add(nodes, byId, side);

                        if (Hash.Hash01(recipe.Seed(cfg), b, t, 3) < 0.35)
                        {
                            var master = new SkillNode
                            {
                                Id = P + "b" + b + "_d" + t + "_2",
                                Kind = NodeKind.Side,
                                Branch = b,
                                Tier = t + 1,
                                Position = Polar(radius + step * 1.5f, sideAngle + sideSign * 0.06f * gap),
                                BaseCost = recipe.Cost(cfg, t + 1, 0.8),
                            };
                            foreach (var e in recipe.SideEffects(b, t)) master.Effects.Add(e.Scaled(1.5));
                            master.Name = "Master " + recipe.SideName(b, t) + " " + NumberFormat.Roman(t);
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
                        Id = P + "x" + b + "_" + t,
                        Kind = NodeKind.Bridge,
                        Branch = b,
                        Tier = t,
                        Position = Polar(radius, branches[b].Angle - 0.5f * gap),
                        BaseCost = recipe.Cost(cfg, t + 1, 2.0),
                        RequireAll = true,
                        Name = "Synergy: " + recipe.BranchNames[b] + " & " + recipe.BranchNames[nb] + " " + NumberFormat.Roman(t / 6 + 1),
                        Flavor = "Links two disciplines. Needs both neighbours.",
                        Glyph = "&",
                    };
                    bridge.Effects.AddRange(recipe.SpineEffects(b, t));
                    bridge.Effects.AddRange(recipe.SpineEffects(nb, t));
                    bridge.Parents.Add(byId[P + "b" + b + "_s" + t]);
                    bridge.Parents.Add(byId[P + "b" + nb + "_s" + t]);
                    Add(nodes, byId, bridge);
                }
            }

            return new SkillTree(nodes, branches, root);
        }

        // ------------------------------------------------------------------ helpers

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
