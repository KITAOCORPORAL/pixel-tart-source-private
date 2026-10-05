using System.ComponentModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _rangeSelectionCancellation;
    private long _rangeSelectionRevision;
    private long _rangeSampleRevision;
    private bool _rangeSelectionOwnsOverlay;
    public Task RangeSelectionWork { get; private set; } = Task.CompletedTask;
    public string RangeSelectionStage { get; private set; } = "";
    private void InitializeRangeSelection() => Editor.PropertyChanged += OnRangeSelectionStateChanged;
    private void OnRangeSelectionStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TetherReferenceModeViewModel.IsSampling) && !Editor.IsSampling
            || args.PropertyName == nameof(TetherReferenceModeViewModel.SourceImage)) Interlocked.Increment(ref _rangeSampleRevision);
        if (args.PropertyName is nameof(TetherReferenceModeViewModel.ShowSelection) or nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode)
            or nameof(TetherReferenceModeViewModel.AdjustmentStack) or nameof(TetherReferenceModeViewModel.MatchedImage)
            or nameof(TetherReferenceModeViewModel.SourceImage) or nameof(TetherReferenceModeViewModel.EffectiveViewMode))
        {
            ClearRangeOwnedOverlay();
            RangeSelectionWork = RefreshRangeSelectionAsync();
        }
    }
    public async Task RefreshRangeSelectionAsync(CancellationToken token = default)
    {
        _rangeSelectionCancellation?.Cancel(); var revision = Interlocked.Increment(ref _rangeSelectionRevision);
        if (!Editor.ShowSelection || Editor.SelectedAdjustmentNode is not { Type: ColorStudioNodeType.ColorRange } || Editor.SourceImage is null)
        { ClearRangeOwnedOverlay(); return; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token); _rangeSelectionCancellation = cancellation;
        try
        {
            await Task.Delay(140, cancellation.Token);
            // Diagnostics continue to inspect processed pixels, never this colored mask.
            await AnalysisWork;
            if (_disposed || cancellation.IsCancellationRequested || revision != Volatile.Read(ref _rangeSelectionRevision)) return;
            if (Editor.SourceImage is not { } source || Editor.SelectedAdjustmentNode is not { Type: ColorStudioNodeType.ColorRange } node) return;
            var stack = Editor.AdjustmentStack.DeepClone(); var look = Editor.SelectedLook;
            var frozen = source.IsFrozen ? source : source.CloneCurrentValue(); if (!frozen.IsFrozen) frozen.Freeze();
            var rawInput = Editor.RangeSelectionRawInput; var width = HighlightImageWidth; var height = HighlightImageHeight;
            if (width < 1 || height < 1) return;
            var result = await Task.Run(() =>
            {
                var input = RangeNodeInput(frozen, rawInput, stack, look, node.Id, width, height, cancellation.Token);
                var weights = input.Processing is null
                    ? ColorStudioRenderPipeline.SelectionWeights(input.Pixels, node, cancellation.Token)
                    : ColorStudioRenderPipeline.SelectionWeights(input.Processing, node, cancellation.Token);
                for (var p = 0; p < weights.Length; p++) if (!input.Pixels.Alpha.IsEmpty && input.Pixels.Alpha.Span[p] == 0) weights[p] = 0;
                return weights;
            }, cancellation.Token);
            if (_disposed || cancellation.IsCancellationRequested || revision != Volatile.Read(ref _rangeSelectionRevision) || !Editor.ShowSelection || !ReferenceEquals(source, Editor.SourceImage)) return;
            if (_colorSpaceSourceBuffer is null || result.Length != _colorSpaceSourceBuffer.PixelCount) return;
            HighlightWeights = result; HighlightedPixels = result.Select((weight, pixel) => (weight, pixel)).Where(p => p.weight > 0).Select(p => p.pixel).ToArray();
            _rangeSelectionOwnsOverlay = true;
            RangeSelectionStage = "颜色选区 · 所选节点输入（取样代理）；直方图仍为当前显示图像"; OnPropertyChanged(nameof(RangeSelectionStage));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException or ArgumentException)
        { if (revision == Volatile.Read(ref _rangeSelectionRevision)) { RangeSelectionStage = "颜色选区未完成：" + error.Message; OnPropertyChanged(nameof(RangeSelectionStage)); } }
        finally { if (ReferenceEquals(_rangeSelectionCancellation, cancellation)) _rangeSelectionCancellation = null; }
    }
    private sealed record RangeNodeInputResult(VisualPixelBuffer Pixels, HighBitDepthImageBuffer? Processing);
    private static RangeNodeInputResult RangeNodeInput(BitmapSource frozen, HighBitDepthImageBuffer? rawInput,
        ColorAdjustmentStack stack, ReferenceLook? look, Guid selectedId, int width, int height, CancellationToken token)
    {
        var index = stack.Nodes.ToList().FindIndex(n => n.Id == selectedId);
        if (index < 0) throw new ArgumentException("颜色范围节点已移除。");
        var prior = stack.Nodes.Take(index).ToArray(); var before = stack with { Nodes = prior };
        if (rawInput is not null)
        {
            // Preserve RAW float values. Center-sampling is a declared bounded inspection
            // proxy, not a claim that spatial filters equal full-resolution pixels.
            var samples = new float[width * height * 3];
            for (var y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
                var sy = Math.Min(rawInput.Height - 1, (int)((y + .5) * rawInput.Height / height));
                for (var x = 0; x < width; x++)
                {
                    var sx = Math.Min(rawInput.Width - 1, (int)((x + .5) * rawInput.Width / width));
                    rawInput.Rgb32.Span.Slice((sy * rawInput.Width + sx) * 3, 3).CopyTo(samples.AsSpan((y * width + x) * 3, 3));
                }
            }
            var input = new HighBitDepthImageBuffer(width, height, samples);
            if (prior.Length > 0)
            {
                var analysis = ColorStudioProcessingAnalysis.Create(input.ToVisualRgb24(), before, look, token);
                input = new ColorStudioRenderPipeline().Render(input, analysis, look, before, token, captureNodeDiagnostics: false).ProcessingPixels!;
            }
            return new(input.ToVisualRgb24(), input);
        }
        var displayInput = ToVisualBuffer(frozen);
        if (prior.Length == 0) return new(displayInput, null);
        var sourceAnalysis = ColorStudioProcessingAnalysis.Create(displayInput, before, look, token);
        var rendered = new ColorStudioRenderPipeline().Render(displayInput, sourceAnalysis, look, before, token, captureNodeDiagnostics: false);
        return new(new(rendered.Pixels.Width, rendered.Pixels.Height, rendered.Pixels.Rgb24, displayInput.Alpha), rendered.ProcessingPixels);
    }
    /// <summary>Translate a displayed photo coordinate to the selected node's input.
    /// Taking the displayed, already shifted color would change the range on a second pick.</summary>
    public async Task<bool> CompleteRangeSampleAsync(double normalizedX, double normalizedY, CancellationToken token = default)
    {
        var sampleRevision = Interlocked.Increment(ref _rangeSampleRevision);
        if (!double.IsFinite(normalizedX) || !double.IsFinite(normalizedY) || Editor.SourceImage is not { } source
            || Editor.SelectedAdjustmentNode is not { Type: ColorStudioNodeType.ColorRange } node) return false;
        var currentStack = Editor.AdjustmentStack; var stack = currentStack.DeepClone(); var look = Editor.SelectedLook;
        var frozen = source.IsFrozen ? source : source.CloneCurrentValue(); if (!frozen.IsFrozen) frozen.Freeze();
        var rawInput = Editor.RangeSelectionRawInput;
        var scale = Math.Min(1, 768d / Math.Max(source.PixelWidth, source.PixelHeight));
        var width = Math.Max(1, (int)Math.Round(source.PixelWidth * scale)); var height = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
        try
        {
            await Task.Yield(); // Allow an immediate Esc to cancel before expensive prefix work begins.
            if (sampleRevision != Volatile.Read(ref _rangeSampleRevision)) return false;
            var sample = await Task.Run<VisualRgb24?>(() =>
            {
                var input = RangeNodeInput(frozen, rawInput, stack, look, node.Id, width, height, token).Pixels;
                var x = Math.Clamp((int)(normalizedX * input.Width), 0, input.Width - 1);
                var y = Math.Clamp((int)(normalizedY * input.Height), 0, input.Height - 1); var p = y * input.Width + x;
                if (!input.Alpha.IsEmpty && input.Alpha.Span[p] == 0) return null;
                return new(input.Rgb24.Span[p * 3], input.Rgb24.Span[p * 3 + 1], input.Rgb24.Span[p * 3 + 2]);
            }, token);
            if (_disposed || token.IsCancellationRequested || sampleRevision != Volatile.Read(ref _rangeSampleRevision) || !ReferenceEquals(source, Editor.SourceImage)
                || !ReferenceEquals(currentStack, Editor.AdjustmentStack) || Editor.SelectedAdjustmentNode?.Id != node.Id || sample is null) return false;
            Editor.CompleteDisplayedSample(sample.Value);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException or ArgumentException)
        { RangeSelectionStage = "取样未完成：" + error.Message; OnPropertyChanged(nameof(RangeSelectionStage)); return false; }
    }
    public void ClearPreviewSelection()
    {
        Interlocked.Increment(ref _rangeSampleRevision);
        _rangeSelectionCancellation?.Cancel(); Interlocked.Increment(ref _rangeSelectionRevision);
        Editor.ShowSelection = false; ClearRangeOwnedOverlay(); ClearColorSpaceHighlight();
    }
    private void StopRangeSelectionForInspection()
    {
        _rangeSelectionCancellation?.Cancel(); Interlocked.Increment(ref _rangeSelectionRevision);
        Editor.ShowSelection = false; ClearRangeOwnedOverlay();
    }
    private void ClearRangeOwnedOverlay()
    {
        if (_rangeSelectionOwnsOverlay) { HighlightedPixels = []; HighlightWeights = []; _rangeSelectionOwnsOverlay = false; }
        if (RangeSelectionStage.Length > 0) { RangeSelectionStage = ""; OnPropertyChanged(nameof(RangeSelectionStage)); }
    }
    private void CancelRangeSelection()
    {
        Interlocked.Increment(ref _rangeSampleRevision);
        Editor.PropertyChanged -= OnRangeSelectionStateChanged; _rangeSelectionCancellation?.Cancel(); Interlocked.Increment(ref _rangeSelectionRevision);
    }
}

