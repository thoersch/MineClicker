namespace IdleMine
{
    /// <summary>Runtime state of one depth level. Rates are cached by GameManager whenever stats change.</summary>
    public class MineLayer
    {
        public readonly int Index;

        // Saved state
        public double Progress;     // breakthrough progress, in ore
        public int Miners;          // miners assigned here

        // Cached, recomputed on stat changes (see GameManager.RecalculateLayer)
        public double OreRequired;
        public double Richness;
        public double ValuePerOre;
        public double OrePerMinerPerSecond;
        public double OrePerTap;

        // Presentation helpers (not saved)
        public double PendingIncome;      // cash earned since the view last popped a floating number
        public float LastTapTime = -10f;

        public MineLayer(int index) { Index = index; }

        public bool Cleared { get { return Progress >= OreRequired; } }
        public double ProgressFraction { get { return OreRequired <= 0 ? 1 : System.Math.Min(1.0, Progress / OreRequired); } }
        public bool IsMotherlode { get { return Richness >= 2.99; } }
    }

    public static class Hash
    {
        /// <summary>Stable 0..1 value from integer inputs. Same inputs always give the same output,
        /// on every platform, so layer richness and tree layout never shift between sessions.</summary>
        public static double Hash01(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint x = (uint)(seed * 73856093) ^ (uint)(a * 19349663) ^ (uint)(b * 83492791) ^ ((uint)c * 2654435761u);
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return x / 4294967296.0;
            }
        }
    }

    /// <summary>
    /// Flavor for layers: a rock band every five levels, and the ore found in them. The bands run from the surface
    /// through the planet's core, then into stranger places (Hollow Earth, dwarven halls, dragon hoards...) and out
    /// into cosmic depths, 250 layers in all before the endless numbered Abyss. Ores keep advancing with depth
    /// instead of looping back to Copper.
    /// </summary>
    public static class LayerCatalog
    {
        public const int LayersPerBand = 5;

        static readonly string[] Bands =
        {
            // the planet
            "Topsoil", "Clay", "Limestone", "Sandstone", "Shale", "Granite", "Basalt", "Obsidian",
            "Crystal Caverns", "Magma Veins", "Mantle", "Outer Core", "Inner Core", "The Deep", "Abyss",
            // below the bottom of the world
            "Hollow Earth", "Fungal Forest", "Fossil Graveyard", "Glowworm Grotto", "Sunken City",
            "Dwarven Halls", "Lost Forge", "Lava Lakes", "Dragon's Hoard", "Titan Bones",
            "Geode Palace", "Quartz Cathedral", "Shadow Roots", "Whispering Dark", "Elder Ruins",
            "Rune Vaults", "Frozen Heart", "Storm Caves", "Living Rock", "Prism Depths",
            // out among the stars
            "Starfall Crater", "Meteor Core", "Void Rift", "Astral Seam", "Nebula Veins",
            "Comet Ice", "Gravity Well", "Time Strata", "Dream Stone", "Celestial Bedrock",
            "Cosmic Furnace", "Dark Matter", "Singularity", "Event Horizon", "Primordial Chaos",
        };

        static readonly string[] Ores =
        {
            "Copper", "Tin", "Iron", "Coal", "Silver", "Gold", "Platinum", "Cobalt", "Titanium",
            "Mithril", "Adamantite", "Orichalcum", "Starmetal", "Voidstone", "Aether",
            "Moonstone", "Sunsteel", "Dragonglass", "Bloodstone", "Frostgold", "Runesilver",
            "Thunderite", "Shadowsteel", "Phoenix Ore", "Titanite", "Celestium", "Nebulite",
            "Starshard", "Chronium", "Dreamsteel", "Void Crystal", "Darkmatter", "Quasarite",
            "Neutronium", "Singularium", "Primordium", "Eternium",
        };

        // The first 15 ores change every 3 layers (as before), the rest every 9, so later ones last a while.
        const int EarlyOres = 15, EarlyStep = 3, LateStep = 9;

        public static int Band(int layer) { return layer / LayersPerBand; }
        public static int BandCount { get { return Bands.Length; } }

        public static string BandName(int layer)
        {
            int b = Band(layer);
            if (b < Bands.Length) return Bands[b];
            return "Abyss " + NumberFormat.Roman(b - Bands.Length + 2);
        }

        public static string OreName(int layer)
        {
            int i = layer < EarlyOres * EarlyStep ? layer / EarlyStep : EarlyOres + (layer - EarlyOres * EarlyStep) / LateStep;
            if (i < Ores.Length) return Ores[i];
            return Ores[Ores.Length - 1] + " " + NumberFormat.Roman(i - Ores.Length + 2);
        }
    }
}
