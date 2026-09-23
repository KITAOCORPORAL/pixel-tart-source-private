using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

/// <summary>WPF pixel adapter for the shared headless stack; both preview and existing export call this.</summary>
public static class ColorStudioBitmapRenderer
{
    public static BitmapSource Render(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token = default)
    {
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4];
        input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
        }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer));
        var result = new ColorStudioRenderPipeline().Render(buffer, analysis, reference, stack, token).Pixels;
        for (var pixel = 0; pixel < result.PixelCount; pixel++)
        {
            bgra[pixel * 4] = result.Rgb24.Span[pixel * 3 + 2];
            bgra[pixel * 4 + 1] = result.Rgb24.Span[pixel * 3 + 1];
            bgra[pixel * 4 + 2] = result.Rgb24.Span[pixel * 3];
        }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze();
        return output;
    }

    public static BitmapSource ShowSelection(BitmapSource source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4];
        input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
        }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var result = new ColorStudioRenderPipeline().ShowSelection(buffer, node, token);
        for (var pixel = 0; pixel < result.PixelCount; pixel++)
        {
            bgra[pixel * 4] = result.Rgb24.Span[pixel * 3 + 2];
            bgra[pixel * 4 + 1] = result.Rgb24.Span[pixel * 3 + 1];
            bgra[pixel * 4 + 2] = result.Rgb24.Span[pixel * 3];
        }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze();
        return output;
    }
}
