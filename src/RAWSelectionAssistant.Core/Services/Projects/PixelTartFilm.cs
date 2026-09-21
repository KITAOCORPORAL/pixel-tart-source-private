using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Non-destructive, deterministic Pixel Tart film treatment. It is deliberately
/// separate from the color LUT because grain, bloom, vignette and surface depend on pixels.</summary>
public sealed record PixelTartFilmSettings(
    bool Enabled = false,
    string ProfileId = "PT-N01",
    double ProfileAmount = 100,
    double GrainAmount = 0,
    double GrainSize = 35,
    double HalationAmount = 0,
    double BloomAmount = 0,
    double VignetteAmount = 0,
    double SurfaceAmount = 0,
    string TextureId = "None",
    double TextureAmount = 0,
    string FrameStyle = "None",
    int Seed = 17)
{
    public void Validate()
    {
        foreach (var value in new[] { ProfileAmount, GrainAmount, GrainSize, HalationAmount, BloomAmount, VignetteAmount, SurfaceAmount, TextureAmount })
            if (!double.IsFinite(value) || value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
        if (Seed < 0) throw new ArgumentOutOfRangeException(nameof(Seed));
    }
}

public static class PixelTartFilmProfiles
{
    public static IReadOnlyList<PixelTartFilmProfile> All { get; } =
        [new("PT-N01", "Neutral Film"), new("PT-W01", "Warm Soft"), new("PT-C01", "Cool Silver")];
}
public sealed record PixelTartFilmProfile(string Id, string Name);

public static class PixelTartFilmPipeline
{
    public static VisualPixelBuffer Apply(VisualPixelBuffer input, PixelTartFilmSettings settings, CancellationToken token = default)
    {
        settings.Validate();
        if (!settings.Enabled || IsIdentity(settings)) return new(input.Width, input.Height, input.Rgb24.ToArray());
        var output = input.Rgb24.ToArray();
        var luminance = new double[input.PixelCount];
        for (var pixel = 0; pixel < input.PixelCount; pixel++)
        {
            var offset = pixel * 3;
            luminance[pixel] = .2126 * output[offset] / 255d + .7152 * output[offset + 1] / 255d + .0722 * output[offset + 2] / 255d;
        }
        for (var y = 0; y < input.Height; y++)
        {
            for (var x = 0; x < input.Width; x++)
            {
                var pixel = y * input.Width + x;
                if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested();
                var offset = pixel * 3;
                var r = output[offset] / 255d; var g = output[offset + 1] / 255d; var b = output[offset + 2] / 255d;
                var luma = luminance[pixel];

                // Film color response: a small, named Pixel Tart bias, never a copied stock profile.
                var profile = settings.ProfileId switch { "PT-W01" => (r: .018, g: .004, b: -.012), "PT-C01" => (r: -.008, g: .002, b: .018), _ => (r: 0d, g: 0d, b: 0d) };
                var profileAmount = settings.ProfileAmount / 100;
                r += profile.r * profileAmount; g += profile.g * profileAmount; b += profile.b * profileAmount;

                // Halation: sample the local highlight envelope so warmth appears beside bright edges.
                var localHighlight = LocalHighlight(luminance, input.Width, input.Height, x, y);
                var edgeMask = Math.Clamp((localHighlight - Math.Max(.68, luma)) * 4.5, 0, 1) * Math.Clamp(settings.HalationAmount / 100, 0, 1) * Math.Clamp((luma - .28) / .45, 0, 1);
                r += edgeMask * .045; g += edgeMask * .008;

                // Bloom: neutral highlight compression, independent from halation.
                var bloom = Math.Clamp((luma - .78) / .22, 0, 1) * Math.Clamp(settings.BloomAmount / 100, 0, 1) * .055;
                r += bloom; g += bloom; b += bloom;

                // Vignette: feathered radial falloff, centered and continuous.
                var nx = (x + .5) / input.Width * 2 - 1; var ny = (y + .5) / input.Height * 2 - 1;
                var distance = Math.Clamp(Math.Sqrt(nx * nx + ny * ny) / 1.4143, 0, 1);
                var vignette = 1 - Math.Clamp(settings.VignetteAmount / 100, 0, 1) * Math.Pow(distance, 1.65) * .55;
                r *= vignette; g *= vignette; b *= vignette;

                // Luma-weighted deterministic grain. Identical seed + input produces identical output.
                var grainScale = 1 + (int)Math.Round(settings.GrainSize / 20);
                var grain = DeterministicNoise(x / grainScale, y / grainScale, settings.Seed) * Math.Clamp(settings.GrainAmount / 100, 0, 1) * .035 * (0.25 + luma * .9);
                r += grain; g += grain; b += grain;

                var surface = Surface(settings.TextureId, x, y) * Math.Clamp(settings.SurfaceAmount * settings.TextureAmount / 10000, 0, 1) * .06;
                r += surface; g += surface; b += surface;
                output[offset] = Channel(r); output[offset + 1] = Channel(g); output[offset + 2] = Channel(b);
            }
        }
        return new(input.Width, input.Height, output);
    }

    public static bool IsIdentity(PixelTartFilmSettings settings) =>
        (settings.ProfileId == "PT-N01" || settings.ProfileAmount == 0) && settings.GrainAmount == 0 &&
        settings.HalationAmount == 0 && settings.BloomAmount == 0 && settings.VignetteAmount == 0 &&
        (settings.SurfaceAmount == 0 || settings.TextureAmount == 0 || settings.TextureId == "None");

    private static double LocalHighlight(double[] luminance, int width, int height, int x, int y)
    {
        var result = luminance[y * width + x];
        for (var oy = -2; oy <= 2; oy++)
        for (var ox = -2; ox <= 2; ox++)
        {
            var sampleX = Math.Clamp(x + ox, 0, width - 1);
            var sampleY = Math.Clamp(y + oy, 0, height - 1);
            result = Math.Max(result, luminance[sampleY * width + sampleX]);
        }
        return result;
    }

    private static double DeterministicNoise(int x, int y, int seed)
    {
        unchecked { var n = x * 374761393 + y * 668265263 + seed * 1442695041; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1; }
    }
    private static double Surface(string texture, int x, int y) => texture switch
    {
        "FineFiber" => Math.Sin(x * .31 + Math.Sin(y * .07)) * .5,
        "Paper" => Math.Sin(x * .06) * Math.Sin(y * .09) * .5,
        "SoftMist" => Math.Sin((x + y) * .025) * .35,
        "Scanline" => ((y % 4) - 1.5) / 3,
        _ => 0
    };
    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
