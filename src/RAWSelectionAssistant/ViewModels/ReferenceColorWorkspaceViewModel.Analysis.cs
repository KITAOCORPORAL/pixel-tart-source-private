using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;

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
    private OklabColor[] _analysisColors = [];
    private OklabColor? _selectedInspectionColor;
    public Task AnalysisWork { get; private set; } = Task.CompletedTask;
    public VisualHistogram? PreviewHistogram { get => _previewHistogram; private set => SetProperty(ref _previewHistogram, value); }
    public string AnalysisLabel
    {
        get => _colorSpaceSourceBuffer is { } buffer
            ? StudioLocalizationService.Current.Format("AnalysisSource", StudioLocalizationService.Current[AnalysisIsOriginal ? "TargetOriginal" : "AdjustedPreview"], buffer.Width, buffer.Height)
            : StudioLocalizationService.Current[_analysisLabel];
        private set => SetProperty(ref _analysisLabel, value);
    }
    public bool ShowAnalysisLuma { get => _showAnalysisLuma; set => SetProperty(ref _showAnalysisLuma, value); }
    public IReadOnlyList<ColorSpaceSamplingTier> SamplingTiers { get; } = Enum.GetValues<ColorSpaceSamplingTier>();
    public ColorSpaceSamplingTier SamplingTier { get => _samplingTier; set { if (SetProperty(ref _samplingTier, value)) SchedulePreviewAnalysis(); } }
    public double CloudPointSize { get => _pointSize; set => SetProperty(ref _pointSize, Finite(value, 1, 6, 2.6)); }
    public double CloudPointOpacity { get => _pointOpacity; set => SetProperty(ref _pointOpacity, Finite(value, .15, 1, .7)); }
    public double SelectionTolerance { get => _selectionTolerance; set { if (SetProperty(ref _selectionTolerance, Finite(value, .01, .15, .06))) RefreshInspectionSelection(); } }
    public bool AnalysisIsOriginal => ReferenceEquals(_analysisImage, Editor.SourceImage);

    private void SchedulePreviewAnalysis()
    {
        _analysisCancellation?.Cancel();
        Interlocked.Increment(ref _colorSpaceRevision);
        PreviewHistogram = null; ColorSpaceModel = null; _colorSpaceSourceBuffer = null; _analysisColors = [];
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
                    var cloud = ColorSpaceProxyBuilder.Build(buffer, ColorSpaceSampling.Settings(tier), cancellation.Token);
                    // This is inspection of an already rendered image, not a second Match invocation.
                    var model = new ColorSpaceVisualizationModel(cloud, cloud, cloud, [], ColorCloudMode.Source, tier, cloud.SourceFingerprint);
                    return new PreviewAnalysisEntry(image, tier, buffer, histogram, model, ColorSpaceSurface.BuildColorIndex(buffer, cancellation.Token));
                }, cancellation.Token);
            }
            if (_disposed || cancellation.IsCancellationRequested || revision != Volatile.Read(ref _colorSpaceRevision)) return;
            if (!_analysisCache.Contains(entry)) { _analysisCache.Add(entry); if (_analysisCache.Count > 4) _analysisCache.RemoveAt(0); }
            _analysisImage = image; _colorSpaceSourceBuffer = entry.Pixels; _analysisColors = entry.Colors;
            PreviewHistogram = entry.Histogram; ColorSpaceModel = entry.Model;
            AnalysisLabel = (AnalysisIsOriginal ? "目标原片" : "当前调整后预览") + $" · sRGB · {entry.Pixels.Width}×{entry.Pixels.Height} 代理；透明像素不计";
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
        StopRangeSelectionForInspection();
        ClearColorSpaceHighlight();
        if (_colorSpaceSourceBuffer is null || zone is < 0 or > 10) return;
        HighlightedPixels = VisualAnalysisEngine.ToneZoneMembers(_colorSpaceSourceBuffer, zone);
    }
    public IReadOnlyList<string> HistogramChannels { get; } = ["RGB", "R", "G", "B", "亮度"];
    private string _histogramChannel = "RGB";
    public string HistogramChannel { get => _histogramChannel; set => SetProperty(ref _histogramChannel, value); }
    private byte[] _highlightWeights = [];
    public byte[] HighlightWeights { get => _highlightWeights; private set => SetProperty(ref _highlightWeights, value); }
    private double _selectionSoftness = .35;
    public double SelectionSoftness { get => _selectionSoftness; set { if (SetProperty(ref _selectionSoftness, Finite(value, 0, 1, .35))) RefreshInspectionSelection(); } }
    private void RefreshInspectionSelection() { if (_selectedInspectionColor is { } color) HighlightColor(color); }
    public void HighlightColor(OklabColor color)
    {
        StopRangeSelectionForInspection();
        if (ColorSpaceModel is not { } model || _colorSpaceSourceBuffer is null) return;
        _selectedInspectionColor = color;
        var index = ColorSpaceLinking.FindNearest(model.Source, color);
        HighlightWeights = ColorSpaceSurface.SelectionMask(_colorSpaceSourceBuffer, _analysisColors, color, SelectionTolerance, SelectionSoftness);
        HighlightedPixels = HighlightWeights.Select((weight, pixel) => (weight, pixel)).Where(p => p.weight > 0).Select(p => p.pixel).ToArray();
        ColorSpaceSelectionChanged?.Invoke(this, new(ColorSpaceMarkerKind.SelectedCluster, index, color));
    }
    private ColorSpaceViewSettings _cloudSettings = new();
    public ColorSpaceViewSettings CloudSettings { get => _cloudSettings; private set { if (SetProperty(ref _cloudSettings, value.Normalize())) OnPropertyChanged(string.Empty); } }
    public int CloudSurfaceMode { get => CloudSettings.SurfaceMode; set => CloudSettings = CloudSettings with { SurfaceMode = value }; }
    public double CloudSurfaceOpacity { get => CloudSettings.SurfaceOpacity; set => CloudSettings = CloudSettings with { SurfaceOpacity = value }; }
    public int CloudBackground { get => CloudSettings.Background; set => CloudSettings = CloudSettings with { Background = value }; }
    public bool CloudShowGrid { get => CloudSettings.ShowGrid; set => CloudSettings = CloudSettings with { ShowGrid = value }; }
    public bool CloudShowAxes { get => CloudSettings.ShowAxes; set => CloudSettings = CloudSettings with { ShowAxes = value }; }
    public bool CloudShowGamut { get => CloudSettings.ShowGamut; set => CloudSettings = CloudSettings with { ShowGamut = value }; }
    public double CloudChromaMin { get => CloudSettings.ChromaMin; set => CloudSettings = CloudSettings with { ChromaMin = value }; }
    public double CloudChromaMax { get => CloudSettings.ChromaMax; set => CloudSettings = CloudSettings with { ChromaMax = value }; }
    public bool CloudSliceEnabled { get => CloudSettings.SliceEnabled; set => CloudSettings = CloudSettings with { SliceEnabled = value }; }
    public double CloudSliceCenter { get => CloudSettings.SliceCenter; set => CloudSettings = CloudSettings with { SliceCenter = value }; }
    public double CloudSliceThickness { get => CloudSettings.SliceThickness; set => CloudSettings = CloudSettings with { SliceThickness = value }; }
    public double CloudRotationX { get => CloudSettings.RotationX; set => CloudSettings = CloudSettings with { RotationX = value }; }
    public double CloudRotationY { get => CloudSettings.RotationY; set => CloudSettings = CloudSettings with { RotationY = value }; }
    public double CloudRotationZ { get => CloudSettings.RotationZ; set => CloudSettings = CloudSettings with { RotationZ = value }; }
    private static double Finite(double value, double min, double max, double fallback) => Math.Clamp(double.IsFinite(value) ? value : fallback, min, max);
    private sealed record PreviewAnalysisEntry(BitmapSource Image, ColorSpaceSamplingTier Tier, VisualPixelBuffer Pixels, VisualHistogram Histogram, ColorSpaceVisualizationModel Model, OklabColor[] Colors);
}
