using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.OnlineSelection;

namespace PixelTart.Modules.AssetLibrary;

public sealed record AssetRelationPickerItem(
    Guid Id,
    string Name,
    string Details,
    Guid? ProjectId = null,
    DateTimeOffset? EventDate = null,
    string Group = "全部");

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
    private AssetLibraryCommandPreview? _lastDropPreview;
    private long _inspectorGeneration;
    private bool _p2JournalBusy;
    private readonly Dictionary<Guid, string> _p2TagSummaryByAsset = [];
    private int _inspirationTrayCount;
    private bool _isProjectPickerOpen;
    private bool _isBookingPickerOpen;
    private bool _isCollectionPanelOpen;
    private Guid? _activeCollectionId;
    private string _projectPickerSearch = string.Empty;
    private string _bookingPickerSearch = string.Empty;
    private AssetRelationPickerItem[] _allProjectPickerItems = [];
    private AssetRelationPickerItem[] _allBookingPickerItems = [];
    private IReadOnlyList<Guid>? _relationshipFilterAssetIds;
    private string? _relationshipFilterDescription;

    public ObservableCollection<AssetLibrarySystemCollectionView> SystemCollections { get; } = [];
    public ObservableCollection<AssetLibraryFolderNodeView> OrganizationFolders { get; } = [];
    public ObservableCollection<AssetLibrarySmartFolderNodeView> OrganizationSmartFolders { get; } = [];
    public ObservableCollection<AssetLibraryTagGroupNodeView> OrganizationTagGroups { get; } = [];
    public BulkObservableCollection<InspirationTrayEntry> InspirationTrayEntries { get; } = [];
    public BulkObservableCollection<InspirationTrayCardView> InspirationTrayCards { get; } = [];
    public ObservableCollection<AssetRelationPickerItem> ProjectPickerItems { get; } = [];
    public ObservableCollection<AssetRelationPickerItem> BookingPickerItems { get; } = [];
    public ObservableCollection<AssetRelationPickerItem> InspectorProjectLinks { get; } = [];
    public ObservableCollection<AssetRelationPickerItem> InspectorBookingLinks { get; } = [];
    public ObservableCollection<InspirationCollectionSummary> InspirationCollections { get; } = [];
    public ObservableCollection<InspirationTrayCardView> ActiveCollectionCards { get; } = [];

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

    /// <summary>
    /// The last drag/drop preview is retained as structured evidence.  The UI still binds
    /// to <see cref="AssetLibraryViewModel.Status"/> for the short human-readable message,
    /// while acceptance and diagnostics can inspect the stable failure code and cancellation
    /// bit without parsing that message.
    /// </summary>
    internal AssetLibraryCommandPreview? LastDropPreview
    {
        get => _lastDropPreview;
        private set
        {
            if (!SetProperty(ref _lastDropPreview, value)) return;
            OnPropertyChanged(nameof(HasDropError));
        }
    }

    internal bool HasDropError => LastDropPreview is { IsAllowed: false };

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
    public AsyncCommand<AssetVisualMatchView> TrashContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RestoreTrashContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> WorkflowClientSelectedCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> WorkflowUnprocessedCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> WorkflowPendingRetouchCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> WorkflowRetouchedCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> WorkflowDeliveredCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ExportOriginalContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ExportManagedContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ExportMetadataContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RemoveContextFromViewCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> ShowContextInfoCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> OpenContextViewerCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> OpenContextExternalCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> RevealContextCommand { get; private set; } = null!;
    public AsyncCommand<AssetVisualMatchView> AddToInspirationTrayCommand { get; private set; } = null!;
    public AsyncCommand ToggleInspirationTrayCommand { get; private set; } = null!;
    public AsyncCommand ClearInspirationTrayCommand { get; private set; } = null!;
    public AsyncCommand<InspirationTrayCardView> RemoveInspirationTrayEntryCommand { get; private set; } = null!;
    public AsyncCommand OpenProjectPickerCommand { get; private set; } = null!;
    public AsyncCommand OpenBookingPickerCommand { get; private set; } = null!;
    public AsyncCommand CloseRelationPickerCommand { get; private set; } = null!;
    public AsyncCommand<AssetRelationPickerItem> SelectProjectRelationCommand { get; private set; } = null!;
    public AsyncCommand<AssetRelationPickerItem> SelectBookingRelationCommand { get; private set; } = null!;
    public AsyncCommand<AssetRelationPickerItem> RemoveProjectRelationCommand { get; private set; } = null!;
    public AsyncCommand<AssetRelationPickerItem> RemoveBookingRelationCommand { get; private set; } = null!;
    public AsyncCommand<AssetRelationPickerItem> OpenCalendarBookingCommand { get; private set; } = null!;
    public AsyncCommand<string> SetInspectorWorkflowCommand { get; private set; } = null!;
    public AsyncCommand OpenCollectionsCommand { get; private set; } = null!;
    public AsyncCommand CreateCollectionCommand { get; private set; } = null!;
    public AsyncCommand<InspirationCollectionSummary> OpenCollectionCommand { get; private set; } = null!;
    public AsyncCommand<InspirationCollectionSummary> ArchiveCollectionCommand { get; private set; } = null!;
    public AsyncCommand<InspirationCollectionSummary> RenameCollectionCommand { get; private set; } = null!;
    public AsyncCommand<InspirationCollectionSummary> SetCollectionProjectCommand { get; private set; } = null!;
    public AsyncCommand<InspirationTrayCardView> RemoveCollectionEntryCommand { get; private set; } = null!;
    public AsyncCommand<InspirationCollectionSummary> AddSelectionToCollectionCommand { get; private set; } = null!;
    public int InspirationTrayCount { get => _inspirationTrayCount; private set => SetProperty(ref _inspirationTrayCount, value); }
    public bool IsProjectPickerOpen { get => _isProjectPickerOpen; private set => SetProperty(ref _isProjectPickerOpen, value); }
    public bool IsBookingPickerOpen { get => _isBookingPickerOpen; private set => SetProperty(ref _isBookingPickerOpen, value); }
    public string ProjectPickerSearch { get => _projectPickerSearch; set { if (SetProperty(ref _projectPickerSearch, value)) FilterProjectPicker(); } }
    public string BookingPickerSearch { get => _bookingPickerSearch; set { if (SetProperty(ref _bookingPickerSearch, value)) FilterBookingPicker(); } }
    public IReadOnlyList<string> WorkflowStatusOptions { get; } = ["未处理", "客户选择", "待精修", "已精修", "已交付"];
    public bool IsCollectionPanelOpen { get => _isCollectionPanelOpen; private set => SetProperty(ref _isCollectionPanelOpen, value); }
    public Guid? ActiveCollectionId { get => _activeCollectionId; private set => SetProperty(ref _activeCollectionId, value); }
    private bool _isInspirationTrayOpen;
    public bool IsInspirationTrayOpen { get => _isInspirationTrayOpen; private set => SetProperty(ref _isInspirationTrayOpen, value); }
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
        TrashContextCommand = new(card => SetContextTrashedAsync(card, true));
        RestoreTrashContextCommand = new(card => SetContextTrashedAsync(card, false));
        WorkflowUnprocessedCommand = new(card => SetContextWorkflowAsync(card, AssetWorkflowStatus.Unprocessed));
        WorkflowClientSelectedCommand = new(card => SetContextWorkflowAsync(card, AssetWorkflowStatus.ClientSelected));
        WorkflowPendingRetouchCommand = new(card => SetContextWorkflowAsync(card, AssetWorkflowStatus.PendingRetouch));
        WorkflowRetouchedCommand = new(card => SetContextWorkflowAsync(card, AssetWorkflowStatus.Retouched));
        WorkflowDeliveredCommand = new(card => SetContextWorkflowAsync(card, AssetWorkflowStatus.Delivered));
        ExportOriginalContextCommand = new(card => ExportContextFilesAsync(card, preferManagedCopy: false));
        ExportManagedContextCommand = new(card => ExportContextFilesAsync(card, preferManagedCopy: true));
        ExportMetadataContextCommand = new(ExportContextMetadataAsync);
        RemoveContextFromViewCommand = new(RemoveContextFromViewAsync, _ => SelectedFolder is not null || SelectedTag is not null);
        ShowContextInfoCommand = new(ShowContextInfoAsync);
        OpenContextViewerCommand = new(OpenContextViewerAsync);
        OpenContextExternalCommand = new(OpenContextExternalAsync);
        RevealContextCommand = new(RevealContextAsync);
        AddToInspirationTrayCommand = new(AddToInspirationTrayAsync, _ => IsReady && SelectedAssets.Count > 0);
        ToggleInspirationTrayCommand = new(ToggleInspirationTrayAsync, () => IsReady);
        ClearInspirationTrayCommand = new(ClearInspirationTrayAsync, () => IsReady && InspirationTrayEntries.Count > 0);
        RemoveInspirationTrayEntryCommand = new(RemoveInspirationTrayEntryAsync, card => IsReady && card is not null);
        OpenProjectPickerCommand = new(OpenProjectPickerAsync, () => IsReady && SelectedAssets.Count > 0);
        OpenBookingPickerCommand = new(OpenBookingPickerAsync, () => IsReady && SelectedAssets.Count > 0);
        CloseRelationPickerCommand = new(() => { IsProjectPickerOpen = false; IsBookingPickerOpen = false; return Task.CompletedTask; });
        SelectProjectRelationCommand = new(SelectProjectRelationAsync, _ => IsReady);
        SelectBookingRelationCommand = new(SelectBookingRelationAsync, _ => IsReady);
        RemoveProjectRelationCommand = new(RemoveProjectRelationAsync, _ => IsReady);
        RemoveBookingRelationCommand = new(RemoveBookingRelationAsync, _ => IsReady);
        OpenCalendarBookingCommand = new(OpenCalendarBookingAsync, item => IsReady && item is not null && _openCalendarBooking is not null);
        SetInspectorWorkflowCommand = new(SetInspectorWorkflowAsync, _ => IsReady && SelectedAssets.Count > 0);
        OpenCollectionsCommand = new(OpenCollectionsAsync, () => IsReady);
        CreateCollectionCommand = new(CreateCollectionAsync, () => IsReady);
        OpenCollectionCommand = new(OpenCollectionAsync, _ => IsReady);
        ArchiveCollectionCommand = new(ArchiveCollectionAsync, _ => IsReady);
        RenameCollectionCommand = new(RenameCollectionAsync, _ => IsReady);
        SetCollectionProjectCommand = new(SetCollectionProjectAsync, _ => IsReady);
        RemoveCollectionEntryCommand = new(RemoveCollectionEntryAsync, _ => IsReady);
        AddSelectionToCollectionCommand = new(AddSelectionToCollectionAsync, _ => IsReady && SelectedAssets.Count > 0);
        P2UndoCommand = new(() => RunTrackedP3OperationAsync(UndoP2Async), () => !_p2JournalBusy && _browserCommands.CanUndo);
        P2RedoCommand = new(() => RunTrackedP3OperationAsync(RedoP2Async), () => !_p2JournalBusy && _browserCommands.CanRedo);
    }

    private void InitializeInspirationTray() => _ = RefreshInspirationTrayAsync();

    private async Task OpenProjectPickerAsync()
    {
        IsBookingPickerOpen = false;
        var database = new PixelTartDatabase(RAWSelectionAssistant.Core.Utilities.AppDataPaths.DatabaseFile);
        var projects = await new SqliteProjectRepository(database).ListAsync(_lifetimeCancellation.Token);
        var selectedAssetIds = SelectedAssets.Select(asset => asset.AssetId).ToArray();
        var bookingLinks = new List<BookingAssetLink>();
        foreach (var assetId in selectedAssetIds)
            bookingLinks.AddRange(await _repository.ListBookingAssetLinksAsync(assetId: assetId, cancellationToken: _lifetimeCancellation.Token));
        var bookingRepository = new SqliteShootBookingRepository(database);
        var currentProjectIds = new HashSet<Guid>();
        foreach (var link in bookingLinks.DistinctBy(link => link.BookingId))
        {
            var booking = await bookingRepository.GetAsync(link.BookingId, cancellationToken: _lifetimeCancellation.Token);
            if (booking?.ProjectId is Guid projectId) currentProjectIds.Add(projectId);
        }
        _allProjectPickerItems = projects
            .OrderByDescending(project => currentProjectIds.Contains(project.Id))
            .ThenByDescending(project => project.UpdatedAt)
            .Select((project, index) => new AssetRelationPickerItem(
                project.Id,
                project.Name,
                $"{project.Status} · {project.Category}",
                Group: currentProjectIds.Contains(project.Id) ? "当前拍摄对应项目" : index < 8 ? "最近项目" : "全部项目"))
            .ToArray();
        ProjectPickerSearch = string.Empty;
        FilterProjectPicker();
        IsProjectPickerOpen = true;
    }

    private async Task OpenBookingPickerAsync()
    {
        IsProjectPickerOpen = false;
        var database = new PixelTartDatabase(RAWSelectionAssistant.Core.Utilities.AppDataPaths.DatabaseFile);
        var projectRepository = new SqliteProjectRepository(database);
        var projects = await projectRepository.ListAsync(_lifetimeCancellation.Token);
        var projectNames = projects.ToDictionary(project => project.Id, project => project.Name);
        var currentProjectIds = new HashSet<Guid>();
        foreach (var asset in SelectedAssets)
            foreach (var link in await _repository.ListProjectAssetLinksAsync(assetId: asset.AssetId, cancellationToken: _lifetimeCancellation.Token))
                currentProjectIds.Add(link.ProjectId);
        var bookings = await new SqliteShootBookingRepository(database).SearchAllUnarchivedAsync(new(null, PageSize: 100), _lifetimeCancellation.Token);
        _allBookingPickerItems = bookings.Items
            .OrderByDescending(item => item.ProjectId is Guid projectId && currentProjectIds.Contains(projectId))
            .ThenByDescending(item => item.StartAtUtc)
            .Select((booking, index) => new AssetRelationPickerItem(
                booking.Id,
                $"{booking.StartAtUtc.ToLocalTime():yyyy-MM-dd} · {booking.Title}",
                $"项目：{(booking.ProjectId is Guid projectId && projectNames.TryGetValue(projectId, out var projectName) ? projectName : "未关联")} · 客户：{ValueOrMissing(booking.ClientDisplayName)} · 地点：{booking.Location ?? "未填写"}",
                booking.ProjectId,
                booking.StartAtUtc,
                booking.ProjectId is Guid currentId && currentProjectIds.Contains(currentId) ? "当前项目下拍摄" : index < 12 ? "最近拍摄" : "全部拍摄"))
            .ToArray();
        BookingPickerSearch = string.Empty;
        FilterBookingPicker();
        IsBookingPickerOpen = true;
    }

    private void FilterProjectPicker()
    {
        var keyword = ProjectPickerSearch.Trim();
        ProjectPickerItems.Clear();
        foreach (var item in _allProjectPickerItems.Where(item => RelationMatches(item, keyword))) ProjectPickerItems.Add(item);
    }

    private void FilterBookingPicker()
    {
        var keyword = BookingPickerSearch.Trim();
        BookingPickerItems.Clear();
        foreach (var item in _allBookingPickerItems.Where(item => RelationMatches(item, keyword))) BookingPickerItems.Add(item);
    }

    private static bool RelationMatches(AssetRelationPickerItem item, string keyword) =>
        keyword.Length == 0 || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.Details.Contains(keyword, StringComparison.OrdinalIgnoreCase) || item.Group.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private async Task SelectProjectRelationAsync(AssetRelationPickerItem? item)
    {
        if (item is null) return;
        foreach (var asset in SelectedAssets) await _repository.SaveProjectAssetLinkAsync(new(item.Id, asset.AssetId, "Asset", DateTimeOffset.UtcNow), _lifetimeCancellation.Token).ConfigureAwait(false);
        IsProjectPickerOpen = false; Status = $"已关联项目：{item.Name}（{SelectedAssets.Count} 项）"; OnP2SelectionChanged(SelectedAssets.ToArray());
    }

    private async Task SelectBookingRelationAsync(AssetRelationPickerItem? item)
    {
        if (item is null) return;
        foreach (var asset in SelectedAssets) await _repository.SaveBookingAssetLinkAsync(new(item.Id, asset.AssetId, DateTimeOffset.UtcNow), _lifetimeCancellation.Token).ConfigureAwait(false);
        IsBookingPickerOpen = false; Status = $"已关联拍摄：{item.Name}（{SelectedAssets.Count} 项）"; OnP2SelectionChanged(SelectedAssets.ToArray());
    }

    private async Task RemoveProjectRelationAsync(AssetRelationPickerItem? item)
    {
        if (item is null) return;
        foreach (var asset in SelectedAssets) await _repository.RemoveProjectAssetLinkAsync(item.Id, asset.AssetId, _lifetimeCancellation.Token).ConfigureAwait(false);
        Status = $"已移除项目关联：{item.Name}"; OnP2SelectionChanged(SelectedAssets.ToArray());
    }

    private async Task RemoveBookingRelationAsync(AssetRelationPickerItem? item)
    {
        if (item is null) return;
        foreach (var asset in SelectedAssets) await _repository.RemoveBookingAssetLinkAsync(item.Id, asset.AssetId, _lifetimeCancellation.Token).ConfigureAwait(false);
        Status = $"已移除拍摄关联：{item.Name}"; OnP2SelectionChanged(SelectedAssets.ToArray());
    }

    public async Task SetInspectorWorkflowAsync(string? displayName)
    {
        var workflowStatus = displayName switch
        {
            "客户选择" => AssetWorkflowStatus.ClientSelected,
            "待精修" => AssetWorkflowStatus.PendingRetouch,
            "已精修" => AssetWorkflowStatus.Retouched,
            "已交付" => AssetWorkflowStatus.Delivered,
            _ => AssetWorkflowStatus.Unprocessed
        };
        var assets = SelectedAssets.ToArray();
        foreach (var asset in assets)
        {
            var existing = await _repository.GetAssetWorkflowMetadataAsync(asset.AssetId, _lifetimeCancellation.Token);
            await _repository.SaveAssetWorkflowMetadataAsync(new(asset.AssetId, existing?.AssetOrigin ?? "素材库", workflowStatus), _lifetimeCancellation.Token);
        }
        InspectorWorkflowStatus = WorkflowDisplayName(workflowStatus);
        Status = $"已更新 {assets.Length} 项工作流状态：{InspectorWorkflowStatus}。";
    }

    private async Task OpenCalendarBookingAsync(AssetRelationPickerItem? item)
    {
        if (item is null || _openCalendarBooking is null) return;
        await _openCalendarBooking(item.Id);
    }

    public async Task ApplyBookingFilterAsync(Guid bookingId)
    {
        var links = await _repository.ListBookingAssetLinksAsync(bookingId: bookingId, cancellationToken: _lifetimeCancellation.Token);
        await ApplyRelationshipFilterAsync(links.Select(link => link.AssetId).ToArray(), $"拍摄素材 · {bookingId:N}"[..23]);
    }

    public async Task ApplyProjectFilterAsync(Guid projectId)
    {
        var links = await _repository.ListProjectAssetLinksAsync(projectId: projectId, cancellationToken: _lifetimeCancellation.Token);
        await ApplyRelationshipFilterAsync(links.Select(link => link.AssetId).ToArray(), $"项目素材 · {projectId:N}"[..23]);
    }

    private async Task ApplyRelationshipFilterAsync(IReadOnlyList<Guid> assetIds, string description)
    {
        StopSearchDebounce();
        ResetVisualModeState();
        SetActiveCollectionWithoutRefresh(AssetLibrarySystemCollection.AllAssets);
        _selectedFolder = null;
        _selectedTag = null;
        ClearSmartFolderSelectionState();
        _searchText = string.Empty;
        _workspaceSettings.SearchText = string.Empty;
        _relationshipFilterAssetIds = assetIds.Distinct().ToArray();
        _relationshipFilterDescription = description;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedFolder));
        OnPropertyChanged(nameof(SelectedTag));
        OnPropertyChanged(nameof(SelectedSmartFolder));
        await RefreshAsync();
    }

    private async Task OpenCollectionsAsync()
    {
        var service = (SqliteInspirationTrayService)_inspirationTray;
        InspirationCollections.Clear(); foreach (var item in await service.ListCollectionsAsync(_lifetimeCancellation.Token).ConfigureAwait(false)) InspirationCollections.Add(item);
        IsCollectionPanelOpen = true;
    }

    private async Task CreateCollectionAsync()
    {
        var service = (SqliteInspirationTrayService)_inspirationTray;
        var created = await service.CreateCollectionAsync($"灵感集 {DateTime.Now:MMdd-HHmm}", cancellationToken: _lifetimeCancellation.Token).ConfigureAwait(false);
        InspirationCollections.Insert(0, created); Status = $"已创建灵感集：{created.Name}";
    }

    private async Task RenameCollectionAsync(InspirationCollectionSummary? collection)
    {
        if (collection is null) return;
        var name = $"{collection.Name} · {DateTime.Now:HHmm}";
        await ((SqliteInspirationTrayService)_inspirationTray).RenameCollectionAsync(collection.CollectionId, name, _lifetimeCancellation.Token);
        var index = InspirationCollections.IndexOf(collection);
        if (index >= 0) InspirationCollections[index] = collection with { Name = name, UpdatedAtUtc = DateTimeOffset.UtcNow };
        Status = $"已重命名灵感集：{name}";
    }

    private async Task SetCollectionProjectAsync(InspirationCollectionSummary? collection)
    {
        if (collection is null) return;
        var projects = await new SqliteProjectRepository(new PixelTartDatabase(RAWSelectionAssistant.Core.Utilities.AppDataPaths.DatabaseFile)).ListAsync(_lifetimeCancellation.Token);
        var project = projects.FirstOrDefault();
        if (project is null) { Status = "暂无可关联项目。"; return; }
        await ((SqliteInspirationTrayService)_inspirationTray).SetCollectionProjectAsync(collection.CollectionId, project.Id, _lifetimeCancellation.Token);
        var index = InspirationCollections.IndexOf(collection);
        if (index >= 0) InspirationCollections[index] = collection with { ProjectId = project.Id, UpdatedAtUtc = DateTimeOffset.UtcNow };
        Status = $"灵感集已关联项目：{project.Name}";
    }

    private async Task RemoveCollectionEntryAsync(InspirationTrayCardView? card)
    {
        if (card is null || ActiveCollectionId is not Guid collectionId) return;
        await ((SqliteInspirationTrayService)_inspirationTray).RemoveEntriesFromCollectionAsync(collectionId, [card.TrayEntryId], _lifetimeCancellation.Token);
        await OpenCollectionAsync(InspirationCollections.FirstOrDefault(item => item.CollectionId == collectionId));
        Status = "已从当前灵感集移除引用；托盘和源文件均未删除。";
    }

    private async Task OpenCollectionAsync(InspirationCollectionSummary? collection)
    {
        if (collection is null) return;
        ActiveCollectionId = collection.CollectionId;
        var entries = await ((SqliteInspirationTrayService)_inspirationTray).ListCollectionEntriesAsync(collection.CollectionId, _lifetimeCancellation.Token).ConfigureAwait(false);
        ActiveCollectionCards.Clear(); foreach (var card in entries.Select(entry => new InspirationTrayCardView(entry, null, entry.ResolutionState == InspirationTrayResolutionState.Resolved ? "本素材库" : "离线素材库"))) ActiveCollectionCards.Add(card);
    }

    private async Task ArchiveCollectionAsync(InspirationCollectionSummary? collection)
    {
        if (collection is null) return;
        await ((SqliteInspirationTrayService)_inspirationTray).ArchiveCollectionAsync(collection.CollectionId, _lifetimeCancellation.Token).ConfigureAwait(false);
        InspirationCollections.Remove(collection); if (ActiveCollectionId == collection.CollectionId) { ActiveCollectionId = null; ActiveCollectionCards.Clear(); }
        Status = $"已归档灵感集：{collection.Name}；素材与源文件均未删除。";
    }

    private async Task AddSelectionToCollectionAsync(InspirationCollectionSummary? collection)
    {
        if (collection is null) return;
        var refs = SelectedAssets.Where(asset => asset.ContentHash?.Length == 64).Select(asset => new AssetLibraryStableReference(_libraryIdForTray, asset.AssetId, asset.ContentHash!)).ToArray();
        var service = (SqliteInspirationTrayService)_inspirationTray;
        var added = await service.AddRangeAsync(refs, "asset-library-selection", _lifetimeCancellation.Token).ConfigureAwait(false);
        await service.AddEntriesToCollectionAsync(collection.CollectionId, added.AddedEntries.Select(entry => entry.TrayEntryId), _lifetimeCancellation.Token).ConfigureAwait(false);
        Status = $"已将 {added.AddedCount} 项加入灵感集：{collection.Name}";
        await OpenCollectionAsync(collection);
    }

    private async Task AddToInspirationTrayAsync(AssetVisualMatchView? card)
    {
        var assets = ContextIds(card).Select(id => SelectedAssets.FirstOrDefault(asset => asset.AssetId == id) ?? AssetCards.FirstOrDefault(item => item.Asset.AssetId == id)?.Asset).Where(asset => asset is not null).Cast<AssetItem>().ToArray();
        var refs = assets.Where(asset => !string.IsNullOrWhiteSpace(asset.ContentHash) && asset.ContentHash!.Length == 64).Select(asset => new AssetLibraryStableReference(_libraryIdForTray, asset.AssetId, asset.ContentHash!)).ToArray();
        if (refs.Length == 0) { Status = "所选素材缺少 SHA-256，暂不能加入灵感托盘。"; return; }
        var result = await _inspirationTray.AddRangeAsync(refs, P2QueryDescription);
        await RefreshInspirationTrayAsync();
        Status = $"灵感托盘：新增 {result.AddedCount} 项，已存在 {result.ExistingCount} 项。";
    }

    private Guid _libraryIdForTray => ResolveLibraryId(_databasePath);
    private static Guid ResolveLibraryId(string databasePath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(databasePath) ?? string.Empty);
        while (directory is not null)
        {
            var manifestPath = Path.Combine(directory.FullName, "library.manifest.json");
            try
            {
                if (File.Exists(manifestPath))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    if (document.RootElement.TryGetProperty("library_id", out var value) && Guid.TryParse(value.GetString(), out var id) && id != Guid.Empty)
                        return id;
                }
            }
            catch (JsonException) { }
            directory = directory.Parent;
        }
        return new Guid(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(databasePath)))[..16]);
    }
    private async Task RefreshInspirationTrayAsync()
    {
        var entries = await _inspirationTray.ListAsync(_lifetimeCancellation.Token);
        InspirationTrayEntries.ReplaceAll(entries);
        var cards = new List<InspirationTrayCardView>(entries.Count);
        foreach (var entry in entries)
        {
            var sameLibrary = entry.Reference.LibraryId == _libraryIdForTray;
            var asset = sameLibrary ? await _repository.GetAssetAsync(entry.Reference.AssetId, _lifetimeCancellation.Token) : null;
            cards.Add(new(entry, sameLibrary && asset is not null ? GetDisplaySourcePath(asset) : null,
                sameLibrary && asset is not null ? "本素材库" : "离线素材库"));
        }
        InspirationTrayCards.ReplaceAll(cards);
        InspirationTrayCount = entries.Count;
        ClearInspirationTrayCommand.RaiseCanExecuteChanged();
    }

    private async Task RemoveInspirationTrayEntryAsync(InspirationTrayCardView? card)
    {
        if (card is null) return;
        await _inspirationTray.RemoveAsync(card.Entry.TrayEntryId, _lifetimeCancellation.Token);
        await RefreshInspirationTrayAsync();
        Status = "已从灵感托盘移除；素材引用和源文件均未删除。";
    }

    private async Task ToggleInspirationTrayAsync()
    {
        IsInspirationTrayOpen = !IsInspirationTrayOpen;
        if (IsInspirationTrayOpen) await RefreshInspirationTrayAsync();
    }

    private async Task ClearInspirationTrayAsync()
    {
        await _inspirationTray.ClearAsync(_lifetimeCancellation.Token);
        await RefreshInspirationTrayAsync();
        Status = "灵感托盘已清空；素材引用和源文件均未删除。";
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
        SystemCollections.Add(new(this, AssetLibrarySystemCollection.RecycleBin, "回收站", "可恢复素材；不会删除源文件", "AssetLibraryRecycleBin"));
    }

    internal void SelectSystemCollection(AssetLibrarySystemCollection collection)
    {
        _changingP2QuerySource = true;
        try
        {
            _selectedFolder = null; _selectedTag = null; ClearSmartFolderSelectionState();
            _workspaceSettings.SelectedFolderId = null; _workspaceSettings.SelectedTagId = null; _workspaceSettings.SelectedSmartFolderId = null;
            OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder));
        }
        finally { _changingP2QuerySource = false; }
        SetActiveCollectionWithoutRefresh(collection);
        OnP3QuerySourceChanged();
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
                _selectedFolder = null; _selectedTag = null; ClearSmartFolderSelectionState();
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
                _selectedTag = null; ClearSmartFolderSelectionState();
                _workspaceSettings.SelectedTagId = null; _workspaceSettings.SelectedSmartFolderId = null;
                OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder));
            }
            else if (tag is not null)
            {
                _selectedFolder = null; ClearSmartFolderSelectionState();
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
        OnP3QuerySourceChanged();
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
        if (SelectedSmartFolder?.SmartFolderId != node.Folder.SmartFolderId) SelectedSmartFolder = node.Folder;
        OpenP3SmartFolderEditor(node.Folder);
        Status = "已打开智能文件夹通用规则编辑器。";
    }

    private void ClearSmartFolderSelectionState()
    {
        _selectedSmartFolder = null;
        ClearSmartFolderEditorState();
    }

    internal bool IsTagGroupExpanded(Guid groupId) => _workspaceSettings.ExpandedTagGroupIds.Contains(groupId);
    internal void RememberTagGroupExpanded(Guid groupId, bool expanded)
    {
        if (expanded)
        {
            if (!_workspaceSettings.ExpandedTagGroupIds.Contains(groupId)) _workspaceSettings.ExpandedTagGroupIds.Add(groupId);
        }
        else _workspaceSettings.ExpandedTagGroupIds.Remove(groupId);
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
            RememberBrowserMutationResult(result);
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
            RememberBrowserMutationResult(result);
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
        RememberBrowserMutationResult(result);
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
        RememberBrowserMutationResult(result);
        await RefreshFilterListsAsync(_lifetimeCancellation.Token);
        Status = $"已将“{node.Name}”提升一级。";
    }

    private async Task RefreshP2OrganizationAsync(
        CancellationToken cancellationToken,
        IReadOnlyList<AssetFolderTreeItem>? prefetchedTree = null,
        bool refreshTagSummaryCache = true)
    {
        IsOrganizationLoading = true;
        OrganizationError = string.Empty;
        try
        {
            var tagNames = Tags.ToDictionary(tag => tag.TagId, tag => tag.Name);
            var data = await Task.Run(async () =>
            {
                var tree = prefetchedTree ?? await _repository.GetFolderTreeAsync(includeArchived: true, cancellationToken);
                if (!refreshTagSummaryCache)
                    return (Tree: tree, TagSummaries: (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>());
                var memberships = await _repository.ListTagMembershipsAsync(cancellationToken: cancellationToken);
                var summaries = memberships
                    .GroupBy(item => item.AssetId)
                    .ToDictionary(
                        group => group.Key,
                        group => string.Join("、", group
                            .Select(item => tagNames.GetValueOrDefault(item.TagId))
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)));
                return (Tree: tree, TagSummaries: (IReadOnlyDictionary<Guid, string>)summaries);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var tree = data.Tree;
            OrganizationFolders.Clear(); foreach (var node in tree) OrganizationFolders.Add(new(this, node));
            OrganizationSmartFolders.Clear(); foreach (var folder in SmartFolders) OrganizationSmartFolders.Add(new(this, folder));
            OrganizationTagGroups.Clear();
            var tagViews = Tags.Select(tag => new AssetLibraryTagNodeView(this, tag)).ToArray();
            foreach (var group in TagGroups)
                OrganizationTagGroups.Add(new(this, group, tagViews.Where(tag => tag.Tag.TagGroupId == group.TagGroupId)));
            var ungrouped = tagViews.Where(tag => tag.Tag.TagGroupId is null || TagGroups.All(group => group.TagGroupId != tag.Tag.TagGroupId)).ToArray();
            if (ungrouped.Length > 0) OrganizationTagGroups.Add(new(this, null, ungrouped));
            if (refreshTagSummaryCache)
            {
                _p2TagSummaryByAsset.Clear();
                foreach (var summary in data.TagSummaries)
                    _p2TagSummaryByAsset[summary.Key] = summary.Value;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { OrganizationError = $"组织栏加载失败：{exception.Message}"; }
        finally { IsOrganizationLoading = false; NotifyP2OrganizationState(); }
    }

    private void ApplyP2BatchTagSummaryChanges(AssetBatchMetadataRequest request)
    {
        var addIds = (request.AddTagIds ?? []).ToHashSet();
        var removeIds = (request.RemoveTagIds ?? []).Where(id => !addIds.Contains(id)).ToHashSet();
        if (addIds.Count == 0 && removeIds.Count == 0) return;
        var addNames = Tags.Where(tag => addIds.Contains(tag.TagId)).Select(tag => tag.Name).ToArray();
        var removeNames = Tags.Where(tag => removeIds.Contains(tag.TagId)).Select(tag => tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var assetId in request.AssetIds.Distinct())
        {
            var names = _p2TagSummaryByAsset.TryGetValue(assetId, out var summary)
                ? summary.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : [];
            names.RemoveAll(removeNames.Contains);
            foreach (var name in addNames)
                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
            if (names.Count == 0) _p2TagSummaryByAsset.Remove(assetId);
            else _p2TagSummaryByAsset[assetId] = string.Join("、", names);
        }
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
        NotifyP3QueryResultChanged();
    }

    private void UpdateP2QueryDescription()
    {
        P2QueryDescription = _relationshipFilterDescription
            ?? SelectedFolder?.Name
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

    private Task _p2InspectorTask = Task.CompletedTask;

    private void OnP2SelectionChanged(IReadOnlyList<AssetItem> selected)
    {
        OnPropertyChanged(nameof(IsQueryInspectorVisible));
        OnPropertyChanged(nameof(IsSingleInspectorVisible));
        OnPropertyChanged(nameof(IsMultipleInspectorVisible));
        var generation = Interlocked.Increment(ref _inspectorGeneration);
        var count = SelectionCount;
        _p2InspectorTask = RunTrackedP3OperationAsync(() => RefreshP2InspectorAsync(selected, count, generation));
    }

    private async Task RefreshP2InspectorAsync(IReadOnlyList<AssetItem> selected, int selectedIdCount, long generation)
    {
        using var timing = RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryOperationTiming.Measure("viewmodel.inspector");
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
            var memberships = await Task.Run(async () => (
                Folders: await _repository.ListFolderMembershipsAsync(cancellationToken: _lifetimeCancellation.Token),
                Tags: await _repository.ListTagMembershipsAsync(cancellationToken: _lifetimeCancellation.Token)), _lifetimeCancellation.Token);
            var folderMemberships = memberships.Folders;
            var tagMemberships = memberships.Tags;
            if (generation != Volatile.Read(ref _inspectorGeneration)) return;
            if (selected.Count == 1)
            {
                var id = selected[0].AssetId;
                SingleFolderSummary = JoinNames(folderMemberships.Where(item => item.AssetId == id).Select(item => Folders.FirstOrDefault(folder => folder.FolderId == item.FolderId)?.Name));
                SingleTagSummary = JoinNames(tagMemberships.Where(item => item.AssetId == id).Select(item => Tags.FirstOrDefault(tag => tag.TagId == item.TagId)?.Name));
                var workflow = await _repository.GetAssetWorkflowMetadataAsync(id, _lifetimeCancellation.Token);
                var projectLinks = await _repository.ListProjectAssetLinksAsync(assetId: id, cancellationToken: _lifetimeCancellation.Token);
                var bookingLinks = await _repository.ListBookingAssetLinksAsync(assetId: id, cancellationToken: _lifetimeCancellation.Token);
                if (generation != Volatile.Read(ref _inspectorGeneration)) return;
                InspectorProjectLinks.Clear(); InspectorBookingLinks.Clear();
                var mainDatabase = new PixelTartDatabase(RAWSelectionAssistant.Core.Utilities.AppDataPaths.DatabaseFile);
                var projects = await new SqliteProjectRepository(mainDatabase).ListAsync(_lifetimeCancellation.Token);
                var bookingRepository = new SqliteShootBookingRepository(mainDatabase);
                var linkedBookings = new List<ShootBooking>();
                foreach (var link in bookingLinks)
                {
                    var booking = await bookingRepository.GetAsync(link.BookingId, includeArchived: true, cancellationToken: _lifetimeCancellation.Token);
                    if (booking is not null) linkedBookings.Add(booking);
                }
                IReadOnlyList<SelectionProject> onlineProjects = [];
                try
                {
                    onlineProjects = (await new JsonSelectionWorkspaceStore(RAWSelectionAssistant.Core.Utilities.AppDataPaths.OnlineSelectionWorkspaceFile).LoadAsync(_lifetimeCancellation.Token)).Projects;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    _logService?.Error("在线选片客户显示信息暂不可用。", exception);
                }
                var linkedProjectNames = projectLinks
                    .Select(link => projects.FirstOrDefault(project => project.Id == link.ProjectId)?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var projectClientNames = onlineProjects
                    .Where(project => linkedProjectNames.Contains(project.Name))
                    .Select(project => project.ClientDisplayName);
                foreach (var link in projectLinks)
                {
                    var project = projects.FirstOrDefault(item => item.Id == link.ProjectId);
                    InspectorProjectLinks.Add(new(link.ProjectId, project?.Name ?? $"项目 {link.ProjectId:N}"[..15], link.Role));
                }
                foreach (var link in bookingLinks)
                {
                    var booking = linkedBookings.FirstOrDefault(item => item.Id == link.BookingId);
                    var projectName = booking?.ProjectId is Guid projectId ? projects.FirstOrDefault(project => project.Id == projectId)?.Name : null;
                    InspectorBookingLinks.Add(new(
                        link.BookingId,
                        booking?.Title ?? $"拍摄 {link.BookingId:N}"[..15],
                        booking is null ? "未找到拍摄记录" : $"{booking.StartAtUtc.ToLocalTime():yyyy-MM-dd} · 项目：{projectName ?? "未关联"} · 客户：{ValueOrMissing(booking.ClientDisplayName)} · 地点：{booking.Location ?? "未填写"}",
                        booking?.ProjectId,
                        booking?.StartAtUtc));
                }
                InspectorAssetOrigin = workflow?.AssetOrigin ?? "未指定";
                InspectorWorkflowStatus = WorkflowDisplayName(workflow?.WorkflowStatus ?? AssetWorkflowStatus.Unprocessed);
                InspectorProject = InspectorProjectLinks.Count == 0 ? "未关联" : string.Join("、", InspectorProjectLinks.Select(link => link.Name));
                InspectorBooking = InspectorBookingLinks.Count == 0 ? "未关联" : string.Join("、", InspectorBookingLinks.Select(link => link.Name));
                InspectorClient = new ClientDisplayResolver().Resolve(linkedBookings.Select(booking => booking.ClientDisplayName), projectClientNames);
                return;
            }
            var commonFolderIds = folderMemberships.Where(item => ids.Contains(item.AssetId)).GroupBy(item => item.FolderId).Where(group => group.Select(item => item.AssetId).Distinct().Count() == ids.Count).Select(group => group.Key);
            var commonTagIds = tagMemberships.Where(item => ids.Contains(item.AssetId)).GroupBy(item => item.TagId).Where(group => group.Select(item => item.AssetId).Distinct().Count() == ids.Count).Select(group => group.Key);
            MultipleFolderSummary = JoinNames(commonFolderIds.Select(id => Folders.FirstOrDefault(folder => folder.FolderId == id)?.Name));
            MultipleTagSummary = JoinNames(commonTagIds.Select(id => Tags.FirstOrDefault(tag => tag.TagId == id)?.Name));
            MultipleRatingSummary = selected.Select(asset => asset.Rating).Distinct().Take(2).Count() == 1 ? $"共同评分：{selected[0].Rating}" : "评分：混合值";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _inspectorGeneration)) return;
            _logService?.Error("检查器信息加载失败。", exception);
            SingleFolderSummary = SingleTagSummary = MultipleFolderSummary = MultipleTagSummary = MultipleRatingSummary = "检查器信息暂不可用，请重试。";
            Status = "检查器信息加载失败，请重试。";
        }
        finally
        {
            if (generation == Volatile.Read(ref _inspectorGeneration)) OnPropertyChanged(nameof(P3BatchCommonState));
        }
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

    internal IReadOnlyList<Guid> GetDragAssetIds() => SelectedAssetIds.Where(id => id != Guid.Empty).Distinct().ToArray();

    /// <summary>
    /// Synchronous gate used by WPF drag events.  It intentionally only allows targets that
    /// are present in the currently loaded, non-archived filter lists; the command service
    /// repeats the check asynchronously immediately before mutation to close stale-target
    /// races and reject forged/nonexistent target ids.
    /// </summary>
    internal bool CanDropOn(AssetLibraryDropTarget? target)
    {
        if (target is null || SelectionCount == 0 || target.IsArchived) return false;
        return target.Kind switch
        {
            AssetLibraryDropTargetKind.Folder => target.TargetId is Guid folderId && folderId != Guid.Empty
                && Folders.Any(folder => folder.FolderId == folderId && !folder.IsArchived),
            AssetLibraryDropTargetKind.Tag => target.TargetId is Guid tagId && tagId != Guid.Empty
                && Tags.Any(tag => tag.TagId == tagId && !tag.IsArchived),
            AssetLibraryDropTargetKind.RemoveFromCurrent => SelectedFolder is { IsArchived: false } || SelectedTag is { IsArchived: false },
            _ => false
        };
    }

    /// <summary>Rejects data objects that do not exactly describe the current selection.</summary>
    internal bool CanDropPayload(IEnumerable<Guid>? payload)
    {
        var incoming = payload?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        var selected = GetDragAssetIds();
        return incoming.Length > 0 && selected.Count > 0 && incoming.ToHashSet().SetEquals(selected);
    }

    internal async Task<AssetLibraryCommandPreview> PreviewDropAsync(AssetLibraryDropTarget? target)
    {
        var ids = GetDragAssetIds();
        try
        {
            var preview = await BuildDropPreviewAsync(ids, target).ConfigureAwait(true);
            return PublishDropPreview(preview);
        }
        catch (OperationCanceledException)
        {
            return PublishDropPreview(new(false, ids.Count, 0, 0, "拖放预览已取消。", "canceled", nameof(OperationCanceledException), true));
        }
        catch (Exception exception)
        {
            return PublishDropPreview(CreateDropFailurePreview("预览", ids.Count, exception));
        }
    }

    internal async Task ExecuteDropAsync(AssetLibraryDropTarget? target)
    {
        var preview = await PreviewDropAsync(target).ConfigureAwait(true);
        if (!preview.IsAllowed) return;

        var ids = GetDragAssetIds();
        try
        {
            AssetLibraryBatchResult result;
            if (target?.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent)
            {
                // Snapshot the selected scope before awaiting the repository.  A selection
                // change during the operation must not redirect the mutation to another
                // folder/tag.
                var folder = SelectedFolder;
                var tag = SelectedTag;
                result = folder is { IsArchived: false }
                    ? await _browserCommands.RemoveFromFolderAsync(ids, folder.FolderId, _lifetimeCancellation.Token).ConfigureAwait(true)
                    : tag is { IsArchived: false }
                        ? await _browserCommands.RemoveTagAsync(ids, tag.TagId, _lifetimeCancellation.Token).ConfigureAwait(true)
                        : new AssetLibraryBatchResult(0, null, ["当前归属已失效，已拒绝移出操作。"]);
            }
            else
            {
                result = await _browserCommands.ExecuteDropAsync(ids, target!, _lifetimeCancellation.Token).ConfigureAwait(true);
            }

            if (result.Warnings.Count > 0)
            {
                PublishDropPreview(new(
                    false,
                    ids.Count,
                    preview.ConflictCount,
                    result.ChangedCount,
                    $"{preview.Message} 未完成：{string.Join("；", result.Warnings)}",
                    "mutation-warning"));
                return;
            }

            Status = result.ChangedCount == 0
                ? $"{preview.Message} 无需变更。"
                : $"{preview.Message} 已完成 {result.ChangedCount} 项，可撤销。";
            RaiseP2CommandStates();
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            PublishDropPreview(new(false, ids.Count, preview.ConflictCount, 0, "拖放执行已取消，未写入任何素材归属。", "canceled", nameof(OperationCanceledException), true));
        }
        catch (Exception exception)
        {
            PublishDropPreview(CreateDropFailurePreview("执行", ids.Count, exception, preview));
        }
    }

    internal void ReportDropRejected(AssetLibraryDropTarget? target)
    {
        var message = target?.IsArchived == true
            ? $"“{(string.IsNullOrWhiteSpace(target.Name) ? "未命名目标" : target.Name.Trim())}”已归档，不能接收素材。"
            : "当前拖放目标不存在、已归档或不支持写入，已拒绝操作。";
        PublishDropPreview(new(false, GetDragAssetIds().Count, 0, 0, message, target?.IsArchived == true ? "archived-target" : "invalid-target"));
    }

    /// <summary>
    /// Last-resort reporting seam for WPF's async-void drag events.  The normal preview and
    /// execute paths already catch their own failures; this keeps an unexpected event-layer
    /// exception visible in the status bar instead of becoming an unhandled UI crash.
    /// </summary>
    internal void ReportDropFailure(string phase, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            PublishDropPreview(new(false, GetDragAssetIds().Count, 0, 0,
                $"拖放{phase}已取消。", "canceled", nameof(OperationCanceledException), true));
            return;
        }
        PublishDropPreview(CreateDropFailurePreview(phase, GetDragAssetIds().Count, exception));
    }

    private async Task<AssetLibraryCommandPreview> BuildDropPreviewAsync(
        IReadOnlyList<Guid> ids,
        AssetLibraryDropTarget? target)
    {
        if (target?.Kind == AssetLibraryDropTargetKind.RemoveFromCurrent)
        {
            var folder = SelectedFolder;
            var tag = SelectedTag;
            return folder is { IsArchived: false }
                ? await _browserCommands.PreviewRemoveAsync(ids, folder.FolderId, folder: true, folder.Name, _lifetimeCancellation.Token).ConfigureAwait(true)
                : tag is { IsArchived: false }
                    ? await _browserCommands.PreviewRemoveAsync(ids, tag.TagId, folder: false, tag.Name, _lifetimeCancellation.Token).ConfigureAwait(true)
                    : new(false, ids.Count, 0, 0, "当前查询没有可移出的文件夹或标签归属。", "invalid-target");
        }

        if (target is null)
            return new(false, ids.Count, 0, 0, "拖放目标无效，已拒绝操作。", "invalid-target");
        return await _browserCommands.PreviewDropAsync(ids, target, _lifetimeCancellation.Token).ConfigureAwait(true);
    }

    private AssetLibraryCommandPreview PublishDropPreview(AssetLibraryCommandPreview preview)
    {
        LastDropPreview = preview;
        Status = preview.Message;
        return preview;
    }

    private static AssetLibraryCommandPreview CreateDropFailurePreview(
        string phase,
        int requestedCount,
        Exception exception,
        AssetLibraryCommandPreview? previous = null) =>
        new(false, requestedCount, previous?.ConflictCount ?? 0, 0,
            $"拖放{phase}失败：{exception.Message}", "exception", exception.GetType().Name);

    private IReadOnlyList<Guid> ContextIds(AssetVisualMatchView? card)
    {
        if (card is null) return [];
        return SelectedAssetIds.Contains(card.Asset.AssetId)
            ? SelectedAssetIds.ToArray()
            : [card.Asset.AssetId];
    }

    private IReadOnlyList<AssetItem> ContextAssets(AssetVisualMatchView? card)
    {
        var ids = ContextIds(card).ToHashSet();
        return AssetCards.Select(item => item.Asset).Where(asset => ids.Contains(asset.AssetId)).ToArray();
    }

    private async Task SetContextWorkflowAsync(AssetVisualMatchView? card, AssetWorkflowStatus workflowStatus)
    {
        if (card is null) return;
        var assets = ContextAssets(card);
        foreach (var asset in assets)
        {
            var existing = await _repository.GetAssetWorkflowMetadataAsync(asset.AssetId, _lifetimeCancellation.Token);
            await _repository.SaveAssetWorkflowMetadataAsync(new(asset.AssetId, existing?.AssetOrigin ?? "素材库", workflowStatus), _lifetimeCancellation.Token);
        }
        Status = $"已更新 {assets.Count} 项工作流状态。";
        OnP2SelectionChanged(SelectedAssets.ToArray());
        await _p2InspectorTask;
    }

    private static string WorkflowDisplayName(AssetWorkflowStatus workflowStatus) => workflowStatus switch
    {
        AssetWorkflowStatus.ClientSelected => "客户选择",
        AssetWorkflowStatus.PendingRetouch => "待精修",
        AssetWorkflowStatus.Retouched => "已精修",
        AssetWorkflowStatus.Delivered => "已交付",
        _ => "未处理"
    };

    private async Task ExportContextFilesAsync(AssetVisualMatchView? card, bool preferManagedCopy)
    {
        if (card is null) return;
        var dialog = new OpenFolderDialog { Title = preferManagedCopy ? "导出托管副本" : "导出原文件副本", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var result = await new AssetSelectionExportService().ExportFilesAsync(ContextAssets(card), dialog.FolderName, preferManagedCopy, _lifetimeCancellation.Token);
            Status = $"已导出 {result.ExportedCount} 项，跳过缺失 {result.MissingCount} 项；同名文件已自动编号。";
        }
        catch (Exception exception) { Status = $"导出失败：{exception.Message}"; }
    }

    private async Task ExportContextMetadataAsync(AssetVisualMatchView? card)
    {
        if (card is null) return;
        var dialog = new SaveFileDialog { Title = "导出素材元数据", Filter = "CSV 文件 (*.csv)|*.csv", DefaultExt = ".csv", AddExtension = true, OverwritePrompt = false, FileName = "pixel-tart-assets.csv" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await new AssetSelectionExportService().ExportMetadataCsvAsync(ContextAssets(card), dialog.FileName, _lifetimeCancellation.Token);
            Status = "素材元数据已导出；现有文件不会被覆盖。";
        }
        catch (IOException) { Status = "目标 CSV 已存在；为避免覆盖，导出已取消。"; }
        catch (Exception exception) { Status = $"导出失败：{exception.Message}"; }
    }

    private async Task CopyContextPathAsync(AssetVisualMatchView? card)
    {
        if (card is null) return;
        try { await _browserCommands.CopyPathAsync(card.Asset.SourcePath); Status = "路径已复制；未修改源文件。"; }
        catch (Exception exception) { Status = $"复制路径失败：{exception.Message}"; }
    }

    private Task OpenContextViewerAsync(AssetVisualMatchView? card)
    {
        if (card is null) return Task.CompletedTask;
        var paths = AssetCards.Select(item => GetDisplaySourcePath(item.Asset)).Where(path => path.Length != 0).ToArray();
        var index = Array.IndexOf(paths, GetDisplaySourcePath(card.Asset));
        new AssetViewerWindow(paths, Math.Max(0, index)).Show();
        return Task.CompletedTask;
    }

    private Task OpenContextExternalAsync(AssetVisualMatchView? card)
    {
        var path = card is null ? string.Empty : GetDisplaySourcePath(card.Asset);
        if (path.Length == 0 || !File.Exists(path)) { Status = "原文件不存在，无法安全打开。"; return Task.CompletedTask; }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            Status = "已请求默认应用打开；未修改源文件。";
        }
        catch (Exception exception) { Status = $"打开原文件失败：{exception.Message}"; }
        return Task.CompletedTask;
    }

    private Task RevealContextAsync(AssetVisualMatchView? card)
    {
        var path = card is null ? string.Empty : GetDisplaySourcePath(card.Asset);
        if (path.Length == 0 || !File.Exists(path)) { Status = "原文件不存在，无法定位。"; return Task.CompletedTask; }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            Status = "已在资源管理器中定位；未修改源文件。";
        }
        catch (Exception exception) { Status = $"定位原文件失败：{exception.Message}"; }
        return Task.CompletedTask;
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

    private async Task SetContextTrashedAsync(AssetVisualMatchView? card, bool trashed)
    {
        if (card is null) return;
        var ids = ContextIds(card);
        var result = await _browserCommands.SetTrashedAsync(ids, trashed, _lifetimeCancellation.Token);
        RemoveSelectedIds(ids);
        Status = trashed ? $"已将 {result.ChangedCount} 项移入可恢复回收站；源文件未删除。" : $"已恢复 {result.ChangedCount} 项，并保留原归档状态。";
        RaiseP2CommandStates(); await RefreshAsync();
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
        await ExecuteP2JournalAsync(undo: true);
    }
    private async Task RedoP2Async()
    {
        await ExecuteP2JournalAsync(undo: false);
    }

    private async Task ExecuteP2JournalAsync(bool undo)
    {
        _p2JournalBusy = true;
        RaiseP2CommandStates();
        try
        {
            var changed = await Task.Run(() => undo
                ? _browserCommands.UndoAsync(_lifetimeCancellation.Token)
                : _browserCommands.RedoAsync(_lifetimeCancellation.Token), _lifetimeCancellation.Token);
            Status = changed ? (undo ? "已撤销素材库操作。" : "已重做素材库操作。") : "没有可恢复的素材库操作。";
            LastUndoToken = _browserCommands.UndoToken;
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        finally { _p2JournalBusy = false; RaiseP2CommandStates(); }
    }

    private void RememberBrowserMutationResult(AssetLibraryBatchResult result)
    {
        _browserCommands.RememberExternalResult(result);
        LastUndoToken = _browserCommands.UndoToken;
        RaiseActions();
        RaiseP2CommandStates();
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

public sealed record InspirationTrayCardView(InspirationTrayEntry Entry, string? ThumbnailPath, string SourceBadge)
{
    public Guid TrayEntryId => Entry.TrayEntryId;
    public string AssetLabel => Entry.Reference.AssetId.ToString("N")[..8];
    public string ResolutionLabel => Entry.ResolutionState switch
    {
        InspirationTrayResolutionState.Resolved => SourceBadge,
        InspirationTrayResolutionState.LibraryOffline => "素材库离线",
        InspirationTrayResolutionState.AssetMissing => "文件缺失",
        _ => "引用校验失败"
    };
    public bool IsOffline => Entry.ResolutionState is InspirationTrayResolutionState.LibraryOffline or InspirationTrayResolutionState.HashMismatch || SourceBadge == "离线素材库";
}

public sealed record AssetLibraryViewModeChangedEventArgs(AssetLibraryViewMode Previous, AssetLibraryViewMode Current);
