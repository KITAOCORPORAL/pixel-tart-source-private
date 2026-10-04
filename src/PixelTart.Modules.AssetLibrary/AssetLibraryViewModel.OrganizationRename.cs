namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryViewModel
{
    internal async Task RenameOrganizationAsync(object node, string name)
    {
        name = name.Trim();
        if (name.Length == 0) throw new ArgumentException("名称不能为空。");
        // Keep stable IDs: saved queries and asset memberships remain attached to the same entity.
        switch (node)
        {
            case AssetLibraryFolderNodeView folder:
                RememberBrowserMutationResult(await _repository.RenameFolderAsync(folder.FolderId, name, _lifetimeCancellation.Token));
                break;
            case AssetLibraryTagNodeView tag:
                RememberP3MetadataResult(await _repository.RenameTagAsync(tag.Tag.TagId, name, _lifetimeCancellation.Token));
                break;
            case AssetLibraryTagGroupNodeView { Group: { } group }:
                await _repository.SaveTagGroupAsync(group with { Name = name }, _lifetimeCancellation.Token);
                break;
            default: throw new InvalidOperationException("该分组是系统入口，不能重命名。");
        }
        await RefreshFilterListsAsync(_lifetimeCancellation.Token);
        // Refresh the selected projection by identity without changing its query scope.
        _selectedFolder = Folders.FirstOrDefault(x => x.FolderId == _selectedFolder?.FolderId);
        _selectedTag = Tags.FirstOrDefault(x => x.TagId == _selectedTag?.TagId);
        OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag));
        await LoadP3TagManagerAsync();
        await RefreshAsync();
        await RefreshSelectionSummaryAsync();
        Status = $"已重命名为“{name}”。";
    }
}
