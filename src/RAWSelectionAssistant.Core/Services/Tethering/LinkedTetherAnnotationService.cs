using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace RAWSelectionAssistant.Core.Services.Tethering;

// Library owns photographer metadata once linked; product DB continues to own customer fields.
public sealed class LinkedTetherAnnotationService(ITetherAnnotationService fallback,
    TetherLibraryHandoffService handoff, Func<CancellationToken, Task<AssetLibraryContainerDescriptor?>> currentLibrary) : ITetherAnnotationService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PhotographerAnnotation> _readVersions = new();
    public async Task<TetherAnnotationRecord?> GetAsync(Guid id, CancellationToken token = default)
    {
        var original = await fallback.GetAsync(id, token);
        var link = await handoff.ResolveLinkAsync(id, token);
        var library = await currentLibrary(token);
        if (link?.State != IdentityBridgeState.Ready || library is null) return original;
        await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
        var asset = await repository.GetAssetAsync(link.AssetId, token);
        if (asset is null) return original;
        var snapshot=await new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath)).ReadAsync(link.AssetId,token);
        _readVersions[id]=snapshot;
        var now = DateTimeOffset.UtcNow;
        return (original ?? new(Guid.NewGuid(), id, 0, null, null, now, now)) with
            { Rating = snapshot.Rating, ColorLabel = snapshot.Color, PhotographerNote = snapshot.Note, ExpectedLibraryRevision=snapshot.Revision };
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
        try
        {
        var link = await handoff.ResolveLinkAsync(annotation.AssetId, cancellationToken);
        if (link is null) return result;
        var library = await currentLibrary(cancellationToken);
        if (library is null) return new(false, result.Annotation, "LIBRARY_OFFLINE", "摄影师标注待关联，客户备注已独立保存。");
        await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
        var item = await repository.GetAssetAsync(link.AssetId, cancellationToken);
        if (item is null) return new(false, result.Annotation, "ASSET_MISSING", "素材记录缺失，标注保留待处理。");
        var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath));
        var expected=await store.ReadAsync(link.AssetId,cancellationToken);
        if(annotation.ExpectedLibraryRevision is not long revision || expected.Revision!=revision)return new(false,result.Annotation,"ANNOTATION_CONCURRENT_CONFLICT","标注版本过期或缺失，产品侧草稿已保留，请重新读取后选择冲突处理。");
        var write=await store.CompareExchangeAsync(expected,new(link.AssetId,annotation.Rating,annotation.ColorLabel??"",annotation.PhotographerNote??""),cancellationToken);
        if(!write.Applied)return new(false,result.Annotation,"ANNOTATION_CONCURRENT_CONFLICT","标注版本已改变，产品侧内容保留待处理，请重新读取后明确解决冲突。");
        _readVersions[annotation.AssetId]=write.Current; return result with { Annotation = annotation with {ExpectedLibraryRevision=write.Current.Revision} };
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {return new(false,result.Annotation,"ANNOTATION_PENDING_RETRY","产品侧标注已保存，素材侧写入未完成；请恢复素材库后重试。");}
    }
}
