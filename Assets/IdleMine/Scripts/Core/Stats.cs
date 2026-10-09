using System;

namespace IdleMine
{
    /// <summary>Every number the skill tree can touch. Add a new entry here, give it a base value in
    /// GameManager.ApplyBaseStats, a description in StatText, and read it wherever it matters.</summary>
    public enum StatType
    {
        MinerCount,        // total miners owned
        MinerSpeed,        // multiplier on ore/sec per miner
        OreValue,          // multiplier on cash per ore
        TapPower,          // multiplier on ore per tap
        LayerSlots,        // miner slots on every layer
        DigSpeed,          // multiplier on breakthrough progress (not on cash)
        CritChance,        // chance a manual tap crits
        CritMultiplier,    // crit tap multiplier
        AutoTapRate,       // automatic taps per second on the deepest layer
        DepthBonus,        // +X ore value per layer of depth
        OfflineEfficiency, // fraction of normal speed while away
        OfflineCapHours,   // max hours of offline progress
        SkillDiscount,     // fraction off skill costs

        // Deep Core mechanics. Each is zero (off) until its branch's first node is bought.
        DynamitePower,       // a full-charge blast is worth this many seconds of income
        DynamiteRadius,      // extra layers a blast reaches on each side
        DynamiteCharge,      // fuse speed multiplier
        DynamiteCooldownCut, // fraction off the blast cooldown
        GemRate,             // gem veins appearing per minute
        GemValue,            // minutes of income per gem
        DrillCount,          // drill rigs (all on the frontier, stacking)
        DrillPower,          // each drill digs like this many miners
        OverclockPower,      // extra production while overclocked (+100% = 1)
        OverclockDuration,   // seconds an overclock lasts
        OverclockCharge,     // overclock meter fill-speed multiplier
        SwingPower,          // a perfect power swing is worth this many seconds of income
        SwingWindow,         // width of the perfect zone, as a fraction of the bar
        SwingRate,           // power swing chances per minute
        DrillBore,           // each drill makes the frontier break through this much faster (1 + bore x drills)
    }

    /// <summary>
    /// Flat adds to the base, Percent adds to a shared "+%" pool, Multiply stacks multiplicatively.
    /// Final = (base + sum(Flat)) * (1 + sum(Percent)) * product(Multiply).
    /// Keeping % additive and x multiplicative is what makes keystone nodes feel huge.
    /// </summary>
    public enum ModOp { Flat, Percent, Multiply }

    [Serializable]
    public struct SkillEffect
    {
        public StatType stat;
        public ModOp op;
        public double value;

        public SkillEffect(StatType stat, ModOp op, double value)
        {
            this.stat = stat; this.op = op; this.value = value;
        }

        public SkillEffect Scaled(double k)
        {
            if (op == ModOp.Multiply) return this;
            double v = value * k;
            // You can't hire half a miner or dig half a slot: counts stay whole (and never drop to zero).
            if (op == ModOp.Flat && IsWholeNumber(stat)) v = Math.Max(1, Math.Round(v, MidpointRounding.AwayFromZero));
            return new SkillEffect(stat, op, v);
        }

        public static bool IsWholeNumber(StatType t)
        {
            return t == StatType.MinerCount || t == StatType.LayerSlots || t == StatType.DrillCount || t == StatType.DynamiteRadius;
        }
    }

    public class StatBlock
    {
        public static readonly int Count = Enum.GetValues(typeof(StatType)).Length;

        readonly double[] _base = new double[Count];
        readonly double[] _flat = new double[Count];
        readonly double[] _pct = new double[Count];
        readonly double[] _mul = new double[Count];
        readonly double[] _cache = new double[Count];
        bool _dirty = true;

        public StatBlock() { ClearModifiers(); }

        public void SetBase(StatType t, double v) { _base[(int)t] = v; _dirty = true; }

        public void ClearModifiers()
        {
            for (int i = 0; i < Count; i++) { _flat[i] = 0; _pct[i] = 0; _mul[i] = 1; }
            _dirty = true;
        }

