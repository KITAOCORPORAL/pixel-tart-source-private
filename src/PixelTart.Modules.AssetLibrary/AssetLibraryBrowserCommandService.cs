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
    string Name,
    bool IsArchived = false);

internal sealed record AssetLibraryCommandPreview(
    bool IsAllowed,
    int RequestedCount,
    int ConflictCount,
    int ChangeCount,
    string Message,
    string? FailureCode = null,
    string? ExceptionType = null,
    bool WasCanceled = false)
{
    /// <summary>
    /// A stable alias used by acceptance evidence.  The message remains human readable,
    /// while this code lets callers distinguish a rejected target from an async failure.
    /// </summary>
    public string? ErrorCode => FailureCode;

    public bool IsCanceled => WasCanceled;
}

internal sealed class AssetLibraryBrowserCommandService(IAssetLibraryRepository repository)
{
    private AssetLibraryUndoToken? _undoToken;
    private AssetLibraryUndoToken? _redoToken;

    public bool CanUndo => _undoToken is not null;
    public bool CanRedo => _redoToken is not null;
    public AssetLibraryUndoToken? UndoToken => _undoToken;

    /// <summary>
    /// Restores the visible one-step undo/redo state from the durable journal.
    /// The repository is the source of truth, so a newly-created ViewModel keeps
    /// the same commands available after an application restart.
    /// </summary>
    public async Task RestoreFromJournalAsync(CancellationToken cancellationToken = default)
    {
        var entries = await repository.ListUndoJournalAsync(100, cancellationToken).ConfigureAwait(false);
        var latestActive = entries.FirstOrDefault(entry => !entry.IsUndone);
        var latestUndone = entries.Where(entry => entry.IsUndone)
            .OrderByDescending(entry => entry.UndoneAt)
            .ThenByDescending(entry => entry.Token.CreatedAt)
            .FirstOrDefault();
        _undoToken = latestActive?.Token;
        _redoToken = latestUndone is not null &&
                     entries.Where(entry => !entry.IsUndone)
                         .All(entry => entry.Token.CreatedAt <= latestUndone.Token.CreatedAt)
            ? latestUndone.Token
            : null;
    }

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
        var ids = NormalizeIds(assetIds);
        if (ids.Length == 0)
            return new(false, 0, 0, 0, "没有可移动的素材。", "empty-selection");

