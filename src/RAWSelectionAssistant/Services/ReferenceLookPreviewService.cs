using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

public interface IReferenceRenderBackend
{
    Task<ReferenceLookPreviewRenderResult> RenderWithResultAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default);
    Task<ReferenceCubeLut> BuildExportLutAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default);
    Task<BitmapSource> ApplyFilmAsync(BitmapSource source, PixelTartFilmSettings settings, CancellationToken token = default);
    Task<ReferenceLookSource> AnalyzeExternalReferenceAsync(string path, CancellationToken token = default);
}

/// <summary>Current CPU reference renderer behind a replaceable backend boundary.</summary>
public sealed class ReferenceLookPreviewService : IReferenceRenderBackend
{
    private readonly ReferenceLookMatcher _matcher = new();
    private readonly ConcurrentDictionary<ReferenceFileIdentity, Lazy<Task<ReferenceLookSource>>> _referenceAnalysis = new();
    private long _referenceHits, _referenceMisses, _referenceExecutions, _referenceAnalysisTicks;
    public ReferenceAnalysisCacheStats ReferenceCacheStats => new(
        Interlocked.Read(ref _referenceHits), Interlocked.Read(ref _referenceMisses),
        Interlocked.Read(ref _referenceExecutions), TimeSpan.FromTicks(Interlocked.Read(ref _referenceAnalysisTicks)));
    public async Task<BitmapSource> RenderAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default) =>
        (await RenderWithResultAsync(source, look, token).ConfigureAwait(false)).Image;
    public Task<ReferenceLookPreviewRenderResult> RenderWithResultAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var (input, bgra, stride, buffer, analysis) = Prepare(source);
        var target = ReferenceColorTargetBuilder.FromLook(look); var current = ReferenceColorTargetBuilder.FromPixels(buffer);
        if (look.Parameters.MatchStrength > 0 && (look.Parameters.ToneStrength > 0 || look.Parameters.ColorStrength > 0))
        {
            var transform = _matcher.BuildTransform(buffer, analysis, look);
            var previewLut = ReferenceCubeLutBuilder.Build(33, transform.Apply, transform.Pipeline);
            for (var pixel = 0; pixel < buffer.PixelCount; pixel++)
            {
                if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested(); var offset = pixel * 3;
                var sample = previewLut.Sample(buffer.Rgb24.Span[offset] / 255d, buffer.Rgb24.Span[offset + 1] / 255d, buffer.Rgb24.Span[offset + 2] / 255d);
                bgra[pixel * 4] = ToByte(sample.B); bgra[pixel * 4 + 1] = ToByte(sample.G); bgra[pixel * 4 + 2] = ToByte(sample.R);
            }
        }
        var image = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, stride);
        image.Freeze(); return new ReferenceLookPreviewRenderResult(image, ReferenceDifferenceAnalyzer.Compare(current, target).UserMessage);
    }, token);

    public Task<ReferenceCubeLut> BuildExportLutAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested(); var (_, _, _, buffer, analysis) = Prepare(source);
        var transform = _matcher.BuildTransform(buffer, analysis, look);
        return ReferenceCubeLutBuilder.Build(65, transform.Apply, transform.Pipeline);
    }, token);

    public Task<BitmapSource> ApplyFilmAsync(BitmapSource source, PixelTartFilmSettings settings, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var input = HistogramService.EnsureBgra32(source);
        var stride = input.PixelWidth * 4;
        var bgra = new byte[stride * input.PixelHeight]; input.CopyPixels(bgra, stride, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < input.PixelWidth * input.PixelHeight; pixel++)
        { var offset = pixel * 4; var rgbOffset = pixel * 3; rgb[rgbOffset] = bgra[offset + 2]; rgb[rgbOffset + 1] = bgra[offset + 1]; rgb[rgbOffset + 2] = bgra[offset]; }
        var result = PixelTartFilmPipeline.Apply(new(input.PixelWidth, input.PixelHeight, rgb), settings, token);
        for (var pixel = 0; pixel < result.PixelCount; pixel++)
        { var offset = pixel * 4; var rgbOffset = pixel * 3; bgra[offset] = result.Rgb24.Span[rgbOffset + 2]; bgra[offset + 1] = result.Rgb24.Span[rgbOffset + 1]; bgra[offset + 2] = result.Rgb24.Span[rgbOffset]; bgra[offset + 3] = 255; }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, stride); output.Freeze(); return output;
    }, token);

    public async Task<ReferenceLookSource> AnalyzeExternalReferenceAsync(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path); var file = new FileInfo(fullPath);
        var identity = new ReferenceFileIdentity(fullPath, file.Length, file.LastWriteTimeUtc.Ticks);
        var candidate = new Lazy<Task<ReferenceLookSource>>(() => AnalyzeReferenceUncachedAsync(fullPath));
        var cached = _referenceAnalysis.GetOrAdd(identity, candidate);
        if (ReferenceEquals(candidate, cached)) Interlocked.Increment(ref _referenceMisses);
        else Interlocked.Increment(ref _referenceHits);
        try { return await cached.Value.WaitAsync(token).ConfigureAwait(false); }
        catch when (cached.IsValueCreated && cached.Value.IsFaulted)
        { _referenceAnalysis.TryRemove(new KeyValuePair<ReferenceFileIdentity, Lazy<Task<ReferenceLookSource>>>(identity, cached)); throw; }
    }

    private async Task<ReferenceLookSource> AnalyzeReferenceUncachedAsync(string path)
    {
        Interlocked.Increment(ref _referenceExecutions);
        var watch = Stopwatch.StartNew();
        try
        {
            var image = await BitmapFileLoader.LoadAsync(path, 2048, CancellationToken.None).ConfigureAwait(false);
            return await Task.Run(() =>
            {
                var (_, _, _, buffer, analysis) = Prepare(image);
                return new ReferenceLookSource(Guid.Empty, analysis.AssetId, Path.GetFileNameWithoutExtension(path), path,
                    VisualAnalysisFingerprint.Compute(buffer), 1, analysis, "External");
            }).ConfigureAwait(false);
        }
        finally { Interlocked.Add(ref _referenceAnalysisTicks, watch.Elapsed.Ticks); }
    }

    private readonly record struct ReferenceFileIdentity(string Path, long Length, long ModifiedTicks);

    private static (BitmapSource Input, byte[] Bgra, int Stride, VisualPixelBuffer Buffer, AssetVisualAnalysisResult Analysis) Prepare(BitmapSource source)
    {
        var input = HistogramService.EnsureBgra32(source); var stride = input.PixelWidth * 4;
        var bgra = new byte[stride * input.PixelHeight]; input.CopyPixels(bgra, stride, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < input.PixelWidth * input.PixelHeight; pixel++)
        { rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4]; }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        return (input, bgra, stride, buffer, VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer)));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
public sealed record ReferenceAnalysisCacheStats(long Hits, long Misses, long Executions, TimeSpan AnalysisTime);
public sealed record ReferenceLookPreviewRenderResult(BitmapSource Image, string? DifferenceWarning);
