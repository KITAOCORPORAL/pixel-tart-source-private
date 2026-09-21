using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Non-destructive, deterministic Pixel Tart film treatment. Film effects are pixel effects and are never baked into a 3D LUT.</summary>
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
    int Seed = 17)
{
    public void Validate()
    {
        foreach (var value in new[] { ProfileAmount, GrainAmount, GrainSize, HalationAmount, BloomAmount, VignetteAmount, SurfaceAmount, TextureAmount })
            if (!double.IsFinite(value) || value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
        if (Seed < 0) throw new ArgumentOutOfRangeException(nameof(Seed));
    }
}

public sealed record PixelTartFilmProfile(string Id, string Name);
public sealed record PixelTartFilmTextureOption(string Id, string DisplayName, string Description);

public static class PixelTartFilmProfiles
{
    public static IReadOnlyList<PixelTartFilmProfile> All { get; } =
        [new("PT-N01", "中性胶片"), new("PT-W01", "暖调柔化"), new("PT-C01", "冷银灰")];
}

public static class PixelTartFilmTextures
{
    public static IReadOnlyList<PixelTartFilmTextureOption> All { get; } =
    [
        new("None", "无", "不叠加表面材质"),
        new("FineFiber", "细纤维", "方向性细纤维"),
        new("Paper", "纸面颗粒", "低频与中频纸面变化"),
        new("SoftMist", "柔雾", "大尺度柔和雾化"),
        new("Scanline", "扫描细纹", "带轻微抖动的扫描纹理")
    ];
}

public static class PixelTartFilmPipeline
{
    public static VisualPixelBuffer Apply(VisualPixelBuffer input, PixelTartFilmSettings settings, CancellationToken token = default)
    {
        settings.Validate();
        if (!settings.Enabled || IsIdentity(settings)) return new(input.Width, input.Height, input.Rgb24.ToArray());
        var output = input.Rgb24.ToArray();
        var luminance = new float[input.PixelCount];
        for (var pixel = 0; pixel < input.PixelCount; pixel++)
        {
            var offset = pixel * 3; var r = SrgbToLinear(output[offset] / 255d); var g = SrgbToLinear(output[offset + 1] / 255d); var b = SrgbToLinear(output[offset + 2] / 255d);
            luminance[pixel] = (float)(.2126 * r + .7152 * g + .0722 * b);
        }
        var highlightMask = new float[input.PixelCount];
        for (var i = 0; i < luminance.Length; i++) highlightMask[i] = Math.Clamp((luminance[i] - .62f) / .38f, 0, 1);
        var minDimension = Math.Min(input.Width, input.Height); var bloomRadius = Math.Clamp(minDimension / 320, 2, 8);
        var bloomSpread = Blur(highlightMask, input.Width, input.Height, bloomRadius); var halationSpread = Blur(highlightMask, input.Width, input.Height, Math.Max(2, bloomRadius / 2));
        for (var y = 0; y < input.Height; y++) for (var x = 0; x < input.Width; x++)
        {
            var pixel = y * input.Width + x; if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested(); var offset = pixel * 3;
            var r = SrgbToLinear(output[offset] / 255d); var g = SrgbToLinear(output[offset + 1] / 255d); var b = SrgbToLinear(output[offset + 2] / 255d); var luma = luminance[pixel];
            var profile = settings.ProfileId switch { "PT-W01" => (r: .018, g: .004, b: -.012), "PT-C01" => (r: -.008, g: .002, b: .018), _ => (r: 0d, g: 0d, b: 0d) }; var profileAmount = settings.ProfileAmount / 100;
            r += profile.r * profileAmount; g += profile.g * profileAmount; b += profile.b * profileAmount;
            var halo = Math.Clamp(halationSpread[pixel] - highlightMask[pixel] * .72f, 0, 1) * settings.HalationAmount / 100d; var edge = halo * Math.Clamp((luma - .12f) / .7f, 0, 1); r += edge * .12; g += edge * .020;
            var bloom = bloomSpread[pixel] * settings.BloomAmount / 100d * .14; r += bloom; g += bloom; b += bloom;
            var nx = (x + .5) / input.Width * 2 - 1; var ny = (y + .5) / input.Height * 2 - 1; var distance = Math.Clamp(Math.Sqrt(nx * nx + ny * ny) / 1.4143, 0, 1); var vignette = 1 - Math.Clamp(settings.VignetteAmount / 100, 0, 1) * Math.Pow(distance, 1.65) * .55; r *= vignette; g *= vignette; b *= vignette;
            var grainScale = 1d + settings.GrainSize / 24d * 5d; var high = DeterministicNoise(x, y, settings.Seed); var low = SmoothNoise(x / grainScale, y / grainScale, settings.Seed + 101); var grain = (high * .62 + low * .38) * settings.GrainAmount / 100d * .042 * Math.Clamp(luma * .95 + .015, 0, 1) * (1 - Math.Clamp(luma - .86f, 0, .14f) * 2.5); r += grain; g += grain; b += grain;
            var surface = Surface(settings.TextureId, x, y, settings.Seed) * Math.Clamp(settings.SurfaceAmount * settings.TextureAmount / 10000, 0, 1) * .06; r += surface; g += surface; b += surface;
            output[offset] = Channel(r); output[offset + 1] = Channel(g); output[offset + 2] = Channel(b);
        }
        return new(input.Width, input.Height, output);
    }
    public static bool IsIdentity(PixelTartFilmSettings settings) => (settings.ProfileId == "PT-N01" || settings.ProfileAmount == 0) && settings.GrainAmount == 0 && settings.HalationAmount == 0 && settings.BloomAmount == 0 && settings.VignetteAmount == 0 && (settings.SurfaceAmount == 0 || settings.TextureAmount == 0 || settings.TextureId == "None");
    public static double SrgbToLinear(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
    public static double LinearToSrgb(double value) => value <= .0031308 ? value * 12.92 : 1.055 * Math.Pow(Math.Max(0, value), 1 / 2.4) - .055;
    private static float[] Blur(float[] source, int width, int height, int radius)
    {
        var kernel = new double[radius * 2 + 1]; var sigma = Math.Max(1d, radius * .55); var sum = 0d; for (var i = -radius; i <= radius; i++) { var weight = Math.Exp(-(i * i) / (2 * sigma * sigma)); kernel[i + radius] = weight; sum += weight; } for (var i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        var horizontal = new float[source.Length]; var output = new float[source.Length]; for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { double value = 0; for (var k = -radius; k <= radius; k++) value += source[y * width + Math.Clamp(x + k, 0, width - 1)] * kernel[k + radius]; horizontal[y * width + x] = (float)value; } for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { double value = 0; for (var k = -radius; k <= radius; k++) value += horizontal[Math.Clamp(y + k, 0, height - 1) * width + x] * kernel[k + radius]; output[y * width + x] = (float)value; } return output;
    }
    private static double DeterministicNoise(int x, int y, int seed) { unchecked { var n = x * 374761393 + y * 668265263 + seed * 1442695041; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1; } }
    private static double SmoothNoise(double x, double y, int seed) { var x0 = (int)Math.Floor(x); var y0 = (int)Math.Floor(y); var tx = x - x0; var ty = y - y0; var sx = tx * tx * (3 - 2 * tx); var sy = ty * ty * (3 - 2 * ty); var a = DeterministicNoise(x0, y0, seed); var b = DeterministicNoise(x0 + 1, y0, seed); var c = DeterministicNoise(x0, y0 + 1, seed); var d = DeterministicNoise(x0 + 1, y0 + 1, seed); var ab = a + (b - a) * sx; return ab + ((c + (d - c) * sx) - ab) * sy; }
    private static double Surface(string texture, int x, int y, int seed) => texture switch { "FineFiber" => SmoothNoise((x * .18) + Math.Sin(y * .03) * 1.7, y * .045, seed + 11) * .7 + SmoothNoise(x * .52, y * .11, seed + 19) * .3, "Paper" => SmoothNoise(x * .06 + Math.Sin(y * .021), y * .06 + Math.Cos(x * .017), seed + 23) * .7 + SmoothNoise(x * .18, y * .16, seed + 29) * .3, "SoftMist" => SmoothNoise(x * .022 + Math.Sin(y * .009), y * .022 + Math.Cos(x * .011), seed + 31), "Scanline" => SmoothNoise(x * .04, y * .19 + Math.Sin(x * .027) * 1.4, seed + 37) * .7 + SmoothNoise(x * .12, y * .035, seed + 41) * .3, _ => 0 };
    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(LinearToSrgb(value) * 255), 0, 255);
}
