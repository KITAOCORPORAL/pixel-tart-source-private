using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class ReferenceFilmVisualEvidenceTests
{
    [TestMethod]
    [TestCategory("VisualEvidence")]
    public void FilmEffects_WriteTruePerEffectComparisonWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_FILM_EVIDENCE");
        if (string.IsNullOrWhiteSpace(output)) Assert.Inconclusive("Set PIXEL_TART_FILM_EVIDENCE to collect review evidence.");
        Directory.CreateDirectory(output!);
        var source = Fixture(1200, 800);
        var variants = new (string File, string Label, PixelTartFilmSettings? Settings)[]
        {
            ("film_original.png", "Original", null),
            ("film_profile.png", "Profile", new(true, "PT-W01", 100)),
            ("film_grain_fine.png", "Fine Grain", new(true, GrainAmount: 88, GrainSize: 8, Seed: 17)),
            ("film_grain_coarse.png", "Coarse Grain", new(true, GrainAmount: 88, GrainSize: 92, Seed: 17)),
            ("film_halation.png", "Halation", new(true, HalationAmount: 100)),
            ("film_bloom.png", "Bloom", new(true, BloomAmount: 100)),
            ("film_vignette.png", "Vignette", new(true, VignetteAmount: 100)),
            ("film_fiber.png", "Fine Fiber", new(true, SurfaceAmount: 100, TextureId: "FineFiber", TextureAmount: 100, Seed: 17)),
            ("film_paper.png", "Paper", new(true, SurfaceAmount: 100, TextureId: "Paper", TextureAmount: 100, Seed: 17)),
            ("film_softmist.png", "Soft Mist", new(true, SurfaceAmount: 100, TextureId: "SoftMist", TextureAmount: 100, Seed: 17)),
            ("film_scanline.png", "Scanline", new(true, SurfaceAmount: 100, TextureId: "Scanline", TextureAmount: 100, Seed: 17)),
            ("film_combined.png", "Combined", new(true, "PT-W01", 72, 42, 38, 42, 30, 28, 55, "Paper", 62, 17))
        };
        var frames = new List<(string Label, BitmapSource Image)>();
        foreach (var variant in variants)
        {
            var result = variant.Settings is null ? source : PixelTartFilmPipeline.Apply(source, variant.Settings);
            var bitmap = BitmapSource.Create(result.Width, result.Height, 96, 96, PixelFormats.Rgb24, null, result.Rgb24.ToArray(), result.Width * 3);
            bitmap.Freeze(); Save(bitmap, Path.Combine(output!, variant.File)); frames.Add((variant.Label, bitmap));
        }
        var drawing = new DrawingVisual(); const int cellWidth = 500, cellHeight = 330, columns = 3;
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 18, 21)), null, new Rect(0, 0, columns * cellWidth, 4 * cellHeight));
            for (var index = 0; index < frames.Count; index++)
            {
                var x = index % columns * cellWidth; var y = index / columns * cellHeight;
                context.DrawText(new FormattedText(frames[index].Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 18, Brushes.White, 1), new Point(x + 10, y + 8));
                var crop = new CroppedBitmap(frames[index].Image, new Int32Rect(350, 235, 480, 280)); crop.Freeze();
                context.DrawImage(crop, new Rect(x + 10, y + 40, 480, 280));
            }
        }
        var sheet = new RenderTargetBitmap(columns * cellWidth, 4 * cellHeight, 96, 96, PixelFormats.Pbgra32); sheet.Render(drawing); sheet.Freeze();
        Save(sheet, Path.Combine(output!, "07_FILM_EFFECTS.png"));
    }

    private static VisualPixelBuffer Fixture(int width, int height)
    {
        var bytes = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var nx = x / (double)(width - 1); var ny = y / (double)(height - 1);
            var r = 20 + nx * 55; var g = 25 + nx * 45; var b = 32 + nx * 38;
            if (x < 210 && y > 520) { r = 18; g = 21; b = 28; }
            var skin = Math.Pow((x - 530) / 250d, 2) + Math.Pow((y - 400) / 310d, 2) < 1;
            if (skin) { var light = 1 - Math.Min(1, Math.Sqrt(Math.Pow((x - 500) / 250d, 2) + Math.Pow((y - 350) / 310d, 2))); r = 155 + light * 76; g = 91 + light * 88; b = 72 + light * 72; }
            if ((x - 940) * (x - 940) + (y - 150) * (y - 150) < 42 * 42) r = g = b = 255;
            if ((x - 1020) * (x - 1020) + (y - 250) * (y - 250) < 18 * 18) { r = 255; g = 245; b = 215; }
            if (x > 850 && y > 520) { var band = ((x / 24) + (y / 18)) % 2 == 0; r = band ? 205 : 45; g = band ? 210 : 52; b = band ? 218 : 62; }
            if (y is > 55 and < 120)
            {
                if (x is > 50 and < 220) { r = 128; g = 128; b = 128; }
                else if (x is > 240 and < 410) { r = 220; g = 40; b = 36; }
                else if (x is > 430 and < 600) { r = 40; g = 205; b = 80; }
                else if (x is > 620 and < 790) { r = 40; g = 85; b = 225; }
            }
            var offset = (y * width + x) * 3; bytes[offset] = (byte)Math.Clamp(r, 0, 255); bytes[offset + 1] = (byte)Math.Clamp(g, 0, 255); bytes[offset + 2] = (byte)Math.Clamp(b, 0, 255);
        }
        return new(width, height, bytes);
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
