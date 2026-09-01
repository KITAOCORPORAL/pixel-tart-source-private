using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using System.Windows;

namespace PixelTart.Modules.AssetLibrary;

internal enum AssetLibraryDropTargetKind
{
    Folder,
    Tag,
    RemoveFromCurrent,
    SmartFolder,
    Invalid
}

internal sealed record AssetLibraryDropTarget(
    AssetLibraryDropTargetKind Kind,
    Guid? TargetId,
    string Name);

internal sealed record AssetLibraryCommandPreview(
    bool IsAllowed,
    int RequestedCount,
    int ConflictCount,
    int ChangeCount,
    string Message);

internal sealed class AssetLibraryBrowserCommandService(IAssetLibraryRepository repository)
{
    private AssetLibraryUndoToken? _undoToken;
    private AssetLibraryUndoToken? _redoToken;

    public bool CanUndo => _undoToken is not null;
    public bool CanRedo => _redoToken is not null;

    public Task CopyPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.CompletedTask;
        Clipboard.SetText(path);
        return Task.CompletedTask;
    }

    public AssetItem PrepareInformationView(AssetItem asset) => asset;

    public async Task<AssetLibraryCommandPreview> PreviewDropAsync(
        IEnumerable<Guid> assetIds,
        AssetLibraryDropTarget target,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return new(false, 0, 0, 0, "没有可移动的素材。");
        if (target.Kind is AssetLibraryDropTargetKind.SmartFolder or AssetLibraryDropTargetKind.Invalid || target.TargetId is null)
            return new(false, ids.Length, 0, 0, $"“{target.Name}”不是可写入的文件夹或标签。");

        IReadOnlySet<Guid> existing = target.Kind == AssetLibraryDropTargetKind.Folder
            ? (await repository.ListFolderMembershipsAsync(folderId: target.TargetId, cancellationToken: cancellationToken).ConfigureAwait(false))
                .Select(item => item.AssetId).ToHashSet()
            : (await repository.ListTagMembershipsAsync(tagId: target.TargetId, cancellationToken: cancellationToken).ConfigureAwait(false))
                .Select(item => item.AssetId).ToHashSet();
        var conflicts = ids.Count(existing.Contains);
        return new(true, ids.Length, conflicts, ids.Length - conflicts,
            $"将 {ids.Length} 项加入“{target.Name}”：新增 {ids.Length - conflicts} 项，已存在 {conflicts} 项。");
    }

    public async Task<AssetLibraryCommandPreview> PreviewRemoveAsync(
        IEnumerable<Guid> assetIds,
        Guid targetId,
        bool folder,
        string name,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        IReadOnlySet<Guid> existing = folder
            ? (await repository.ListFolderMembershipsAsync(folderId: targetId, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(item => item.AssetId).ToHashSet()
            : (await repository.ListTagMembershipsAsync(tagId: targetId, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(item => item.AssetId).ToHashSet();
        var changes = ids.Count(existing.Contains);
        return new(changes > 0, ids.Length, ids.Length - changes, changes,
            $"从“{name}”移出 {changes} 项；不在当前归属中的 {ids.Length - changes} 项保持不变。");
    }

    public async Task<AssetLibraryBatchResult> ExecuteDropAsync(
        IEnumerable<Guid> assetIds,
        AssetLibraryDropTarget target,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToArray();
        var result = target.Kind switch
        {
            AssetLibraryDropTargetKind.Folder when target.TargetId is not null =>
                await repository.AddToFolderAsync(ids, target.TargetId.Value, cancellationToken).ConfigureAwait(false),
            AssetLibraryDropTargetKind.Tag when target.TargetId is not null =>
                await repository.AddTagsAsync(ids, [target.TargetId.Value], cancellationToken).ConfigureAwait(false),
            _ => new AssetLibraryBatchResult(0, null, ["目标不接受素材拖放。"])
        };
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> AddToFolderAsync(IEnumerable<Guid> ids, Guid folderId, CancellationToken cancellationToken = default)
    {
        var result = await repository.AddToFolderAsync(ids, folderId, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> RemoveFromFolderAsync(IEnumerable<Guid> ids, Guid folderId, CancellationToken cancellationToken = default)
    {
        var result = await repository.RemoveFromFolderAsync(ids, folderId, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> AddTagAsync(IEnumerable<Guid> ids, Guid tagId, CancellationToken cancellationToken = default)
    {
        var result = await repository.AddTagsAsync(ids, [tagId], cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> RemoveTagAsync(IEnumerable<Guid> ids, Guid tagId, CancellationToken cancellationToken = default)
    {
        var result = await repository.RemoveTagsAsync(ids, [tagId], cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> RateAsync(IEnumerable<Guid> ids, int rating, CancellationToken cancellationToken = default)
    {
        var result = await repository.UpdateAssetsMetadataAsync(ids, rating: Math.Clamp(rating, 0, 5), cancellationToken: cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> SetMissingAsync(IEnumerable<Guid> ids, bool missing, CancellationToken cancellationToken = default)
    {
        var result = await repository.SetAssetsMissingAsync(ids, missing, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> SetArchivedAsync(IEnumerable<Guid> ids, bool archived, CancellationToken cancellationToken = default)
    {
        var result = await repository.SetAssetsArchivedAsync(ids, archived, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_undoToken is null) return false;
        var token = _undoToken;
        if (!await repository.UndoAsync(token, cancellationToken).ConfigureAwait(false)) return false;
        _undoToken = null;
        _redoToken = token;
        return true;
    }

    public async Task<bool> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (_redoToken is null) return false;
        var token = _redoToken;
        if (!await repository.RedoAsync(token, cancellationToken).ConfigureAwait(false)) return false;
        _redoToken = null;
        _undoToken = token;
        return true;
    }

    private void Remember(AssetLibraryBatchResult result)
    {
        if (result.UndoToken is null) return;
        _undoToken = result.UndoToken;
        _redoToken = null;
    }
}
