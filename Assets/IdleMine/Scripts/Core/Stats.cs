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
            return op == ModOp.Multiply ? this : new SkillEffect(stat, op, value * k);
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
                case StatType.CritMultiplier: return "+" + e.value + "x Crit Damage";
                case StatType.AutoTapRate: return "+" + e.value + " Auto-Taps / sec";
                case StatType.DepthBonus: return "+" + NumberFormat.Percent(e.value) + " Ore Value per depth level";
                case StatType.OfflineEfficiency: return "+" + NumberFormat.Percent(e.value) + " Offline Earnings";
                case StatType.OfflineCapHours: return "+" + e.value + "h Offline Time Cap";
                case StatType.SkillDiscount: return "-" + NumberFormat.Percent(e.value) + " Skill Costs";
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
                case StatType.CritMultiplier: return "Crit Damage";
                case StatType.AutoTapRate: return "Auto-Tap";
                case StatType.DepthBonus: return "Depth Bonus";
                case StatType.OfflineEfficiency: return "Offline Earnings";
                case StatType.OfflineCapHours: return "Offline Cap";
                case StatType.SkillDiscount: return "Skill Discount";
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
            }
            return "?";
        }
    }
}
