using System.Collections.ObjectModel;
using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private AssetLibraryBrowserCommandService _browserCommands = null!;
    private bool _changingP2QuerySource;
    private int _p2QueryTotalCount;
    private string _p2QueryDescription = "全部素材";
    private bool _isOrganizationLoading;
    private string _organizationError = string.Empty;
    private string _singleFolderSummary = string.Empty;
    private string _singleTagSummary = string.Empty;
    private string _multipleFolderSummary = string.Empty;
    private string _multipleTagSummary = string.Empty;
    private string _multipleRatingSummary = string.Empty;
    private long _inspectorGeneration;
    private readonly Dictionary<Guid, string> _p2TagSummaryByAsset = [];

    public ObservableCollection<AssetLibrarySystemCollectionView> SystemCollections { get; } = [];
    public ObservableCollection<AssetLibraryFolderNodeView> OrganizationFolders { get; } = [];
    public ObservableCollection<AssetLibrarySmartFolderNodeView> OrganizationSmartFolders { get; } = [];
    public ObservableCollection<AssetLibraryTagGroupNodeView> OrganizationTagGroups { get; } = [];

    public AssetLibraryViewMode ViewMode => _workspaceSettings.ViewMode;
    public AssetLibrarySortField SortField => _workspaceSettings.SortField;
    public AssetLibrarySortDirection SortDirection => _workspaceSettings.SortDirection;
    public AssetLibrarySystemCollection ActiveCollection => _workspaceSettings.ActiveCollection;
    public bool IsGridView => ViewMode == AssetLibraryViewMode.Grid;
    public bool IsMasonryView => ViewMode == AssetLibraryViewMode.Masonry;
    public bool IsJustifiedView => ViewMode == AssetLibraryViewMode.Justified;
    public bool IsListView => ViewMode == AssetLibraryViewMode.List;
    public string SortDirectionLabel => SortDirection == AssetLibrarySortDirection.Ascending ? "升序" : "降序";
    public string CurrentViewLabel => ViewMode switch
    {
        AssetLibraryViewMode.Grid => "网格",
        AssetLibraryViewMode.Masonry => "瀑布流",
        AssetLibraryViewMode.Justified => "两端对齐",
        _ => "列表"
    };

    public int P2QueryTotalCount { get => _p2QueryTotalCount; private set => SetProperty(ref _p2QueryTotalCount, value); }
    public string P2QueryDescription { get => _p2QueryDescription; private set => SetProperty(ref _p2QueryDescription, value); }
    public string P2QuerySummary => $"{P2QueryDescription} · {P2QueryTotalCount:N0} 项 · {CurrentViewLabel} · {SortField}/{SortDirectionLabel}";
    public bool IsOrganizationLoading { get => _isOrganizationLoading; private set { if (SetProperty(ref _isOrganizationLoading, value)) NotifyP2OrganizationState(); } }
    public string OrganizationError { get => _organizationError; private set { if (SetProperty(ref _organizationError, value)) NotifyP2OrganizationState(); } }
    public bool HasOrganizationError => !string.IsNullOrWhiteSpace(OrganizationError);
    public bool IsOrganizationEmpty => !IsOrganizationLoading && !HasOrganizationError && OrganizationFolders.Count == 0 && OrganizationSmartFolders.Count == 0 && OrganizationTagGroups.All(group => group.Children.Count == 0);

    public bool IsQueryInspectorVisible => SelectionCount == 0;
    public bool IsSingleInspectorVisible => SelectionCount == 1;
    public bool IsMultipleInspectorVisible => SelectionCount > 1;
    public string SingleFolderSummary { get => _singleFolderSummary; private set => SetProperty(ref _singleFolderSummary, value); }
    public string SingleTagSummary { get => _singleTagSummary; private set => SetProperty(ref _singleTagSummary, value); }
    public string MultipleFolderSummary { get => _multipleFolderSummary; private set => SetProperty(ref _multipleFolderSummary, value); }
    public string MultipleTagSummary { get => _multipleTagSummary; private set => SetProperty(ref _multipleTagSummary, value); }
    public string MultipleRatingSummary { get => _multipleRatingSummary; private set => SetProperty(ref _multipleRatingSummary, value); }

    public AsyncCommand<string> SwitchViewCommand { get; private set; } = null!;
    public AsyncCommand<string> SortBrowserCommand { get; private set; } = null!;
    public AsyncCommand ToggleSortDirectionCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> CopyContextPathCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> AddContextFolderCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RemoveContextFolderCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> AddContextTagCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RemoveContextTagCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextZeroCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextOneCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextTwoCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextThreeCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextFourCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RateContextFiveCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> MarkContextMissingCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ClearContextMissingCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ArchiveContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RestoreContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RemoveContextFromViewCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ShowContextInfoCommand { get; private set; } = null!;
    public AsyncCommand P2UndoCommand { get; private set; } = null!;
    public AsyncCommand P2RedoCommand { get; private set; } = null!;

    public event EventHandler<AssetLibraryViewModeChangedEventArgs>? ViewModeChanging;
    public event EventHandler<AssetLibraryViewModeChangedEventArgs>? ViewModeChanged;

    private void InitializeP2Browser()
    {
        _browserCommands = new(_repository);
        BuildSystemCollections();
        SwitchViewCommand = new(SwitchViewAsync);
        SortBrowserCommand = new(SortBrowserAsync);
        ToggleSortDirectionCommand = new(ToggleSortDirectionAsync);
        CopyContextPathCommand = new(CopyContextPathAsync);
        AddContextFolderCommand = new(card => AddContextFolderAsync(card), _ => SelectedFolder is not null);
        RemoveContextFolderCommand = new(card => RemoveContextFolderAsync(card), _ => SelectedFolder is not null);
        AddContextTagCommand = new(card => AddContextTagAsync(card), _ => SelectedTag is not null);
        RemoveContextTagCommand = new(card => RemoveContextTagAsync(card), _ => SelectedTag is not null);
        RateContextZeroCommand = new(card => RateContextAsync(card, 0));
        RateContextOneCommand = new(card => RateContextAsync(card, 1));
        RateContextTwoCommand = new(card => RateContextAsync(card, 2));
        RateContextThreeCommand = new(card => RateContextAsync(card, 3));
        RateContextFourCommand = new(card => RateContextAsync(card, 4));
        RateContextFiveCommand = new(card => RateContextAsync(card, 5));
        MarkContextMissingCommand = new(card => SetContextMissingAsync(card, true));
        ClearContextMissingCommand = new(card => SetContextMissingAsync(card, false));
        ArchiveContextCommand = new(card => SetContextArchivedAsync(card, true));
        RestoreContextCommand = new(card => SetContextArchivedAsync(card, false));
        RemoveContextFromViewCommand = new(RemoveContextFromViewAsync, _ => SelectedFolder is not null || SelectedTag is not null);
        ShowContextInfoCommand = new(ShowContextInfoAsync);
        P2UndoCommand = new(UndoP2Async, () => _browserCommands.CanUndo);
        P2RedoCommand = new(RedoP2Async, () => _browserCommands.CanRedo);
    }

    private void BuildSystemCollections()
    {
        SystemCollections.Clear();
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.AllAssets, "全部素材", "显示当前素材库中的全部未归档素材", "AssetLibraryAllAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.RecentlyAdded, "最近添加", "按添加时间从新到旧", "AssetLibraryRecentAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.Uncategorized, "未归类", "尚未加入文件夹", "AssetLibraryUncategorizedAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.Untagged, "未打标签", "尚未添加标签", "AssetLibraryUntaggedAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.MissingFiles, "缺失文件", "源路径目前不可用", "AssetLibraryMissingAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.Archived, "已归档", "仅显示已归档素材", "AssetLibraryArchivedAssets"));
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.RecycleBin, "回收站", "暂未启用", "AssetLibraryRecycleBin", isEnabled: false));
    }

    internal void SelectSystemCollection(AssetLibrarySystemCollection collection)
    {
        if (collection == AssetLibrarySystemCollection.RecycleBin) { Status = "回收站暂未启用；P2 不提供删除流程。"; return; }
        _changingP2QuerySource = true;
        try
        {
            _selectedFolder = null; _selectedTag = null; _selectedSmartFolder = null;
            _workspaceSettings.SelectedFolderId = null; _workspaceSettings.SelectedTagId = null; _workspaceSettings.SelectedSmartFolderId = null;
            OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder));
        }
        finally { _changingP2QuerySource = false; }
        SetActiveCollectionWithoutRefresh(collection);
        _ = RefreshAsync();
    }

    private void SetActiveCollectionWithoutRefresh(AssetLibrarySystemCollection collection)
    {
        _workspaceSettings.ActiveCollection = collection;
        OnPropertyChanged(nameof(ActiveCollection));
        UpdateP2QueryDescription();
    }

    private void RestoreP2QuerySourceAfterLists()
    {
        _changingP2QuerySource = true;
        try
        {
            if (ActiveCollection != AssetLibrarySystemCollection.AllAssets)
            {
                _selectedFolder = null; _selectedTag = null; _selectedSmartFolder = null;
            }
            else if (_selectedSmartFolder is not null)
            {
                _selectedFolder = null; _selectedTag = null;
            }
            else if (_selectedTag is not null)
            {
                _selectedFolder = null;
            }
            _workspaceSettings.SelectedFolderId = _selectedFolder?.FolderId;
            _workspaceSettings.SelectedTagId = _selectedTag?.TagId;
            _workspaceSettings.SelectedSmartFolderId = _selectedSmartFolder?.SmartFolderId;
            OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder));
        }
        finally { _changingP2QuerySource = false; }
        UpdateP2QueryDescription();
    }

    private void SelectP2QuerySource(AssetFolder? folder = null, AssetTag? tag = null, SmartFolder? smartFolder = null)
    {
        if (_changingP2QuerySource || folder is null && tag is null && smartFolder is null) return;
        _changingP2QuerySource = true;
        try
        {
            SetActiveCollectionWithoutRefresh(AssetLibrarySystemCollection.AllAssets);
            if (folder is not null)
            {
                _selectedTag = null; _selectedSmartFolder = null;
                _workspaceSettings.SelectedTagId = null; _workspaceSettings.SelectedSmartFolderId = null;
                OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder));
            }
            else if (tag is not null)
            {
                _selectedFolder = null; _selectedSmartFolder = null;
                _workspaceSettings.SelectedFolderId = null; _workspaceSettings.SelectedSmartFolderId = null;
                OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedSmartFolder));
            }
            else
            {
                _selectedFolder = null; _selectedTag = null;
                _workspaceSettings.SelectedFolderId = null; _workspaceSettings.SelectedTagId = null;
                OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag));
            }
        }
        finally { _changingP2QuerySource = false; }
        UpdateP2QueryDescription();
        RaiseP2CommandStates();
    }

    internal void SelectFolderNode(AssetLibraryFolderNodeView node)
    {
        if (node.IsArchived) { Status = "已归档文件夹仅供恢复，不能作为当前素材归属目标。"; return; }
        SelectedFolder = Folders.FirstOrDefault(folder => folder.FolderId == node.FolderId) ?? node.Folder;
    }

    internal void SelectSmartFolderNode(AssetLibrarySmartFolderNodeView node) => SelectedSmartFolder = node.Folder;
    internal void SelectTagNode(AssetLibraryTagNodeView node) => SelectedTag = node.Tag;
    internal void EditSmartFolder(AssetLibrarySmartFolderNodeView node)
    {
        SelectedSmartFolder = node.Folder;
        SmartFolderName = node.Folder.Name;
        Status = "已载入智能文件夹；P2 基础编辑器支持单层条件。";
    }

    internal bool IsFolderExpanded(Guid folderId) => _workspaceSettings.ExpandedFolderIds.Contains(folderId);
    internal void RememberFolderExpanded(Guid folderId, bool expanded)
    {
        if (expanded)
        {
            if (!_workspaceSettings.ExpandedFolderIds.Contains(folderId)) _workspaceSettings.ExpandedFolderIds.Add(folderId);
        }
        else _workspaceSettings.ExpandedFolderIds.Remove(folderId);
    }

    internal async Task<bool> RenameFolderNodeAsync(AssetLibraryFolderNodeView node, string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) { Status = "文件夹名称不能为空。"; return false; }
        try
        {
            var result = await _repository.RenameFolderAsync(node.FolderId, trimmed, _lifetimeCancellation.Token);
            LastUndoToken = result.UndoToken;
            Status = result.ChangedCount == 0 ? "文件夹名称没有变化。" : $"已重命名为“{trimmed}”。";
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            return true;
        }
        catch (Exception exception) { Status = $"重命名失败：{exception.Message}"; return false; }
    }

    internal async Task CreateFolderRelativeAsync(AssetLibraryFolderNodeView node, bool child)
    {
        var parentId = child ? node.FolderId : node.Folder.ParentFolderId;
        var seed = string.IsNullOrWhiteSpace(NewFolderName) ? (child ? "新建子文件夹" : "新建文件夹") : NewFolderName.Trim();
        var name = UniqueName(seed, Folders.Where(folder => folder.ParentFolderId == parentId).Select(folder => folder.Name));
        await _repository.SaveFolderAsync(new(Guid.NewGuid(), parentId, name), _lifetimeCancellation.Token);
        NewFolderName = string.Empty;
        await RefreshFilterListsAsync(_lifetimeCancellation.Token);
        Status = $"已创建文件夹：{name}";
    }

    internal async Task SetFolderArchivedAsync(AssetLibraryFolderNodeView node, bool archived)
    {
        try
        {
            var result = await _repository.SetFolderArchivedAsync(node.FolderId, archived, _lifetimeCancellation.Token);
            LastUndoToken = result.UndoToken;
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            Status = archived ? $"已归档文件夹：{node.Name}" : $"已恢复文件夹：{node.Name}";
        }
        catch (Exception exception) { Status = $"文件夹操作失败：{exception.Message}"; }
    }

    internal async Task MoveFolderInSiblingOrderAsync(AssetLibraryFolderNodeView node, int delta)
    {
        var siblings = Folders.Where(folder => folder.ParentFolderId == node.Folder.ParentFolderId && !folder.IsArchived)
            .OrderBy(folder => folder.SortOrder).ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var index = siblings.FindIndex(folder => folder.FolderId == node.FolderId);
        var target = index + Math.Sign(delta);
        if (index < 0 || target < 0 || target >= siblings.Count) { Status = delta < 0 ? "已经是同级第一项。" : "已经是同级最后一项。"; return; }
        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        var result = await _repository.ReorderFoldersAsync(node.Folder.ParentFolderId, siblings.Select(folder => folder.FolderId), _lifetimeCancellation.Token);
        LastUndoToken = result.UndoToken;
        await RefreshFilterListsAsync(_lifetimeCancellation.Token);
        Status = delta < 0 ? $"已上移文件夹：{node.Name}" : $"已下移文件夹：{node.Name}";
    }

    internal async Task PromoteFolderAsync(AssetLibraryFolderNodeView node)
    {
        if (node.Folder.ParentFolderId is null) { Status = "该文件夹已经位于根级。"; return; }
        var parent = Folders.FirstOrDefault(folder => folder.FolderId == node.Folder.ParentFolderId);
        var nextParent = parent?.ParentFolderId;
        var nextSort = Folders.Where(folder => folder.ParentFolderId == nextParent).Select(folder => folder.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        var result = await _repository.MoveFolderAsync(new(node.FolderId, nextParent, nextSort), _lifetimeCancellation.Token);
        LastUndoToken = result.UndoToken;
        await RefreshFilterListsAsync(_lifetimeCancellation.Token);
        Status = $"已将“{node.Name}”提升一级。";
    }

    private async Task RefreshP2OrganizationAsync(CancellationToken cancellationToken)
    {
        IsOrganizationLoading = true;
        OrganizationError = string.Empty;
        try
        {
            var tree = await _repository.GetFolderTreeAsync(includeArchived: true, cancellationToken);
            OrganizationFolders.Clear(); foreach (var node in tree) OrganizationFolders.Add(new(this, node));
            OrganizationSmartFolders.Clear(); foreach (var folder in SmartFolders) OrganizationSmartFolders.Add(new(this, folder));
            OrganizationTagGroups.Clear();
            var tagViews = Tags.Select(tag => new AssetLibraryTagNodeView(this, tag)).ToArray();
            foreach (var group in TagGroups)
                OrganizationTagGroups.Add(new(group, tagViews.Where(tag => tag.Tag.TagGroupId == group.TagGroupId)));
            var ungrouped = tagViews.Where(tag => tag.Tag.TagGroupId is null || TagGroups.All(group => group.TagGroupId != tag.Tag.TagGroupId)).ToArray();
            if (ungrouped.Length > 0) OrganizationTagGroups.Add(new(null, ungrouped));
            var tagNames = Tags.ToDictionary(tag => tag.TagId, tag => tag.Name);
            var memberships = await _repository.ListTagMembershipsAsync(cancellationToken: cancellationToken);
            _p2TagSummaryByAsset.Clear();
            foreach (var group in memberships.GroupBy(item => item.AssetId))
                _p2TagSummaryByAsset[group.Key] = string.Join("、", group.Select(item => tagNames.GetValueOrDefault(item.TagId)).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { OrganizationError = $"组织栏加载失败：{exception.Message}"; }
        finally { IsOrganizationLoading = false; NotifyP2OrganizationState(); }
    }

    private async Task SwitchViewAsync(string? value)
    {
        if (!Enum.TryParse<AssetLibraryViewMode>(value, true, out var next) || next == ViewMode) return;
        var previous = ViewMode;
        ViewModeChanging?.Invoke(this, new(previous, next));
        _workspaceSettings.ViewMode = next;
        NotifyP2BrowserView();
        await Task.Yield();
        ViewModeChanged?.Invoke(this, new(previous, next));
    }

    private async Task SortBrowserAsync(string? value)
    {
        if (!Enum.TryParse<AssetLibrarySortField>(value, true, out var next)) return;
        if (next == SortField)
            _workspaceSettings.SortDirection = SortDirection == AssetLibrarySortDirection.Ascending ? AssetLibrarySortDirection.Descending : AssetLibrarySortDirection.Ascending;
        else
        {
            _workspaceSettings.SortField = next;
            _workspaceSettings.SortDirection = next == AssetLibrarySortField.FileName ? AssetLibrarySortDirection.Ascending : AssetLibrarySortDirection.Descending;
        }
        NotifyP2Sort();
        await RefreshAsync();
    }

    private async Task ToggleSortDirectionAsync()
    {
        _workspaceSettings.SortDirection = SortDirection == AssetLibrarySortDirection.Ascending ? AssetLibrarySortDirection.Descending : AssetLibrarySortDirection.Ascending;
        NotifyP2Sort();
        await RefreshAsync();
    }

    private void UpdateP2QuerySummary(int total)
    {
        P2QueryTotalCount = total;
        UpdateP2QueryDescription();
        OnPropertyChanged(nameof(P2QuerySummary));
    }

    private void UpdateP2QueryDescription()
    {
        P2QueryDescription = SelectedFolder?.Name
            ?? SelectedTag?.Name
            ?? SelectedSmartFolder?.Name
            ?? ActiveCollection switch
            {
                AssetLibrarySystemCollection.RecentlyAdded => "最近添加",
                AssetLibrarySystemCollection.Uncategorized => "未归类",
                AssetLibrarySystemCollection.Untagged => "未打标签",
                AssetLibrarySystemCollection.MissingFiles => "缺失文件",
                AssetLibrarySystemCollection.Archived => "已归档",
                _ => "全部素材"
            };
        OnPropertyChanged(nameof(P2QuerySummary));
    }

    private void OnP2SelectionChanged(IReadOnlyList<AssetItem> selected)
    {
        OnPropertyChanged(nameof(IsQueryInspectorVisible));
        OnPropertyChanged(nameof(IsSingleInspectorVisible));
        OnPropertyChanged(nameof(IsMultipleInspectorVisible));
        var generation = Interlocked.Increment(ref _inspectorGeneration);
        _ = RefreshP2InspectorAsync(selected, SelectionCount, generation);
    }

    private async Task RefreshP2InspectorAsync(IReadOnlyList<AssetItem> selected, int selectedIdCount, long generation)
    {
        try
        {
            if (selectedIdCount == 0)
            {
                SingleFolderSummary = SingleTagSummary = MultipleFolderSummary = MultipleTagSummary = MultipleRatingSummary = string.Empty;
                return;
            }
            if (selected.Count != selectedIdCount)
            {
                SingleFolderSummary = SingleTagSummary = string.Empty;
                MultipleFolderSummary = MultipleTagSummary = MultipleRatingSummary = "部分选择项尚未加载；选中编号仍已保留。";
                return;
            }
            var ids = selected.Select(asset => asset.AssetId).ToHashSet();
            var folderMemberships = await _repository.ListFolderMembershipsAsync(cancellationToken: _lifetimeCancellation.Token);
            var tagMemberships = await _repository.ListTagMembershipsAsync(cancellationToken: _lifetimeCancellation.Token);
            if (generation != Volatile.Read(ref _inspectorGeneration)) return;
            if (selected.Count == 1)
            {
                var id = selected[0].AssetId;
                SingleFolderSummary = JoinNames(folderMemberships.Where(item => item.AssetId == id).Select(item => Folders.FirstOrDefault(folder => folder.FolderId == item.FolderId)?.Name));
                SingleTagSummary = JoinNames(tagMemberships.Where(item => item.AssetId == id).Select(item => Tags.FirstOrDefault(tag => tag.TagId == item.TagId)?.Name));
                return;
            }
            var commonFolderIds = folderMemberships.Where(item => ids.Contains(item.AssetId)).GroupBy(item => item.FolderId).Where(group => group.Select(item => item.AssetId).Distinct().Count() == ids.Count).Select(group => group.Key);
            var commonTagIds = tagMemberships.Where(item => ids.Contains(item.AssetId)).GroupBy(item => item.TagId).Where(group => group.Select(item => item.AssetId).Distinct().Count() == ids.Count).Select(group => group.Key);
            MultipleFolderSummary = JoinNames(commonFolderIds.Select(id => Folders.FirstOrDefault(folder => folder.FolderId == id)?.Name));
            MultipleTagSummary = JoinNames(commonTagIds.Select(id => Tags.FirstOrDefault(tag => tag.TagId == id)?.Name));
            MultipleRatingSummary = selected.Select(asset => asset.Rating).Distinct().Take(2).Count() == 1 ? $"共同评分：{selected[0].Rating}" : "评分：混合值";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
    }

    private static string JoinNames(IEnumerable<string?> values)
    {
        var names = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return names.Length == 0 ? "无" : string.Join("、", names!);
    }

    private string GetP2TagSummary(Guid assetId) => _p2TagSummaryByAsset.GetValueOrDefault(assetId, "—");

    public void RememberScrollAnchor(Guid? assetId)
    {
        _workspaceSettings.ScrollAnchors[ViewMode.ToString()] = assetId;
    }

    public Guid? GetScrollAnchor(AssetLibraryViewMode mode) => _workspaceSettings.ScrollAnchors.GetValueOrDefault(mode.ToString());

    internal IReadOnlyList<Guid> GetDragAssetIds() => SelectedAssetIds.ToArray();
    internal bool CanDropOn(AssetLibraryDropTarget target) => SelectionCount > 0 &&
        (target.Kind is AssetLibraryDropTargetKind.Folder or AssetLibraryDropTargetKind.Tag && target.TargetId is not null ||
         target.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent && (SelectedFolder is not null || SelectedTag is not null));
    internal async Task PreviewDropAsync(AssetLibraryDropTarget target)
    {
        var preview = target.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent
            ? SelectedFolder is not null
                ? await _browserCommands.PreviewRemoveAsync(GetDragAssetIds(), SelectedFolder.FolderId, folder: true, SelectedFolder.Name, _lifetimeCancellation.Token)
                : SelectedTag is not null
                    ? await _browserCommands.PreviewRemoveAsync(GetDragAssetIds(), SelectedTag.TagId, folder: false, SelectedTag.Name, _lifetimeCancellation.Token)
                    : new(false, SelectionCount, 0, 0, "当前查询没有可移出的文件夹或标签归属。")
            : await _browserCommands.PreviewDropAsync(GetDragAssetIds(), target, _lifetimeCancellation.Token);
        Status = preview.Message;
    }
    internal async Task ExecuteDropAsync(AssetLibraryDropTarget target)
    {
        var preview = target.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent
            ? SelectedFolder is not null
                ? await _browserCommands.PreviewRemoveAsync(GetDragAssetIds(), SelectedFolder.FolderId, folder: true, SelectedFolder.Name, _lifetimeCancellation.Token)
                : SelectedTag is not null
                    ? await _browserCommands.PreviewRemoveAsync(GetDragAssetIds(), SelectedTag.TagId, folder: false, SelectedTag.Name, _lifetimeCancellation.Token)
                    : new(false, SelectionCount, 0, 0, "当前查询没有可移出的文件夹或标签归属。")
            : await _browserCommands.PreviewDropAsync(GetDragAssetIds(), target, _lifetimeCancellation.Token);
        Status = preview.Message;
        if (!preview.IsAllowed) return;
        var result = target.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent
            ? SelectedFolder is not null
                ? await _browserCommands.RemoveFromFolderAsync(GetDragAssetIds(), SelectedFolder.FolderId, _lifetimeCancellation.Token)
                : await _browserCommands.RemoveTagAsync(GetDragAssetIds(), SelectedTag!.TagId, _lifetimeCancellation.Token)
            : await _browserCommands.ExecuteDropAsync(GetDragAssetIds(), target, _lifetimeCancellation.Token);
        Status = $"{preview.Message} 已完成 {result.ChangedCount} 项，可撤销。";
        RaiseP2CommandStates();
        await RefreshAsync();
    }

    private IReadOnlyList<Guid> ContextIds(AssetVisualMatchView? card)
    {
        if (card is null) return [];
        return SelectedAssetIds.Contains(card.Asset.AssetId)
            ? SelectedAssetIds.ToArray()
            : [card.Asset.AssetId];
    }

    private async Task CopyContextPathAsync(AssetVisualMatchView? card)
    {
        if (card is null) return;
        try { await _browserCommands.CopyPathAsync(card.Asset.SourcePath); Status = "路径已复制；未修改源文件。"; }
        catch (Exception exception) { Status = $"复制路径失败：{exception.Message}"; }
    }

    private async Task AddContextFolderAsync(AssetVisualMatchView? card)
    {
        if (card is null || SelectedFolder is null) return;
        var result = await _browserCommands.AddToFolderAsync(ContextIds(card), SelectedFolder.FolderId, _lifetimeCancellation.Token);
        Status = $"已加入文件夹：{result.ChangedCount} 项。"; RaiseP2CommandStates();
    }
    private async Task RemoveContextFolderAsync(AssetVisualMatchView? card)
    {
        if (card is null || SelectedFolder is null) return;
        var result = await _browserCommands.RemoveFromFolderAsync(ContextIds(card), SelectedFolder.FolderId, _lifetimeCancellation.Token);
        Status = $"已移出文件夹：{result.ChangedCount} 项。"; RaiseP2CommandStates(); await RefreshAsync();
    }
    private async Task AddContextTagAsync(AssetVisualMatchView? card)
    {
        if (card is null || SelectedTag is null) return;
        var result = await _browserCommands.AddTagAsync(ContextIds(card), SelectedTag.TagId, _lifetimeCancellation.Token);
        Status = $"已加入标签：{result.ChangedCount} 项。"; RaiseP2CommandStates();
    }
    private async Task RemoveContextTagAsync(AssetVisualMatchView? card)
    {
        if (card is null || SelectedTag is null) return;
        var result = await _browserCommands.RemoveTagAsync(ContextIds(card), SelectedTag.TagId, _lifetimeCancellation.Token);
        Status = $"已移出标签：{result.ChangedCount} 项。"; RaiseP2CommandStates(); await RefreshAsync();
    }
    private async Task RateContextAsync(AssetVisualMatchView? card, int rating)
    {
        if (card is null) return;
        var result = await _browserCommands.RateAsync(ContextIds(card), rating, _lifetimeCancellation.Token);
        Status = $"已将 {result.ChangedCount} 项评分设为 {rating}。"; RaiseP2CommandStates(); await RefreshAsync();
    }
    private async Task SetContextMissingAsync(AssetVisualMatchView? card, bool missing)
    {
        if (card is null) return;
        var result = await _browserCommands.SetMissingAsync(ContextIds(card), missing, _lifetimeCancellation.Token);
        Status = missing ? $"已标记 {result.ChangedCount} 项缺失。" : $"已清除 {result.ChangedCount} 项缺失标记。"; RaiseP2CommandStates(); await RefreshAsync();
    }
    private async Task SetContextArchivedAsync(AssetVisualMatchView? card, bool archived)
    {
        if (card is null) return;
        var ids = ContextIds(card);
        var result = await _browserCommands.SetArchivedAsync(ids, archived, _lifetimeCancellation.Token);
        if (archived && ActiveCollection != AssetLibrarySystemCollection.Archived || !archived && ActiveCollection == AssetLibrarySystemCollection.Archived)
            RemoveSelectedIds(ids);
        Status = archived ? $"已归档 {result.ChangedCount} 项。" : $"已恢复 {result.ChangedCount} 项。"; RaiseP2CommandStates(); await RefreshAsync();
    }

    private void RemoveSelectedIds(IEnumerable<Guid> ids)
    {
        var removed = ids.ToHashSet();
        var remaining = SelectedAssetIds.Where(id => !removed.Contains(id)).ToArray();
        var remainingSet = remaining.ToHashSet();
        ApplySelectionState(SelectedAssets.Where(asset => remainingSet.Contains(asset.AssetId)).ToArray(), remaining, replacePersistedIds: true);
    }
    private async Task RemoveContextFromViewAsync(AssetVisualMatchView? card)
    {
        if (SelectedFolder is not null) await RemoveContextFolderAsync(card);
        else if (SelectedTag is not null) await RemoveContextTagAsync(card);
    }
    private Task ShowContextInfoAsync(AssetVisualMatchView? card)
    {
        if (card is null) return Task.CompletedTask;
        SyncSelection([_browserCommands.PrepareInformationView(card.Asset)]);
        if (IsInspectorPaneCollapsed) IsInspectorPaneCollapsed = false;
        Status = "已在检查器中显示素材信息。";
        return Task.CompletedTask;
    }
    private async Task UndoP2Async()
    {
        Status = await _browserCommands.UndoAsync(_lifetimeCancellation.Token) ? "已撤销素材库操作。" : "没有可撤销的素材库操作。";
        RaiseP2CommandStates(); await RefreshFilterListsAsync(_lifetimeCancellation.Token); await RefreshAsync();
    }
    private async Task RedoP2Async()
    {
        Status = await _browserCommands.RedoAsync(_lifetimeCancellation.Token) ? "已重做素材库操作。" : "没有可重做的素材库操作。";
        RaiseP2CommandStates(); await RefreshFilterListsAsync(_lifetimeCancellation.Token); await RefreshAsync();
    }

    private void RaiseP2CommandStates()
    {
        AddContextFolderCommand.RaiseCanExecuteChanged(); RemoveContextFolderCommand.RaiseCanExecuteChanged();
        AddContextTagCommand.RaiseCanExecuteChanged(); RemoveContextTagCommand.RaiseCanExecuteChanged();
        RemoveContextFromViewCommand.RaiseCanExecuteChanged(); P2UndoCommand.RaiseCanExecuteChanged(); P2RedoCommand.RaiseCanExecuteChanged();
    }
    private void NotifyP2BrowserView()
    {
        OnPropertyChanged(nameof(ViewMode)); OnPropertyChanged(nameof(IsGridView)); OnPropertyChanged(nameof(IsMasonryView));
        OnPropertyChanged(nameof(IsJustifiedView)); OnPropertyChanged(nameof(IsListView)); OnPropertyChanged(nameof(CurrentViewLabel)); OnPropertyChanged(nameof(P2QuerySummary));
    }
    private void NotifyP2Sort()
    {
        OnPropertyChanged(nameof(SortField)); OnPropertyChanged(nameof(SortDirection)); OnPropertyChanged(nameof(SortDirectionLabel)); OnPropertyChanged(nameof(P2QuerySummary));
    }
    private void NotifyP2OrganizationState()
    {
        OnPropertyChanged(nameof(HasOrganizationError)); OnPropertyChanged(nameof(IsOrganizationEmpty));
    }
}

public sealed record AssetLibraryViewModeChangedEventArgs(AssetLibraryViewMode Previous, AssetLibraryViewMode Current);
