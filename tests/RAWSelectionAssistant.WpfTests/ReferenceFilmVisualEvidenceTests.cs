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
    public void ReviewSheets_ComposeHonestProductionEvidenceWhenRequested()
    {
        var source = Environment.GetEnvironmentVariable("PIXEL_TART_REVIEW_SOURCE");
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_REVIEW_OUTPUT");
        var closeOutput = Environment.GetEnvironmentVariable("PIXEL_TART_CLOSE_EVIDENCE");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(closeOutput)) Assert.Inconclusive("Set review evidence paths.");
        Directory.CreateDirectory(output!); Directory.CreateDirectory(closeOutput!);
        string P(string relative) => Path.Combine(source!, relative);
        Compose(Path.Combine(output!, "01_WORKSPACE_SIMPLE_PRO.png"), 2, [("Simple", P("11_reference-color/reference_simple.png")), ("Pro", P("11_reference-color/reference_pro.png"))]);
        Compose(Path.Combine(output!, "02_WORKSPACE_RESPONSIVE.png"), 3, [("Wide", P("11_reference-color/loaded-synthetic.png")), ("Compact · Closed", P("11_reference-color/compact_drawer_closed.png")), ("Compact · Open", P("11_reference-color/compact_drawer_open.png")), ("Narrow", P("11_reference-color/narrow_canvas_first.png")), ("Focus", P("11_reference-color/focus_view.png"))]);
        Compose(Path.Combine(output!, "03_REFERENCE_COMPARE.png"), 2, [("Original", P("11_reference-color/原片.png")), ("Matched", P("11_reference-color/仿色结果.png")), ("Split", P("11_reference-color/左右对比.png")), ("Side by side", P("11_reference-color/并排对比.png"))]);
        Compose(Path.Combine(output!, "04_MULTI_REFERENCE.png"), 2, [("1 reference", P("11_reference-color/loaded-synthetic.png")), ("3 references", P("11_reference-color/multi-reference.png"))]);
        Compose(Path.Combine(output!, "05_FILM_CONTROLS.png"), 2, [("Film · Simple", P("11_reference-color/film_simple.png")), ("Film · Pro", P("11_reference-color/film_pro.png"))]);
        var textureCrop = Path.Combine(output!, "texture-grid-crop.png"); Crop(P("11_reference-color/reference_texture_grid.png"), textureCrop, new Int32Rect(180, 80, 350, 850));
        Compose(Path.Combine(output!, "06_TEXTURE_GRID.png"), 2, [("Production workspace", P("11_reference-color/reference_texture_grid.png")), ("Procedural texture tiles", textureCrop)]);
        File.Copy(Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_FILM_EVIDENCE")!, "07_FILM_EFFECTS.png"), Path.Combine(output!, "07_FILM_EFFECTS.png"), true);
        Compose(Path.Combine(output!, "08_200_DPI.png"), 2, [("100%", P("dpi/ReferenceColor-1920x1080-100.png")), ("200%", P("dpi/ReferenceColor-1920x1080-200.png"))]);
        Compose(Path.Combine(output!, "09_LOADING_ERROR.png"), 3, [("Loading", P("11_reference-color/loading.png")), ("Error + Retry", P("11_reference-color/error.png")), ("Recovered", P("11_reference-color/recovered.png"))]);
        File.Copy(P("accent-ab/10_ACCENT_AB.png"), Path.Combine(output!, "10_ACCENT_AB.png"), true);

        var closeMap = new (string Name, string Relative)[]
        {
            ("01_workbench.png", "01_workbench/default.png"), ("02_asset-library.png", "02_asset-library/default.png"),
            ("03_workflow.png", "03_ingest/default.png"), ("04_calendar.png", "04_calendar/default.png"),
            ("05_planning.png", "05_planning/default.png"), ("06_tether.png", "06_tether/default.png"),
            ("07_online-selection.png", "07_online-selection/default.png"), ("08_finance.png", "08_finance/default.png"),
            ("09_history.png", "09_history/default.png"), ("10_toolbox.png", "10_toolbox/default.png"),
            ("11_reference-color.png", "11_reference-color/default.png"), ("12_publish.png", "12_publish/default.png"),
            ("13_settings.png", "16_settings/default.png"), ("14_help.png", "18_help/default.png"),
            ("15_quick-preview.png", "02_asset-library/quick-loupe.png"), ("16_dialog.png", "05_planning/create-dialog.png")
        };
        var closeCrops = new List<(string Label, string Path)>();
        foreach (var item in closeMap)
        {
            var destination = Path.Combine(closeOutput!, item.Name); File.Copy(P(item.Relative), destination, true);
            var crop = Path.Combine(closeOutput!, "crop-" + item.Name); var image = Load(destination);
            Crop(destination, crop, new Int32Rect(Math.Max(0, image.PixelWidth - 520), Math.Min(36, Math.Max(0, image.PixelHeight - 180)), Math.Min(520, image.PixelWidth), Math.Min(180, image.PixelHeight)));
            closeCrops.Add((Path.GetFileNameWithoutExtension(item.Name), crop));
        }
        Compose(Path.Combine(closeOutput!, "GLOBAL_CLOSE_SAFE_AREA_CONTACT_SHEET.png"), 4, closeCrops);
    }

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
            if ((x - 760) * (x - 760) + (y - 300) * (y - 300) < 42 * 42) r = g = b = 255;
            if ((x - 720) * (x - 720) + (y - 420) * (y - 420) < 18 * 18) { r = 255; g = 245; b = 215; }
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

    private static BitmapSource Load(string path)
    {
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(Path.GetFullPath(path)); image.EndInit(); image.Freeze(); return image;
    }

    private static void Crop(string source, string destination, Int32Rect rectangle)
    {
        var image = Load(source); rectangle.Width = Math.Min(rectangle.Width, image.PixelWidth - rectangle.X); rectangle.Height = Math.Min(rectangle.Height, image.PixelHeight - rectangle.Y);
        var crop = new CroppedBitmap(image, rectangle); crop.Freeze(); Save(crop, destination);
    }

    private static void Compose(string destination, int columns, IEnumerable<(string Label, string Path)> items)
    {
        var frames = items.ToArray(); const int cellWidth = 720, cellHeight = 450; var rows = (int)Math.Ceiling(frames.Length / (double)columns);
        var drawing = new DrawingVisual(); using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 18, 21)), null, new Rect(0, 0, columns * cellWidth, rows * cellHeight));
            for (var index = 0; index < frames.Length; index++)
            {
                var image = Load(frames[index].Path); var x = index % columns * cellWidth + 12; var y = index / columns * cellHeight + 10;
                context.DrawText(new FormattedText(frames[index].Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 16, Brushes.White, 1), new Point(x, y));
                var scale = Math.Min(696d / image.PixelWidth, 400d / image.PixelHeight); context.DrawImage(image, new Rect(x, y + 30, image.PixelWidth * scale, image.PixelHeight * scale));
            }
        }
        var sheet = new RenderTargetBitmap(columns * cellWidth, rows * cellHeight, 96, 96, PixelFormats.Pbgra32); sheet.Render(drawing); sheet.Freeze(); Save(sheet, destination);
    }
}
