using System.IO;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    public string ProjectVisualDirectory => Path.Combine(Path.GetDirectoryName(_productDatabasePath)!, "ProjectVisuals");
    public async Task SaveProjectVisualAsync(Guid projectId, VisualAnalysisSurfacePayload payload, bool tone,
        string kind = "Asset", Guid? containerId = null)
    {
        var sources = payload.Items.Select(item => new ProjectVisualSource(CanvasLibraryId, item.Asset.AssetId,
            item.Asset.ContentHash ?? item.Analysis.ContentHash, kind, containerId)).ToArray();
        var store = new ProjectVisualReferenceStore(ProjectVisualDirectory);
        if (tone)
        {
            var histogram = Enumerable.Range(0, 256).Select(bin => payload.Items.Average(item =>
                item.Analysis.HistogramLuma[bin] / (double)Math.Max(1, item.Analysis.HistogramLuma.Sum(value => (long)value)))).ToArray();
            await store.SaveToneAsync(projectId, new(payload.Aggregate.Zones, histogram, sources,
                DateTimeOffset.UtcNow, AssetVisualAnalysisResult.CurrentVersion), _lifetimeCancellation.Token);
        }
        else await store.SavePaletteAsync(projectId, new(payload.Aggregate.Palette, sources,
            DateTimeOffset.UtcNow, AssetVisualAnalysisResult.CurrentVersion), _lifetimeCancellation.Token);
    }
}
