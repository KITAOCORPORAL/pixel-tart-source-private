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

    public async Task<ReferenceLook> CreateProjectLookAsync(Guid projectId, string name, VisualAnalysisSurfacePayload payload,
        string kind = "Asset", Guid? containerId = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("请选择项目。", nameof(projectId));
        var weight = 1d / payload.Items.Count;
        var sources = payload.Items.Select(item => new ReferenceLookSource(CanvasLibraryId, item.Asset.AssetId,
            item.Asset.DisplayName, GetDisplaySourcePath(item.Asset), item.Asset.ContentHash ?? item.Analysis.ContentHash,
            weight, item.Analysis, kind, containerId)).ToArray();
        var now = DateTimeOffset.UtcNow;
        var look = new ReferenceLook(Guid.NewGuid(), string.IsNullOrWhiteSpace(name) ? $"项目色彩方案 {now:MMdd-HHmm}" : name,
            projectId, sources, new(), now, now).Normalize();
        await new ReferenceLookStore(ProjectVisualDirectory).SaveAsync(look, token: _lifetimeCancellation.Token);
        return look;
    }

    public Task<ReferenceLookCatalog> LoadProjectLooksAsync() =>
        new ReferenceLookStore(ProjectVisualDirectory).LoadAsync(_lifetimeCancellation.Token);

    public Task SaveProjectLookAsync(ReferenceLook look, bool makeDefault = false) =>
        new ReferenceLookStore(ProjectVisualDirectory).SaveAsync(look with { UpdatedAt = DateTimeOffset.UtcNow }, makeDefault, _lifetimeCancellation.Token);

    public Task DeleteProjectLookAsync(Guid lookId) =>
        new ReferenceLookStore(ProjectVisualDirectory).RemoveAsync(lookId, _lifetimeCancellation.Token);
}
