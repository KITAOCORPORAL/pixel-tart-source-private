using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _analysisCancellation;
    private readonly List<PreviewAnalysisEntry> _analysisCache = [];
    private VisualHistogram? _previewHistogram;
    private string _analysisLabel = "尚无目标图像";
    private ColorSpaceSamplingTier _samplingTier = ColorSpaceSamplingTier.Standard;
    private double _pointSize = 2.6, _pointOpacity = .7, _selectionTolerance = .06;
    private bool _showAnalysisLuma = true, _cloudPanMode;
    public bool CloudPanMode { get => _cloudPanMode; set => SetProperty(ref _cloudPanMode, value); }
    private BitmapSource? _analysisImage;
    public Task AnalysisWork { get; private set; } = Task.CompletedTask;
    public VisualHistogram? PreviewHistogram { get => _previewHistogram; private set => SetProperty(ref _previewHistogram, value); }
    public string AnalysisLabel { get => _analysisLabel; private set => SetProperty(ref _analysisLabel, value); }
    public bool ShowAnalysisLuma { get => _showAnalysisLuma; set => SetProperty(ref _showAnalysisLuma, value); }
    public IReadOnlyList<ColorSpaceSamplingTier> SamplingTiers { get; } = Enum.GetValues<ColorSpaceSamplingTier>();
    public ColorSpaceSamplingTier SamplingTier { get => _samplingTier; set { if (SetProperty(ref _samplingTier, value)) SchedulePreviewAnalysis(); } }
    public double CloudPointSize { get => _pointSize; set => SetProperty(ref _pointSize, Math.Clamp(value, 1, 6)); }
    public double CloudPointOpacity { get => _pointOpacity; set => SetProperty(ref _pointOpacity, Math.Clamp(value, .15, 1)); }
    public double SelectionTolerance { get => _selectionTolerance; set { if (SetProperty(ref _selectionTolerance, Math.Clamp(value, .01, .15))) ClearColorSpaceHighlight(); } }
    public bool AnalysisIsOriginal => ReferenceEquals(_analysisImage, Editor.SourceImage);

    private void SchedulePreviewAnalysis()
    {
        _analysisCancellation?.Cancel();
        Interlocked.Increment(ref _colorSpaceRevision);
        PreviewHistogram = null; ColorSpaceModel = null; _colorSpaceSourceBuffer = null;
        ClearColorSpaceHighlight(); BuildColorSpaceModelCommand.RaiseCanExecuteChanged();
        AnalysisLabel = Editor.SourceImage is null ? "尚无目标图像" : "正在分析当前图像…";
        AnalysisWork = RefreshPreviewAnalysisAsync();
    }

    public async Task RefreshPreviewAnalysisAsync(CancellationToken token = default, BitmapSource? inspectedImage = null)
    {
        _analysisCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _analysisCancellation = cancellation;
        var revision = Interlocked.Increment(ref _colorSpaceRevision);
        try
        {
            // Coalesce source/result/view notifications and slider updates. WPF images are read
            // only after the current target transition completes; obsolete results never publish.
            await Task.Delay(120, cancellation.Token);
            var image = inspectedImage ?? (Editor.ShowOriginal ? Editor.SourceImage : Editor.MatchedImage ?? Editor.SourceImage);
            if (image is null || _disposed) return;
            var tier = SamplingTier;
            var entry = _analysisCache.FirstOrDefault(x => ReferenceEquals(x.Image, image) && x.Tier == tier);
            if (entry is null)
            {
                var frozen = image.IsFrozen ? image : image.CloneCurrentValue();
                if (!frozen.IsFrozen) frozen.Freeze();
                entry = await Task.Run(() =>
                {
                    var buffer = ToVisualBuffer(frozen);
                    var histogram = VisualAnalysisEngine.AnalyzeHistogram(buffer, cancellation.Token);
                    var transform = new MatchV4ResolvedTransform(new(0,0,0), [new(0,0,0),new(0,0,0),new(0,0,0)], new(), "inspection-only");
                    var model = ColorSpaceVisualizationBuilder.Build(buffer, buffer, transform, tier, cancellation.Token);
                    return new PreviewAnalysisEntry(image, tier, buffer, histogram, model);
                }, cancellation.Token);
            }
            if (_disposed || cancellation.IsCancellationRequested || revision != Volatile.Read(ref _colorSpaceRevision)) return;
            if (!_analysisCache.Contains(entry)) { _analysisCache.Add(entry); if (_analysisCache.Count > 4) _analysisCache.RemoveAt(0); }
            _analysisImage = image; _colorSpaceSourceBuffer = entry.Pixels;
            PreviewHistogram = entry.Histogram; ColorSpaceModel = entry.Model;
            AnalysisLabel = (AnalysisIsOriginal ? "目标原片" : "当前仿色预览") + $" · sRGB · {entry.Pixels.Width}×{entry.Pixels.Height} 取样";
            OnPropertyChanged(nameof(AnalysisIsOriginal)); OnPropertyChanged(nameof(HighlightImageWidth)); OnPropertyChanged(nameof(HighlightImageHeight));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException or ArgumentException)
        { if (revision == Volatile.Read(ref _colorSpaceRevision)) AnalysisLabel = "分析未完成：" + error.Message; }
        finally { if (ReferenceEquals(_analysisCancellation, cancellation)) _analysisCancellation = null; }
    }

    public async Task HighlightDisplayedImageSampleAsync(BitmapSource image, VisualRgb24 sample)
    {
        if (!ReferenceEquals(image, _analysisImage)) await RefreshPreviewAnalysisAsync(inspectedImage: image);
        if (ReferenceEquals(image, _analysisImage)) HighlightImageSample(sample);
    }
    public void HighlightToneZone(int zone)
    {
        ClearColorSpaceHighlight();
        if (_colorSpaceSourceBuffer is null || zone is < 0 or > 10) return;
        HighlightedPixels = VisualAnalysisEngine.ToneZoneMembers(_colorSpaceSourceBuffer, zone);
    }
    public IReadOnlyList<string> HistogramChannels { get; } = ["RGB", "R", "G", "B", "亮度"];
    private string _histogramChannel = "RGB";
    public string HistogramChannel { get => _histogramChannel; set => SetProperty(ref _histogramChannel, value); }
    private sealed record PreviewAnalysisEntry(BitmapSource Image, ColorSpaceSamplingTier Tier, VisualPixelBuffer Pixels, VisualHistogram Histogram, ColorSpaceVisualizationModel Model);
}
