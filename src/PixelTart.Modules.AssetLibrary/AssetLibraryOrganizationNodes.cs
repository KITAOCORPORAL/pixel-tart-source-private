using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibrarySystemCollectionView
{
    internal AssetLibrarySystemCollectionView(AssetLibraryViewModel owner, AssetLibrarySystemCollection collection, string label, string description, string automationId, bool isEnabled = true)
    {
        Collection = collection;
        Label = label;
        Description = description;
        AutomationId = automationId;
        IsEnabled = isEnabled;
        SelectCommand = new(() => owner.SelectSystemCollection(collection), () => isEnabled);
    }

    public AssetLibrarySystemCollection Collection { get; }
    public string Label { get; }
    public string Description { get; }
    public string AutomationId { get; }
    public string AccessibleName => $"{Label}，{Description}";
    public bool IsEnabled { get; }
    public object? DropTarget => Collection == AssetLibrarySystemCollection.AllAssets
        ? new AssetLibraryDropTarget(AssetLibraryDropTargetKind.RemoveFromCurrent, null, Label)
        : null;
    public AssetCommand SelectCommand { get; }
}

public sealed class AssetLibraryFolderNodeView : ObservableObject
{
    private readonly AssetLibraryViewModel _owner;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isRenaming;
    private string _editName;

    internal AssetLibraryFolderNodeView(AssetLibraryViewModel owner, AssetFolderTreeItem item)
    {
        _owner = owner;
        Item = item;
        _editName = item.Folder.Name;
        _isExpanded = owner.IsFolderExpanded(item.Folder.FolderId);
        foreach (var child in item.Children) Children.Add(new(owner, child));
        BeginRenameCommand = new(() => IsRenaming = true);
        CommitRenameCommand = new(CommitRenameAsync);
        CancelRenameCommand = new(() => { EditName = Name; IsRenaming = false; });
        CreateSiblingCommand = new(() => owner.CreateFolderRelativeAsync(this, child: false));
        CreateChildCommand = new(() => owner.CreateFolderRelativeAsync(this, child: true));
        MoveUpCommand = new(() => owner.MoveFolderInSiblingOrderAsync(this, -1));
        MoveDownCommand = new(() => owner.MoveFolderInSiblingOrderAsync(this, 1));
        PromoteCommand = new(() => owner.PromoteFolderAsync(this));
        ToggleArchiveCommand = new(() => owner.SetFolderArchivedAsync(this, !IsArchived));
    }

    public AssetFolderTreeItem Item { get; }
    internal AssetLibraryViewModel Owner => _owner;
    public AssetFolder Folder => Item.Folder;
    public Guid FolderId => Folder.FolderId;
    public string Name => Folder.Name;
    public string Path => Item.Path;
    public bool IsArchived => Folder.IsArchived;
    public int DirectAssetCount => Item.DirectAssetCount;
    public int DescendantAssetCount => Item.DescendantAssetCount;
    public string CountText => DescendantAssetCount == DirectAssetCount ? DirectAssetCount.ToString() : $"{DirectAssetCount}/{DescendantAssetCount}";
    public string AutomationId => $"AssetFolderNode_{FolderId:N}";
    public string AccessibleName => $"文件夹 {Name}，{DescendantAssetCount} 项{(IsArchived ? "，已归档" : string.Empty)}";
    public object DropTarget => new AssetLibraryDropTarget(AssetLibraryDropTargetKind.Folder, FolderId, Name);
    public ObservableCollection<AssetLibraryFolderNodeView> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value)) return;
            _owner.RememberFolderExpanded(FolderId, value);
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value) || !value) return;
            _owner.SelectFolderNode(this);
        }
    }

    public bool IsRenaming { get => _isRenaming; set => SetProperty(ref _isRenaming, value); }
    public string EditName { get => _editName; set => SetProperty(ref _editName, value ?? string.Empty); }
    public AssetCommand BeginRenameCommand { get; }
    public AsyncCommand CommitRenameCommand { get; }
    public AssetCommand CancelRenameCommand { get; }
    public AsyncCommand CreateSiblingCommand { get; }
    public AsyncCommand CreateChildCommand { get; }
    public AsyncCommand MoveUpCommand { get; }
    public AsyncCommand MoveDownCommand { get; }
    public AsyncCommand PromoteCommand { get; }
    public AsyncCommand ToggleArchiveCommand { get; }

    private async Task CommitRenameAsync()
    {
        if (await _owner.RenameFolderNodeAsync(this, EditName)) IsRenaming = false;
    }
}

public sealed class AssetLibrarySmartFolderNodeView
{
    internal AssetLibrarySmartFolderNodeView(AssetLibraryViewModel owner, SmartFolder folder)
    {
        Folder = folder;
        SelectCommand = new(() => owner.SelectSmartFolderNode(this));
        EditCommand = new(() => owner.EditSmartFolder(this));
    }

    public SmartFolder Folder { get; }
    public string Name => Folder.Name;
    public string Description => Folder.Description;
    public string AutomationId => $"AssetSmartFolderNode_{Folder.SmartFolderId:N}";
    public string AccessibleName => $"智能文件夹 {Name}{(string.IsNullOrWhiteSpace(Description) ? string.Empty : "，" + Description)}";
    public AssetCommand SelectCommand { get; }
    public AssetCommand EditCommand { get; }
}

public sealed class AssetLibraryTagNodeView
{
    private readonly AssetLibraryViewModel _owner;
    internal AssetLibraryTagNodeView(AssetLibraryViewModel owner, AssetTag tag)
    {
        _owner = owner;
        Tag = tag;
        SelectCommand = new(() => owner.SelectTagNode(this));
    }

    public AssetTag Tag { get; }
    internal AssetLibraryViewModel Owner => _owner;
    public string Name => Tag.Name;
    public int UsageCount => Tag.UsageCount;
    public string AutomationId => $"AssetTagNode_{Tag.TagId:N}";
    public string AccessibleName => $"标签 {Name}，{UsageCount} 项";
    public object DropTarget => new AssetLibraryDropTarget(AssetLibraryDropTargetKind.Tag, Tag.TagId, Name);
    public AssetCommand SelectCommand { get; }
}

public sealed class AssetLibraryTagGroupNodeView
{
    internal AssetLibraryTagGroupNodeView(TagGroup? group, IEnumerable<AssetLibraryTagNodeView> tags)
    {
        Group = group;
        Name = group?.Name ?? "未分组标签";
        AutomationId = group is null ? "AssetTagGroup_Ungrouped" : $"AssetTagGroup_{group.TagGroupId:N}";
        Children = new(tags);
    }

    public TagGroup? Group { get; }
    public string Name { get; }
    public string AutomationId { get; }
    public string AccessibleName => $"标签组 {Name}，{Children.Count} 个标签";
    public ObservableCollection<AssetLibraryTagNodeView> Children { get; }
}
