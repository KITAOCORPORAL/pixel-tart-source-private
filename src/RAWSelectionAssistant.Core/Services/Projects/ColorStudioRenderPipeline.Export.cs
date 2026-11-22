using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Export owns only a newly rendered result. A borrowed frozen master is never disposed here.</summary>
public sealed class HighPrecisionExportResult : IDisposable
{
    private HighBitDepthImageBuffer? pixels;
    internal HighPrecisionExportResult(HighBitDepthImageBuffer pixels, bool borrowed, string path)
    { this.pixels = pixels; IsBorrowed = borrowed; Path = path; }
    public HighBitDepthImageBuffer Pixels => pixels ?? throw new ObjectDisposedException(nameof(HighPrecisionExportResult));
    public bool IsBorrowed { get; }
    public string Path { get; }
    public void Dispose()
    {
        var held = Interlocked.Exchange(ref pixels, null);
        if (!IsBorrowed) held?.Dispose();
    }
}

public sealed partial class ColorStudioRenderPipeline
{
    private static ColorAdjustmentStackNode[]? PointExportNodes(ColorAdjustmentStack normalized)
    {
        var nodes = normalized.Nodes.Where(n => n.Enabled).ToArray();
        if (normalized.Graph is not null || !nodes.All(n => StudioLayerComposite.Identity(n) && n.Type is
            ColorStudioNodeType.Preset or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.BasicTone or
            ColorStudioNodeType.ColorBalance or ColorStudioNodeType.Levels or ColorStudioNodeType.Curve or ColorStudioNodeType.SkinTone)) return null;
        // Exact legacy Preset identity predicate; other point nodes retain their validation/math.
        return nodes.Where(n => n.Type != ColorStudioNodeType.Preset ||
            Parameter(n,"exposure",0)*Math.Clamp(Parameter(n,"preset_strength",1),0,1) != 0 ||
            Parameter(n,"contrast",0)*Math.Clamp(Parameter(n,"preset_strength",1),0,1) != 0 ||
            Parameter(n,"saturation",0)*Math.Clamp(Parameter(n,"preset_strength",1),0,1) != 0).ToArray();
    }

    public HighPrecisionExportRows? TryCreatePointExportRows(HighBitDepthImageBuffer source,
        ColorAdjustmentStack stack, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); token.ThrowIfCancellationRequested();
        var nodes = PointExportNodes(stack.Normalize());
        return nodes is null ? null : new HighPrecisionExportRows(source,nodes,token);
    }

    /// <summary>Borrow a neutral input or own the legacy high-precision result, without an output display adapter.
    /// Export callers prefer TryCreatePointExportRows for supported point stacks.</summary>
    public HighPrecisionExportResult RenderForExport(HighBitDepthImageBuffer source, ReferenceLook? reference,
        ColorAdjustmentStack stack, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        token.ThrowIfCancellationRequested();
        var normalized = stack.Normalize();
        if (PointExportNodes(normalized) is { Length: 0 }) return new(source, true, "BorrowedIdentity");
        var display = ColorStudioProcessingAnalysis.RequiresSourceAnalysis(normalized,reference)
            ? source.ToVisualRgb24(token) : new VisualPixelBuffer(1,1,new byte[3]);
        var analysis = ColorStudioProcessingAnalysis.Create(display,normalized,reference,token);
        var fallback = RenderProcessing(source,analysis,reference,normalized,token,false);
        return new(fallback.Pixels, false, "OwnedLegacyProcessing");
    }
}
