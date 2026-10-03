using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleMine
{
    /// <summary>One perk in the Paragon tree. Bought with Paragon Points; lasts for the current run only.</summary>
    public class ParagonPerk
    {
        public string Id;
        public string Name;
        public string Flavor;
        public int RequiredLevel;   // Paragon level needed before it can be bought
        public int Cost;            // Paragon Points
        public Vector2 Position;    // layout in the Paragon tree view, canvas units from its centre
        public bool Capstone;
        public readonly List<SkillEffect> Effects = new List<SkillEffect>();
        public readonly List<ParagonPerk> Parents = new List<ParagonPerk>();

        public string Description { get { return string.Join("\n", Effects.Select(StatText.Describe).ToArray()); } }
        public string Glyph { get { return Effects.Count > 0 ? StatText.Glyph(Effects[0]) : "?"; } }
    }

    /// <summary>
    /// The Paragon tree: big, run-only perks. Every run you get Paragon Points equal to your Paragon level;
    /// perks cost points and unlock at rising Paragon levels, each needing the perk above it in its column.
    /// Ascending wipes the perks and refills the points, so each run is a fresh choice. Two columns climb
    /// eight tiers to a capstone that needs both. Tune the whole tree in the Define calls below.
    /// </summary>
    public class ParagonTree
    {
        public readonly List<ParagonPerk> Perks = new List<ParagonPerk>();
        readonly Dictionary<string, ParagonPerk> _byId = new Dictionary<string, ParagonPerk>();

        public ParagonPerk Get(string id)
        {
            ParagonPerk p;
            return id != null && _byId.TryGetValue(id, out p) ? p : null;
        }

        public static ParagonTree Build()
        {
            var t = new ParagonTree();
            const float x = 210f, top = 560f, step = 160f;

            // tier, required Paragon level, cost
            t.Column("L", -x, top, step, new[]
            {
                P("Extra Hands", "Two more pairs of gloves at the shaft.", 1, 1, E(StatType.MinerCount, ModOp.Flat, 2)),
                P("Wide Shafts", "Room for one more on every ledge.", 3, 1, E(StatType.LayerSlots, ModOp.Flat, 1)),
                P("Overtime", "Nobody goes home early.", 5, 2, E(StatType.MinerSpeed, ModOp.Multiply, 1.5)),
                P("Crew Expansion", "Word got out. They came.", 8, 2, E(StatType.MinerCount, ModOp.Flat, 5)),
                P("Foreman's Eye", "Someone is always swinging.", 12, 3, E(StatType.AutoTapRate, ModOp.Flat, 2)),
                P("Deep Survey", "The deeper, the richer.", 16, 3, E(StatType.DepthBonus, ModOp.Flat, 0.05)),
                P("Legendary Crew", "Songs are sung about this crew.", 20, 4, E(StatType.MinerSpeed, ModOp.Multiply, 2)),
            });
            t.Column("R", x, top, step, new[]
            {
                P("Rich Veins", "You know exactly where to dig.", 1, 1, E(StatType.OreValue, ModOp.Multiply, 1.5)),
                P("Steel Picks", "Every swing counts double.", 3, 1, E(StatType.TapPower, ModOp.Multiply, 2)),
                P("Blasting Caps", "Faster through the hard stuff.", 5, 2, E(StatType.DigSpeed, ModOp.Multiply, 2)),
                P("Bulk Orders", "The supplier gives you a deal.", 8, 2, E(StatType.SkillDiscount, ModOp.Flat, 0.15)),
                P("Night Shift", "The mine never sleeps.", 12, 3, E(StatType.OfflineEfficiency, ModOp.Flat, 0.25), E(StatType.OfflineCapHours, ModOp.Flat, 2)),
                P("Golden Touch", "Lucky strikes, every time.", 16, 3, E(StatType.CritChance, ModOp.Flat, 0.1), E(StatType.CritMultiplier, ModOp.Flat, 5)),
                P("Motherlode Maps", "Every vein marked in gold.", 20, 4, E(StatType.OreValue, ModOp.Multiply, 2)),
            });

            var cap = P("Paragon's Will", "The whole mountain answers to you.", 30, 5,
                        E(StatType.OreValue, ModOp.Multiply, 3), E(StatType.MinerSpeed, ModOp.Multiply, 3));
            cap.Id = "cap";
            cap.Capstone = true;
            cap.Position = new Vector2(0, top - 7 * step);
            cap.Parents.Add(t.Get("L6"));
            cap.Parents.Add(t.Get("R6"));
            t.Add(cap);
            return t;
        }

        void Column(string prefix, float x, float top, float step, ParagonPerk[] perks)
        {
            for (int i = 0; i < perks.Length; i++)
            {
                var p = perks[i];
                p.Id = prefix + i;
                p.Position = new Vector2(x, top - i * step);
                if (i > 0) p.Parents.Add(perks[i - 1]);
                Add(p);
            }
        }

        void Add(ParagonPerk p)
        {
            Perks.Add(p);
            _byId[p.Id] = p;
        }

        static ParagonPerk P(string name, string flavor, int level, int cost, params SkillEffect[] effects)
        {
            var p = new ParagonPerk { Name = name, Flavor = flavor, RequiredLevel = level, Cost = cost };
            p.Effects.AddRange(effects);
            return p;
        }

        static SkillEffect E(StatType s, ModOp op, double v) { return new SkillEffect(s, op, v); }
    }
}
