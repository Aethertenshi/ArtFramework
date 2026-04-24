using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace ArtFramework.AtlasParser
{
    // This is the main class representing a loaded MTSDF font, containing the texture and glyph data
    public class MtsdfFont
    {
        public Texture2D Texture { get; internal set; }
        public float DistanceRange { get; internal set; }
        public float EmSize { get; internal set; }
        public Dictionary<char, MtsdfGlyph> Glyphs { get; internal set; } = new();

        // Fallback to '?' if character isn't loaded
        public MtsdfGlyph GetGlyph(char c) => Glyphs.TryGetValue(c, out var g) ? g : Glyphs['?'];
    }

    public struct MtsdfGlyph
    {
        public float Advance;
        public Vector4 AtlasBounds; // X=Left, Y=Bottom, Z=Right, W=Top
        public Vector4 PlaneBounds; // X=Left, Y=Bottom, Z=Right, W=Top
    }

    // This is the structure we get from parsing the JSON, which we then convert to MtsdfFont
    public class AtlasData
    {
        public AtlasMetrics Metrics { get; set; }
        public List<GlyphData> Glyphs { get; set; }
    }

    public class AtlasMetrics
    {
        public float EmSize { get; set; }
        public float LineHeight { get; set; }
        public float Ascent { get; set; }
        public float Descent { get; set; }
        public float DistanceRange { get; set; } // This is your 'pxrange' from the command
    }

    public class GlyphData
    {
        public int Unicode { get; set; }
        public float Advance { get; set; }
        public PlaneBounds PlaneBounds { get; set; } // The "Vector" bounds
        public AtlasBounds AtlasBounds { get; set; } // The Texture UV bounds
    }

    // These define the rectangle on the atlas and how it sits on the baseline
    public record PlaneBounds(float Left, float Bottom, float Right, float Top);
    public record AtlasBounds(float Left, float Bottom, float Right, float Top);
}