using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Only reference matching consumes source palette/statistics. Diagnostic histograms
/// are computed separately from the displayed result, never from this processing placeholder.</summary>
public static class ColorStudioProcessingAnalysis
{
    private static readonly Lazy<AssetVisualAnalysisResult> UnusedAnalysis = new(() =>
        VisualAnalysisEngine.Analyze(new(Guid.Empty, "not-used-by-non-reference-nodes", new VisualPixelBuffer(1, 1, new byte[] { 0, 0, 0 }))));

    public static bool RequiresSourceAnalysis(ColorAdjustmentStack stack, ReferenceLook? reference) =>
        reference is not null && stack.Nodes.Any(node => node.Enabled && node.Type == ColorStudioNodeType.ReferenceMatch &&
            node.NumericParameters.GetValueOrDefault("match_strength", reference.Parameters.MatchStrength) != 0);

    public static AssetVisualAnalysisResult Create(VisualPixelBuffer pixels, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!RequiresSourceAnalysis(stack, reference)) return UnusedAnalysis.Value;
        // Legacy matching statistics historically consume RGB only. Keep those semantics
        // until all palette/moment statistics support alpha together; live Studio diagnostics
        // use AnalyzeHistogram directly with alpha and correctly exclude transparent pixels.
        var opaque = pixels.Alpha.IsEmpty ? pixels : new VisualPixelBuffer(pixels.Width, pixels.Height, pixels.Rgb24);
        return VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(opaque), opaque), token);
    }
}
