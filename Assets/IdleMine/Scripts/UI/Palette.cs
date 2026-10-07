using UnityEngine;

namespace IdleMine
{
    public static class Palette
    {
        public static readonly Color Background = Hex("120E17");
        public static readonly Color Panel = Hex("1E1A26");
        public static readonly Color PanelLight = Hex("2C2536");
        public static readonly Color PanelLine = Hex("3B3347");
        public static readonly Color Text = Hex("F6F0E4");
        public static readonly Color TextDim = Hex("A99FB8");
        public static readonly Color Gold = Hex("FFC845");
        public static readonly Color Green = Hex("6BE07B");
        public static readonly Color Red = Hex("FF6B6B");
        public static readonly Color Orange = Hex("FF9F43");
        public static readonly Color Sky = Hex("6FB7E8");
        public static readonly Color SkyLow = Hex("BFE3F2");
        public static readonly Color Grass = Hex("5DAA48");
        public static readonly Color Shadow = new Color(0, 0, 0, 0.55f);

        static readonly Color[] BandColors =
        {
            Hex("7A5230"), Hex("9C5A34"), Hex("9E8E6E"), Hex("B08650"), Hex("5E6470"),
            Hex("7D6B6B"), Hex("454C59"), Hex("3A2E4A"), Hex("2F6A86"), Hex("8F2E1E"),
            Hex("A2461E"), Hex("C0662A"), Hex("C9982F"), Hex("1F4A5E"), Hex("26213D"),
            // below the bottom of the world (one per LayerCatalog band)
            Hex("4E6B3A"), Hex("6A3E7A"), Hex("B8A98A"), Hex("1E5E5A"), Hex("2E5470"),
            Hex("7A5A3A"), Hex("8A3A1E"), Hex("B0401A"), Hex("B8862A"), Hex("A89E8C"),
            Hex("6A4A8E"), Hex("C8C0D8"), Hex("2A2433"), Hex("1C1A2A"), Hex("5E6A4E"),
            Hex("3E5A9E"), Hex("6EA8C8"), Hex("3E4E6E"), Hex("5A7A3E"), Hex("9E5AA8"),
            // out among the stars
            Hex("6E4A3A"), Hex("8E3A2A"), Hex("3A1E5E"), Hex("4A5EAE"), Hex("8E3A8E"),
            Hex("8EB8D0"), Hex("2E2A4E"), Hex("9E8A5E"), Hex("B87AB0"), Hex("C8B070"),
            Hex("C8501E"), Hex("1A1626"), Hex("2A1E3E"), Hex("4A2A2A"), Hex("6A1E4E"),
        };

        /// <summary>Earthy colour per rock band, darkening slightly within a band, looping with a hue shift past the last band.</summary>
        public static Color Layer(int index)
        {
            int band = LayerCatalog.Band(index);
            Color c = BandColors[band % BandColors.Length];
            int loop = band / BandColors.Length;
            if (loop > 0)
            {
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                c = Color.HSVToRGB(Mathf.Repeat(h + 0.17f * loop, 1f), s, v * 0.8f);
            }
            float inBand = (index % LayerCatalog.LayersPerBand) / (float)LayerCatalog.LayersPerBand;
            return Color.Lerp(c, Color.black, 0.18f + inBand * 0.18f);
        }

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
