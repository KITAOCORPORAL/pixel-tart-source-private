using System.Collections.ObjectModel;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibrarySystemCollectionView : ObservableObject
{
    private int _count;
    private bool _isActive;

    internal AssetLibrarySystemCollectionView(AssetLibraryViewModel owner, AssetLibrarySystemCollection collection, string label, string description, string automationId, bool isEnabled = true)
    {
        Collection = collection;
        Label = label;
        Description = description;
        AutomationId = automationId;
        IsEnabled = isEnabled;
        IconData = Geometry.Parse(collection switch
        {
            AssetLibrarySystemCollection.AllAssets => "M3,4 L17,4 L17,16 L3,16 Z M6,7 L9,10 L12,8 L16,13",
            AssetLibrarySystemCollection.RecentlyAdded => "M10,3 A7,7 0 1 0 17,10 A7,7 0 1 0 10,3 M10,6 L10,10 L13,12",
            AssetLibrarySystemCollection.Uncategorized => "M2.5,5 L8,5 L10,7 L17.5,7 L17.5,16 L2.5,16 Z",
            AssetLibrarySystemCollection.Untagged => "M3,9 L9,3 L17,3 L17,11 L11,17 Z M13.5,6.5 L13.6,6.5",
            AssetLibrarySystemCollection.HighRating => "M10,2.5 L12.3,7.2 L17.5,8 L13.8,11.7 L14.7,17 L10,14.5 L5.3,17 L6.2,11.7 L2.5,8 L7.7,7.2 Z",
            AssetLibrarySystemCollection.MissingFiles => "M3,4 L17,4 L17,16 L3,16 Z M6,7 L14,15 M14,7 L6,15",
            AssetLibrarySystemCollection.Archived => "M3,7 L17,7 L16,17 L4,17 Z M2.5,3 L17.5,3 L17.5,7 L2.5,7 Z",
            _ => "M5,6 L15,6 L14,17 L6,17 Z M3.5,6 L16.5,6 M8,3 L12,3"
        });
        IconData.Freeze();
        SelectCommand = new(() => owner.SelectSystemCollection(collection), () => isEnabled);
    }

    public AssetLibrarySystemCollection Collection { get; }
    public string Label { get; }
    public string Description { get; }
    public string AutomationId { get; }
    public Geometry IconData { get; }
    public int Count { get => _count; internal set { if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(AccessibleName)); } }
    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
    public string AccessibleName => $"{Label}，{Count:N0} 项，{Description}";
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
    // Keep the target metadata explicit so the drag behavior can fail closed before
    // it starts an async preview for an archived node.
    public object DropTarget => new AssetLibraryDropTarget(AssetLibraryDropTargetKind.Folder, FolderId, Name, IsArchived);
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
    public object DropTarget => new AssetLibraryDropTarget(AssetLibraryDropTargetKind.Tag, Tag.TagId, Name, Tag.IsArchived);
    public AssetCommand SelectCommand { get; }
}

public sealed class AssetLibraryTagGroupNodeView : ObservableObject
{
    private readonly AssetLibraryViewModel _owner;
    private bool _isExpanded;

    internal AssetLibraryTagGroupNodeView(AssetLibraryViewModel owner, TagGroup? group, IEnumerable<AssetLibraryTagNodeView> tags)
    {
        _owner = owner;
        Group = group;
        Name = group?.Name ?? "未分组标签";
        AutomationId = group is null ? "AssetTagGroup_Ungrouped" : $"AssetTagGroup_{group.TagGroupId:N}";
        Children = new(tags);
        _isExpanded = group is null || owner.IsTagGroupExpanded(group.TagGroupId);
    }

    public TagGroup? Group { get; }
    public string Name { get; }
    public string AutomationId { get; }
    public string AccessibleName => $"标签组 {Name}，{Children.Count} 个标签";
    public ObservableCollection<AssetLibraryTagNodeView> Children { get; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value) || Group is null) return;
            _owner.RememberTagGroupExpanded(Group.TagGroupId, value);
        }
    }
}