        public void Apply(SkillEffect e)
        {
            int i = (int)e.stat;
            switch (e.op)
            {
                case ModOp.Flat: _flat[i] += e.value; break;
                case ModOp.Percent: _pct[i] += e.value; break;
                case ModOp.Multiply: _mul[i] *= e.value; break;
            }
            _dirty = true;
        }

        public double Get(StatType t)
        {
            if (_dirty) Rebuild();
            return _cache[(int)t];
        }

        void Rebuild()
        {
            for (int i = 0; i < Count; i++)
                _cache[i] = Clamp((StatType)i, (_base[i] + _flat[i]) * (1.0 + _pct[i]) * _mul[i]);
            _dirty = false;
        }

        static double Clamp(StatType t, double v)
        {
            switch (t)
            {
                case StatType.CritChance: return Math.Min(v, 0.75);
                case StatType.SkillDiscount: return Math.Min(v, 0.75);
                case StatType.OfflineEfficiency: return Math.Min(v, 1.0);
                case StatType.DynamiteCooldownCut: return Math.Min(v, 0.8);
                case StatType.SwingWindow: return Math.Min(v, 0.45);
                default: return Math.Max(0, v);
            }
        }
    }

    /// <summary>Player-facing wording for effects, plus the short glyph shown on a node's face.</summary>
    public static class StatText
    {
        public static string Describe(SkillEffect e)
        {
            string name = Name(e.stat);
            if (e.op == ModOp.Multiply) return NumberFormat.Multiplier(e.value) + " " + name;
            if (e.op == ModOp.Percent) return "+" + NumberFormat.Percent(e.value) + " " + name;

            switch (e.stat)
            {
                case StatType.MinerCount: return "+" + e.value + (e.value == 1 ? " Miner" : " Miners");
                case StatType.LayerSlots: return "+" + e.value + " Miner Slot" + (e.value == 1 ? "" : "s") + " on every layer";
                case StatType.CritChance: return "+" + NumberFormat.Percent(e.value) + " Tap Crit Chance";
                case StatType.CritMultiplier: return "+" + e.value + "x Ore from Crit Taps";
                case StatType.AutoTapRate: return "+" + e.value + " Auto-Taps / sec";
                case StatType.DepthBonus: return "+" + NumberFormat.Percent(e.value) + " Ore Value per depth level";
                case StatType.OfflineEfficiency: return "+" + NumberFormat.Percent(e.value) + " Offline Earnings";
                case StatType.OfflineCapHours: return "+" + e.value + "h Offline Time Cap";
                case StatType.SkillDiscount: return "-" + NumberFormat.Percent(e.value) + " Skill Costs";
                case StatType.DynamitePower: return "Dynamite blasts worth +" + NumberFormat.Format(e.value) + "s of income";
                case StatType.DynamiteRadius: return "Dynamite reaches +" + e.value + " layer" + (e.value == 1 ? "" : "s") + " each way";
                case StatType.DynamiteCooldownCut: return "-" + NumberFormat.Percent(e.value) + " Dynamite Cooldown";
                case StatType.GemRate: return "+" + NumberFormat.Format(e.value) + " Gem Vein" + (e.value == 1 ? "" : "s") + " per minute";
                case StatType.GemValue: return "Gems worth +" + NumberFormat.Format(e.value) + " min of income";
                case StatType.DrillCount: return "+" + e.value + " Drill Rig" + (e.value == 1 ? "" : "s") + " on the deepest layer";
                case StatType.DrillPower: return "Each drill digs like +" + NumberFormat.Format(e.value) + " miners";
                case StatType.OverclockPower: return "+" + NumberFormat.Percent(e.value) + " production while Overclocked";
                case StatType.OverclockDuration: return "+" + NumberFormat.Format(e.value) + "s Overclock duration";
                case StatType.SwingPower: return "Perfect Swings worth +" + NumberFormat.Format(e.value) + "s of income";
                case StatType.SwingWindow: return "+" + NumberFormat.Percent(e.value) + " Perfect Swing zone";
                case StatType.SwingRate: return "+" + NumberFormat.Format(e.value) + " Power Swing" + (e.value == 1 ? "" : "s") + " per minute";
                case StatType.DrillBore: return "Each drill: +" + NumberFormat.Percent(e.value) + " breakthrough speed";
                default: return "+" + NumberFormat.Format(e.value) + " " + name;
            }
        }

