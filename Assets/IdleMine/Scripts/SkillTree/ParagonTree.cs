using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleMine
{
    /// <summary>One perk in the Paragon tree. Bought with Paragon Points; kept across ascensions until a respec.</summary>
    public class ParagonPerk
    {
        public string Id;
        public string Name;
        public string Flavor;
        public int RequiredLevel;   // Paragon level needed before it can be bought
        public int Cost;            // Paragon Points
        public Vector2 Position;    // layout in the Paragon tree view, canvas units from the top of the tree
        public bool Capstone;       // a major perk (every fifth tier): drawn bigger
        public readonly List<SkillEffect> Effects = new List<SkillEffect>();
        public readonly List<ParagonPerk> Parents = new List<ParagonPerk>();

        public string Description { get { return string.Join("\n", Effects.Select(StatText.Describe).ToArray()); } }
        public string Glyph { get { return Effects.Count > 0 ? StatText.Glyph(Effects[0]) : "?"; } }
    }

    /// <summary>
    /// The Paragon tree: three lanes (Crew, Riches, Depths) that never end. Tier N of every lane unlocks at
    /// Paragon N, every perk costs 1 point, and you get one point per Paragon level, so each new level always
    /// has something to spend on (with three lanes to choose from, you can't own everything; respec is free).
    /// Every fifth tier is a major perk with a big multiplier. Perks are generated tier by tier, so the tree
    /// grows as far as needed: call EnsureTiers. Tune the lanes in Minor / Major below.
    /// </summary>
    public class ParagonTree
    {
        public const int BaseTiers = 40;   // always built; EnsureTiers extends past this
        public const int TiersAhead = 10;  // how far past the player's level the tree is shown
        public const float LaneX = 270f, TierStep = 170f;
        public static readonly string[] LaneNames = { "CREW", "RICHES", "DEPTHS" };
        static readonly string[] Prefix = { "pa", "pb", "pc" };

        public readonly List<ParagonPerk> Perks = new List<ParagonPerk>();
        readonly Dictionary<string, ParagonPerk> _byId = new Dictionary<string, ParagonPerk>();

        public int Tiers { get; private set; }

        public ParagonPerk Get(string id)
        {
            ParagonPerk p;
            return id != null && _byId.TryGetValue(id, out p) ? p : null;
        }

        /// <summary>Tier (1-based) a perk id belongs to, or 0 if it isn't a tree id. Lets saves grow the tree first.</summary>
        public static int TierOf(string id)
        {
            int t;
            return id != null && id.Length > 2 && id[0] == 'p' && int.TryParse(id.Substring(2), out t) ? t : 0;
        }

        public static ParagonTree Build()
        {
            var t = new ParagonTree();
            t.EnsureTiers(BaseTiers);
            return t;
        }

        /// <summary>Generates tiers up to <paramref name="tiers"/>. Returns true if anything was added.</summary>
        public bool EnsureTiers(int tiers)
        {
            if (tiers <= Tiers) return false;
            for (int tier = Tiers + 1; tier <= tiers; tier++)
                for (int lane = 0; lane < 3; lane++)
                {
                    var p = tier % 5 == 0 ? Major(lane, tier) : Minor(lane, tier);
                    p.Id = Prefix[lane] + tier;
                    p.RequiredLevel = tier;
                    p.Cost = 1;
                    p.Position = new Vector2((lane - 1) * LaneX, -(tier - 1) * TierStep);
                    var parent = Get(Prefix[lane] + (tier - 1));
                    if (parent != null) p.Parents.Add(parent);
                    Perks.Add(p);
                    _byId[p.Id] = p;
                }
            Tiers = tiers;
            return true;
        }

        // ================================================================== lanes

        // Small steps: the four tiers between majors cycle through these.
        static ParagonPerk Minor(int lane, int tier)
        {
            int slot = tier % 5;            // 1..4
            int block = tier / 5 + 1;       // 1 for tiers 1-4, 2 for 6-9, ...
            string n = " " + Roman(block);
            switch (lane)
            {
                case 0: // Crew
                    switch (slot)
                    {
                        case 1: return P("Extra Hands" + n, "More gloves at the shaft.", E(StatType.MinerCount, ModOp.Flat, 1 + tier / 10));
                        case 2: return P("Overtime" + n, "Nobody goes home early.", E(StatType.MinerSpeed, ModOp.Percent, 0.15));
                        case 3: return P("Foreman's Eye" + n, "Someone is always swinging.", E(StatType.AutoTapRate, ModOp.Flat, 0.5));
                        default: return P("Work Songs" + n, "A rhythm the whole crew swings to.", E(StatType.MinerSpeed, ModOp.Percent, 0.15));
                    }
                case 1: // Riches
                    switch (slot)
                    {
                        case 1: return P("Rich Veins" + n, "You know exactly where to dig.", E(StatType.OreValue, ModOp.Percent, 0.15));
                        case 2: return P("Lucky Strikes" + n, "The rock splits just right.", E(StatType.CritChance, ModOp.Flat, 0.02));
                        case 3: return P("Assayer's Eye" + n, "Nothing of value goes to the slag heap.", E(StatType.OreValue, ModOp.Percent, 0.15));
                        default: return P("Golden Touch" + n, "Lucky strikes hit harder.", E(StatType.CritMultiplier, ModOp.Flat, 1));
                    }
                default: // Depths
                    switch (slot)
                    {
                        case 1: return P("Blasting Caps" + n, "Faster through the hard stuff.", E(StatType.DigSpeed, ModOp.Percent, 0.2));
                        case 2: return P("Steel Picks" + n, "Every swing bites deeper.", E(StatType.TapPower, ModOp.Percent, 0.25));
                        case 3: return P("Deep Survey" + n, "The deeper, the richer.", E(StatType.DepthBonus, ModOp.Flat, 0.01));
                        default:
                            return block % 2 == 1
                                ? P("Night Shift" + n, "The mine never sleeps.", E(StatType.OfflineEfficiency, ModOp.Flat, 0.05))
                                : P("Long Haul" + n, "The crew keeps digging while you're gone.", E(StatType.OfflineCapHours, ModOp.Flat, 1));
                    }
            }
        }

        // Big steps every fifth tier.
        static ParagonPerk Major(int lane, int tier)
        {
            int k = tier / 5; // 1, 2, 3...
            ParagonPerk p;
            switch (lane)
            {
                case 0: // Crew: an extra slot twice, otherwise a doubling of miner speed
                    if (tier == 5 || tier == 25)
                        p = P("Wide Shafts " + Roman(tier == 5 ? 1 : 2), "Room for one more on every ledge.",
                              E(StatType.LayerSlots, ModOp.Flat, 1), E(StatType.MinerCount, ModOp.Flat, 3));
                    else
                        p = P("Legendary Crew " + Roman(k), "Songs are sung about this crew.", E(StatType.MinerSpeed, ModOp.Multiply, 2));
                    break;
                case 1: // Riches: doubled ore value; the first three also cut skill costs
                    p = k <= 6 && k % 2 == 0
                        ? P("Bulk Orders " + Roman(k / 2), "The supplier gives you a deal.", E(StatType.OreValue, ModOp.Multiply, 2), E(StatType.SkillDiscount, ModOp.Flat, 0.05))
                        : P("Motherlode Maps " + Roman(k), "Every vein marked in gold.", E(StatType.OreValue, ModOp.Multiply, 2));
                    break;
                default: // Depths: alternates dig speed and tap power
                    p = k % 2 == 1
                        ? P("Bore Masters " + Roman((k + 1) / 2), "The mountain gives way.", E(StatType.DigSpeed, ModOp.Multiply, 2), E(StatType.DepthBonus, ModOp.Flat, 0.02))
                        : P("Titan Picks " + Roman(k / 2), "One swing, one boulder.", E(StatType.TapPower, ModOp.Multiply, 3));
                    break;
            }
            p.Capstone = true;
            return p;
        }

        static ParagonPerk P(string name, string flavor, params SkillEffect[] effects)
        {
            var p = new ParagonPerk { Name = name, Flavor = flavor };
            p.Effects.AddRange(effects);
            return p;
        }

        static SkillEffect E(StatType s, ModOp op, double v) { return new SkillEffect(s, op, v); }

        static readonly int[] RomanValues = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        static readonly string[] RomanDigits = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        public static string Roman(int n)
        {
            if (n <= 0 || n >= 4000) return n.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < RomanValues.Length; i++)
                while (n >= RomanValues[i]) { sb.Append(RomanDigits[i]); n -= RomanValues[i]; }
            return sb.ToString();
        }
    }
}
