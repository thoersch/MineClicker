using System;
using System.Globalization;

namespace IdleMine
{
    /// <summary>
    /// Formats huge doubles the way idle games do: 1.23K, 45.6M, 789B ... then aa, ab, ac.
    /// A double tops out around 1e308 (suffix "dm"), which is plenty for this template.
    /// If you ever need more headroom, swap double for BreakInfinity.cs's BigDouble.
    /// </summary>
    public static class NumberFormat
    {
        static readonly string[] Named = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return v > 0 ? "INF" : "-INF";
            if (v < 0) return "-" + Format(-v);

            if (v < 1000)
            {
                if (v < 10 && Math.Abs(v - Math.Round(v)) > 0.005) return v.ToString("0.##", Inv);
                if (v < 100 && Math.Abs(v - Math.Round(v)) > 0.05) return v.ToString("0.#", Inv);
                return Math.Floor(v + 1e-9).ToString("0", Inv);
            }

            int tier = (int)Math.Floor(Math.Log10(v) / 3.0);
            double mantissa = v / Math.Pow(1000.0, tier);
            if (mantissa >= 999.5) { mantissa /= 1000.0; tier++; }

            string fmt = mantissa < 9.995 ? "0.00" : (mantissa < 99.95 ? "0.0" : "0");
            return mantissa.ToString(fmt, Inv) + Suffix(tier);
        }

        public static string Money(double v) { return "$" + Format(v); }

        public static string Percent(double fraction)
        {
            double p = fraction * 100.0;
            return (Math.Abs(p - Math.Round(p)) < 0.05 ? Math.Round(p).ToString("0", Inv) : p.ToString("0.#", Inv)) + "%";
        }

        public static string Multiplier(double m)
        {
            return "\u00D7" + (Math.Abs(m - Math.Round(m)) < 0.001 ? m.ToString("0", Inv) : m.ToString("0.##", Inv));
        }

        static string Suffix(int tier)
        {
            if (tier < Named.Length) return Named[tier];
            int k = tier - Named.Length;
            return new string(new[] { (char)('a' + (k / 26) % 26), (char)('a' + k % 26) });
        }

        public static string Time(double seconds)
        {
            if (seconds < 0) seconds = 0;
            long s = (long)seconds;
            long d = s / 86400; s %= 86400;
            long h = s / 3600; s %= 3600;
            long m = s / 60; s %= 60;
            if (d > 0) return d + "d " + h + "h";
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m " + s.ToString("00") + "s";
            return s + "s";
        }

        public static string Roman(int n)
        {
            if (n <= 0) return "0";
            if (n > 3999) return n.ToString(Inv);
            int[] vals = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] syms = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < vals.Length; i++)
                while (n >= vals[i]) { sb.Append(syms[i]); n -= vals[i]; }
            return sb.ToString();
        }
    }
}