        public static string Name(StatType t)
        {
            switch (t)
            {
                case StatType.MinerCount: return "Miners";
                case StatType.MinerSpeed: return "Miner Speed";
                case StatType.OreValue: return "Ore Value";
                case StatType.TapPower: return "Tap Power";
                case StatType.LayerSlots: return "Layer Slots";
                case StatType.DigSpeed: return "Dig Speed";
                case StatType.CritChance: return "Crit Chance";
                case StatType.CritMultiplier: return "Crit Bonus";
                case StatType.AutoTapRate: return "Auto-Tap";
                case StatType.DepthBonus: return "Depth Bonus";
                case StatType.OfflineEfficiency: return "Offline Earnings";
                case StatType.OfflineCapHours: return "Offline Cap";
                case StatType.SkillDiscount: return "Skill Discount";
                case StatType.DynamitePower: return "Blast Power";
                case StatType.DynamiteRadius: return "Blast Radius";
                case StatType.DynamiteCharge: return "Fuse Speed";
                case StatType.DynamiteCooldownCut: return "Dynamite Cooldown";
                case StatType.GemRate: return "Gem Veins";
                case StatType.GemValue: return "Gem Value";
                case StatType.DrillCount: return "Drill Rigs";
                case StatType.DrillPower: return "Drill Power";
                case StatType.OverclockPower: return "Overclock Boost";
                case StatType.OverclockDuration: return "Overclock Duration";
                case StatType.OverclockCharge: return "Overclock Charge Speed";
                case StatType.SwingPower: return "Swing Power";
                case StatType.SwingWindow: return "Perfect Zone";
                case StatType.SwingRate: return "Power Swings";
                case StatType.DrillBore: return "Drill Bore Speed";
            }
            return t.ToString();
        }

        public static string Glyph(SkillEffect e)
        {
            if (e.op == ModOp.Multiply) return NumberFormat.Multiplier(e.value);
            switch (e.stat)
            {
                case StatType.MinerCount: return "+" + e.value;
                case StatType.MinerSpeed: return "SPD";
                case StatType.OreValue: return "$";
                case StatType.TapPower: return "TAP";
                case StatType.LayerSlots: return "SLOT";
                case StatType.DigSpeed: return "DIG";
                case StatType.CritChance: return "CRIT";
                case StatType.CritMultiplier: return "CRIT";
                case StatType.AutoTapRate: return "AUTO";
                case StatType.DepthBonus: return "DEEP";
                case StatType.OfflineEfficiency: return "ZZZ";
                case StatType.OfflineCapHours: return "ZZZ";
                case StatType.SkillDiscount: return "%";
                case StatType.DynamitePower: return "TNT";
                case StatType.DynamiteRadius: return "BOOM";
                case StatType.DynamiteCharge: return "FUSE";
                case StatType.DynamiteCooldownCut: return "TNT";
                case StatType.GemRate: return "GEM";
                case StatType.GemValue: return "GEM";
                case StatType.DrillCount: return "+" + e.value;
                case StatType.DrillPower: return "DRILL";
                case StatType.OverclockPower: return "OC";
                case StatType.OverclockDuration: return "OC";
                case StatType.OverclockCharge: return "OC";
                case StatType.SwingPower: return "SWING";
                case StatType.SwingWindow: return "ZONE";
                case StatType.SwingRate: return "SWING";
                case StatType.DrillBore: return "BORE";
            }
            return "?";
        }
    }
}
