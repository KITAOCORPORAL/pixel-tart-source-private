using System.Globalization;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public static class PaletteClipboardText
{
    public static string Hsl(DominantColor color) => string.Create(CultureInfo.InvariantCulture,
        $"HSL({Math.Round(color.Hue) % 360:0}, {color.Saturation * 100:0}%, {color.Lightness * 100:0}%)");
}

/// <summary>Prepared once from the already decoded analysis proxy and its zone map.
/// Hover only selects an immutable frame; it performs no decoding or analysis.</summary>
public sealed class VisualZoneHoverPreview
{
    private readonly VisualPixelBuffer[] _frames;
    public VisualPixelBuffer Original { get; }
    public VisualZoneHoverPreview(VisualPixelBuffer source, VisualPixelBuffer zoneMap, CancellationToken token = default)
    {
        if (source.Width != zoneMap.Width || source.Height != zoneMap.Height)
            throw new ArgumentException("Zone map and proxy dimensions must agree.", nameof(zoneMap));
        Original = source;
        _frames = new VisualPixelBuffer[11];
        for (var zone = 0; zone < 11; zone++)
        {
            token.ThrowIfCancellationRequested();
            var pixels = source.Rgb24.ToArray();
            for (var pixel = 0; pixel < source.PixelCount; pixel++)
            {
                var offset = pixel * 3;
                if ((int)Math.Round(zoneMap.Rgb24.Span[offset] * 10d / 255) != zone) continue;
                pixels[offset] = (byte)Math.Round(pixels[offset] * .76 + 21 * .24);
                pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * .76 + 199 * .24);
                pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * .76 + 174 * .24);
            }
            _frames[zone] = new(source.Width, source.Height, pixels);
        }
    }
    public VisualPixelBuffer Select(int? zone) => zone is null ? Original :
        zone is >= 0 and <= 10 ? _frames[zone.Value] : throw new ArgumentOutOfRangeException(nameof(zone));
}
