using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

public sealed class ReferenceLookPreviewService
{
    private readonly ReferenceLookMatcher _matcher = new();
    public Task<BitmapSource> RenderAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var input = HistogramService.EnsureBgra32(source); var stride = input.PixelWidth * 4;
        var bgra = new byte[stride * input.PixelHeight]; input.CopyPixels(bgra, stride, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < input.PixelWidth * input.PixelHeight; pixel++)
        { rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4]; }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer));
        var result = _matcher.Match(buffer, analysis, look, token).Preview;
        for (var pixel = 0; pixel < input.PixelWidth * input.PixelHeight; pixel++)
        { bgra[pixel * 4] = result.Rgb24.Span[pixel * 3 + 2]; bgra[pixel * 4 + 1] = result.Rgb24.Span[pixel * 3 + 1]; bgra[pixel * 4 + 2] = result.Rgb24.Span[pixel * 3]; }
        var image = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, stride);
        image.Freeze(); return image;
    }, token);
}
