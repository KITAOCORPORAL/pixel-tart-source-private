using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace RAWSelectionAssistant.Core.Services.Tethering;

// Library owns photographer metadata once linked; product DB continues to own customer fields.
public sealed class LinkedTetherAnnotationService(ITetherAnnotationService fallback,
    TetherLibraryHandoffService handoff, Func<CancellationToken, Task<AssetLibraryContainerDescriptor?>> currentLibrary) : ITetherAnnotationService
{
    public async Task<TetherAnnotationRecord?> GetAsync(Guid id, CancellationToken token = default)
    {
        var original = await fallback.GetAsync(id, token);
        var link = await handoff.ResolveLinkAsync(id, token);
        var library = await currentLibrary(token);
        if (link?.State != IdentityBridgeState.Ready || library is null) return original;
        await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
        var asset = await repository.GetAssetAsync(link.AssetId, token);
        if (asset is null) return original;
        var now = DateTimeOffset.UtcNow;
        return (original ?? new(Guid.NewGuid(), id, 0, null, null, now, now)) with
            { Rating = asset.Rating, ColorLabel = asset.ColorLabel, PhotographerNote = asset.Comment };
    }
    public async Task<IReadOnlyDictionary<Guid,TetherAnnotationRecord>> ListBySessionAsync(Guid id, CancellationToken token = default)
    {
        var values = await fallback.ListBySessionAsync(id, token); var result = new Dictionary<Guid,TetherAnnotationRecord>();
        foreach (var pair in values) result[pair.Key] = await GetAsync(pair.Key, token) ?? pair.Value;
        return result;
    }
    public async Task<TetherAnnotationSaveResult> SaveAsync(TetherAnnotationRecord annotation, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var result = await fallback.SaveAsync(annotation, projectId, cancellationToken);
        if (!result.Success) return result;
        var link = await handoff.ResolveLinkAsync(annotation.AssetId, cancellationToken);
        if (link is null) return result;
        var library = await currentLibrary(cancellationToken);
        if (library is null) return new(false, result.Annotation, "LIBRARY_OFFLINE", "摄影师标注待关联，客户备注已独立保存。");
        await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
        var item = await repository.GetAssetAsync(link.AssetId, cancellationToken);
        if (item is null) return new(false, result.Annotation, "ASSET_MISSING", "素材记录缺失，标注保留待处理。");
        if (TetherAnnotationMerge.Propose(item, annotation).Conflicts.Count != 0)
            return new(false, result.Annotation, "ANNOTATION_CONFLICT", "标注冲突：请选择保留素材库、采用联机或合并摄影师备注。");
        var error = await handoff.ResolveAnnotationAsync(annotation.AssetId, AnnotationConflictChoice.UsePhotographer, cancellationToken);
        return error is null ? result : new(false, result.Annotation, error, "标注部分完成：客户备注已保存，素材库标注待重试。");
    }
}