        var targetValidation = await ValidateDropTargetAsync(target, cancellationToken).ConfigureAwait(false);
        if (!targetValidation.IsAllowed)
            return new(false, ids.Length, 0, 0, targetValidation.Message, targetValidation.FailureCode);

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
        var ids = NormalizeIds(assetIds);
        if (ids.Length == 0)
            return new(false, 0, 0, 0, "没有可移出的素材。", "empty-selection");
        var targetValidation = await ValidateDropTargetAsync(
            new(folder ? AssetLibraryDropTargetKind.Folder : AssetLibraryDropTargetKind.Tag, targetId, name),
            cancellationToken).ConfigureAwait(false);
        if (!targetValidation.IsAllowed)
            return new(false, ids.Length, 0, 0, targetValidation.Message, targetValidation.FailureCode);

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
        var ids = NormalizeIds(assetIds);
        var preview = await PreviewDropAsync(ids, target, cancellationToken).ConfigureAwait(false);
        if (!preview.IsAllowed)
            return new AssetLibraryBatchResult(0, null, [preview.Message]);

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
        var validation = await ValidateDropTargetAsync(new(AssetLibraryDropTargetKind.Folder, folderId, "目标文件夹"), cancellationToken).ConfigureAwait(false);
        if (!validation.IsAllowed) return new(0, null, [validation.Message]);
        var result = await repository.AddToFolderAsync(ids, folderId, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> RemoveFromFolderAsync(IEnumerable<Guid> ids, Guid folderId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDropTargetAsync(new(AssetLibraryDropTargetKind.Folder, folderId, "目标文件夹"), cancellationToken).ConfigureAwait(false);
        if (!validation.IsAllowed) return new(0, null, [validation.Message]);
        var result = await repository.RemoveFromFolderAsync(ids, folderId, cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> AddTagAsync(IEnumerable<Guid> ids, Guid tagId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDropTargetAsync(new(AssetLibraryDropTargetKind.Tag, tagId, "目标标签"), cancellationToken).ConfigureAwait(false);
        if (!validation.IsAllowed) return new(0, null, [validation.Message]);
        var result = await repository.AddTagsAsync(ids, [tagId], cancellationToken).ConfigureAwait(false);
        Remember(result);
        return result;
    }

    public async Task<AssetLibraryBatchResult> RemoveTagAsync(IEnumerable<Guid> ids, Guid tagId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDropTargetAsync(new(AssetLibraryDropTargetKind.Tag, tagId, "目标标签"), cancellationToken).ConfigureAwait(false);
        if (!validation.IsAllowed) return new(0, null, [validation.Message]);
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
        await RestoreFromJournalAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (_redoToken is null) return false;
        var token = _redoToken;
        if (!await repository.RedoAsync(token, cancellationToken).ConfigureAwait(false)) return false;
        await RestoreFromJournalAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void Remember(AssetLibraryBatchResult result)
    {
        if (result.UndoToken is null) return;
        _undoToken = result.UndoToken;
        _redoToken = null;
    }

    internal void RememberExternalResult(AssetLibraryBatchResult result) => Remember(result);

    private async Task<DropTargetValidation> ValidateDropTargetAsync(
        AssetLibraryDropTarget? target,
        CancellationToken cancellationToken)
    {
        if (target is null)
            return new(false, "拖放目标无效，已拒绝操作。", "invalid-target");
        if (target.IsArchived)
            return new(false, $"“{DisplayName(target)}”已归档，不能接收素材。", "archived-target");
        if (target.Kind is AssetLibraryDropTargetKind.SmartFolder or AssetLibraryDropTargetKind.Invalid)
            return new(false, $"“{DisplayName(target)}”不是可写入的文件夹或标签。", "unsupported-target");
        if (target.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent)
            return new(false, "当前视图不是可写入的文件夹或标签目标。", "unsupported-target");
        if (target.TargetId is not Guid targetId || targetId == Guid.Empty)
            return new(false, $"“{DisplayName(target)}”缺少有效目标编号，已拒绝拖放。", "invalid-target");

        if (target.Kind == AssetLibraryDropTargetKind.Folder)
        {
            var folder = (await repository.ListFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(item => item.FolderId == targetId);
            if (folder is null)
                return new(false, $"目标文件夹“{DisplayName(target)}”不存在，已拒绝拖放。", "missing-target");
            if (folder.IsArchived)
                return new(false, $"目标文件夹“{folder.Name}”已归档，不能接收素材。", "archived-target");
            return new(true, string.Empty, null);
        }

        if (target.Kind == AssetLibraryDropTargetKind.Tag)
        {
            var tag = (await repository.ListTagsAsync(includeArchived: true, cancellationToken: cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(item => item.TagId == targetId);
            if (tag is null)
                return new(false, $"目标标签“{DisplayName(target)}”不存在，已拒绝拖放。", "missing-target");
            if (tag.IsArchived)
                return new(false, $"目标标签“{tag.Name}”已归档，不能接收素材。", "archived-target");
            return new(true, string.Empty, null);
        }

        return new(false, "拖放目标无效，已拒绝操作。", "invalid-target");
    }

    private static Guid[] NormalizeIds(IEnumerable<Guid>? ids) =>
        ids?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];

    private static string DisplayName(AssetLibraryDropTarget target) =>
        string.IsNullOrWhiteSpace(target.Name) ? "未命名目标" : target.Name.Trim();

    private sealed record DropTargetValidation(bool IsAllowed, string Message, string? FailureCode);
}
