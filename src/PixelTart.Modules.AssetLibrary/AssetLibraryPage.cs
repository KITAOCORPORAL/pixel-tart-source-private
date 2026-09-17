using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage : UserControl, IAsyncDisposable
{
    private readonly AssetLibraryViewModel _viewModel;
    private readonly bool _enablePreviewFeatures;
    private readonly string? _demoDirectory;
    private bool _initialized;
    private bool _disposed;
    private readonly object _disposeSync = new();
    private Task? _disposeTask;
    private bool _applyingViewModelSelection;
    // WPF raises one SelectionChanged event for every item added to SelectedItems.
    // Keep the grid event path at one dispatcher turn so a bulk selection performs
    // one view-model synchronization (and one set of inspector queries) instead
    // of doing the same work once per item.
    private DispatcherOperation? _pendingSelectionSync;
    private DispatcherOperation? _pendingPaneWidthCommit;
    private Guid? _viewTransitionAnchor;
    private bool _isMarqueeSelecting;
    private Point _marqueeStart;
    private bool _marqueeControlSelection;
    private bool _marqueeShiftSelection;
    private HashSet<Guid> _marqueeBaseSelection = [];
    private readonly IAssetPreviewProvider _previewProvider;
    private CancellationTokenSource? _quickLoupeCancellation;
    private AssetVisualMatchView? _quickLoupeCard;

    public AssetLibraryPage()
        : this(
            Path.Combine(Path.GetTempPath(), "PixelTart.ModuleContract", "asset-library-v16.db"),
            new TaskOperationBridge(),
            [])
    {
    }

    public AssetLibraryPage(
        string databasePath,
        TaskOperationBridge taskOperationBridge,
        IReadOnlyList<AssetLibraryModuleDiagnostic> moduleDiagnostics,
        bool enablePreviewFeatures = false,
        string? demoDirectory = null,
        AssetLibraryWorkspaceSettings? workspaceSettings = null,
        ILogService? logService = null,
        IAssetLibraryLoadStateController? loadStateController = null,
        bool focusedChrome = false,
        Func<Guid, Task>? openCalendarBooking = null,
        string? productDatabasePath = null,
        string? onlineSelectionWorkspaceFile = null,
        string? inspirationTrayDatabasePath = null)
    {
        InitializeComponent();
        _ = focusedChrome; // Compatibility switch; the migrated toolbar is now the only chrome.
        var thumbnailProvider = new WpfAssetThumbnailProvider(ResolvePreviewCacheDirectory(databasePath));
        _previewProvider = thumbnailProvider;
        AsyncThumbnail.Provider = thumbnailProvider;
        AsyncThumbnail.SetScopedProvider(this, thumbnailProvider);
        _enablePreviewFeatures = enablePreviewFeatures && loadStateController?.DisablePreviewFixtures != true;
        _demoDirectory = _enablePreviewFeatures ? demoDirectory : null;
        _viewModel = new AssetLibraryViewModel(
            databasePath,
            taskOperationBridge,
            moduleDiagnostics,
            _enablePreviewFeatures,
            workspaceSettings,
            logService,
            loadStateController,
            openCalendarBooking,
            productDatabasePath,
            onlineSelectionWorkspaceFile,
            inspirationTrayDatabasePath,
            thumbnailProvider);
        _viewModel.SelectionRestoreRequested += ViewModel_SelectionRestoreRequested;
        _viewModel.ViewModeChanging += ViewModel_ViewModeChanging;
        _viewModel.ViewModeChanged += ViewModel_ViewModeChanged;
        AssetGrid.PreviewMouseLeftButtonDown += AssetGrid_PreviewMouseLeftButtonDown;
        AssetGrid.PreviewMouseMove += AssetGrid_PreviewMouseMove;
        AssetGrid.MouseLeave += AssetGrid_MouseLeave;
        AssetGrid.PreviewMouseLeftButtonUp += AssetGrid_PreviewMouseLeftButtonUp;
        AssetGrid.LostMouseCapture += AssetGrid_LostMouseCapture;
        AssetGrid.PreviewMouseRightButtonDown += AssetGrid_PreviewMouseRightButtonDown;
        AssetGrid.MouseDoubleClick += AssetGrid_MouseDoubleClick;
        TextCompositionManager.AddPreviewTextInputStartHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_CompositionStarted);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_CompositionUpdated);
        TextCompositionManager.AddTextInputHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_TextInputCompleted);
        DataContext = _viewModel;
    }

    public AssetLibraryViewModel ViewModel => _viewModel;

    private static string? ResolvePreviewCacheDirectory(string databasePath)
    {
        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (string.IsNullOrWhiteSpace(databaseDirectory)) return null;
        var parent = Directory.GetParent(databaseDirectory);
        return parent is not null && string.Equals(new DirectoryInfo(databaseDirectory).Name, "database", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(parent.FullName, "previews")
            : Path.Combine(databaseDirectory, "previews");
    }

    public async Task RefreshForSessionAsync()
    {
        if (!_viewModel.RefreshCommand.CanExecute(null)) return;
        _viewModel.RefreshCommand.Execute(null);
        await _viewModel.RefreshCommand.ExecutionTask;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeForSessionAsync();
    }

    /// <summary>Initializes a page before it is placed in a visible host during a library switch.</summary>
    public async Task InitializeForSessionAsync()
    {
        if (_initialized || _disposed) return;
        _initialized = true;
        _viewModel.UpdateViewportWidth(ActualWidth);
        try
        {
            await _viewModel.InitializeAsync();
            if (_enablePreviewFeatures && !string.IsNullOrWhiteSpace(_demoDirectory) && Directory.Exists(_demoDirectory))
                await _viewModel.ImportDemoDirectoryAsync(_demoDirectory);
            UpdateGridDiagnostics();
        }
        catch (Exception)
        {
            _viewModel.SetForegroundError("素材库加载失败。请检查数据目录权限后重试。");
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed || Application.Current?.MainWindow?.IsLoaded == true) return;
        await DisposeAsync();
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? starter = null;
        Task task;
        lock (_disposeSync)
        {
            if (_disposeTask is null)
            {
                starter = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = starter.Task;
            }
            task = _disposeTask;
        }
        if (starter is not null) _ = CompleteDisposeAsync(starter);
        return new ValueTask(task);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        try { await DisposeCoreAsync(); completion.TrySetResult(); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        _pendingSelectionSync?.Abort();
        _pendingSelectionSync = null;
        _pendingPaneWidthCommit?.Abort();
        _pendingPaneWidthCommit = null;
        _viewModel.SelectionRestoreRequested -= ViewModel_SelectionRestoreRequested;
        _viewModel.ViewModeChanging -= ViewModel_ViewModeChanging;
        _viewModel.ViewModeChanged -= ViewModel_ViewModeChanged;
        AssetGrid.PreviewMouseLeftButtonDown -= AssetGrid_PreviewMouseLeftButtonDown;
        AssetGrid.PreviewMouseMove -= AssetGrid_PreviewMouseMove;
        AssetGrid.MouseLeave -= AssetGrid_MouseLeave;
        AssetGrid.PreviewMouseLeftButtonUp -= AssetGrid_PreviewMouseLeftButtonUp;
        AssetGrid.LostMouseCapture -= AssetGrid_LostMouseCapture;
        AssetGrid.PreviewMouseRightButtonDown -= AssetGrid_PreviewMouseRightButtonDown;
        AssetGrid.MouseDoubleClick -= AssetGrid_MouseDoubleClick;
        TextCompositionManager.RemovePreviewTextInputStartHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_CompositionStarted);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_CompositionUpdated);
        TextCompositionManager.RemoveTextInputHandler(AssetLibrarySearchBox, AssetLibrarySearchBox_TextInputCompleted);
        CancelMarqueeSelection();
        HideQuickLoupe();
        await AsyncThumbnail.CancelAndDrainAsync(this);
        await _viewModel.DisposeAsync();
    }

    private void AssetLibrarySearchBox_CompositionStarted(object sender, TextCompositionEventArgs e) =>
        _viewModel.BeginP3SearchComposition();

    private void AssetLibrarySearchBox_CompositionUpdated(object sender, TextCompositionEventArgs e) =>
        _viewModel.UpdateP3SearchComposition(e.TextComposition.CompositionText);

    private void AssetLibrarySearchBox_TextInputCompleted(object sender, TextCompositionEventArgs e) =>
        _viewModel.CompleteP3SearchComposition();

    private async void AssetLibrarySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SubmitP3SearchCommand.CanExecute(null))
        {
            _viewModel.SubmitP3SearchCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            await _viewModel.HandleP3SearchEscapeAsync();
            e.Handled = true;
        }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ClearFilters();

    private void OpenButtonContextMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleP3QueryPanelCommand.Execute(null);
    }

    private void TagFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenFilterPanel();
    }

    private void RatingFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenFilterPanel();
    }

    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenFilterPanel();
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.Items.Clear();
            menu.Items.Add(CreateMoreItem(_viewModel.OrganizationPaneToggleLabel, _viewModel.ToggleOrganizationPaneCommand));
            menu.Items.Add(CreateMoreItem(_viewModel.InspectorPaneToggleLabel, _viewModel.ToggleInspectorPaneCommand));
            menu.Items.Add(CreateMoreItem("打开灵感板", _viewModel.OpenCollectionsCommand));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMoreItem("新建智能文件夹", _viewModel.NewP3SmartFolderCommand));
            menu.Items.Add(CreateMoreItem("标签管理与批量编辑", _viewModel.ToggleP3TagManagerCommand));
            menu.Items.Add(CreateMoreItem("分析选中素材", _viewModel.AnalyzeSelectionCommand));
            menu.Items.Add(CreateMoreItem("撤销", _viewModel.P2UndoCommand));
            menu.Items.Add(CreateMoreItem("重做", _viewModel.P2RedoCommand));
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private static MenuItem CreateMoreItem(string header, ICommand command) => new() { Header = header, Command = command };

    private void ViewMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        if (TryFindResource("PixelTart.Menu.Context") is Style style) menu.Style = style;
        foreach (var pair in new[] { ("网格", "Grid"), ("瀑布流", "Masonry"), ("两端对齐", "Justified"), ("列表", "List") })
        {
            var item = new MenuItem { Header = pair.Item1, Command = _viewModel.SwitchViewCommand, CommandParameter = pair.Item2 };
            AutomationProperties.SetAutomationId(item, $"AssetView{pair.Item2}");
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void SortMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        if (TryFindResource("PixelTart.Menu.Context") is Style style) menu.Style = style;
        foreach (var pair in new[] { ("添加时间", "AddedAt"), ("拍摄时间", "CaptureTime"), ("文件名", "FileName"), ("文件大小", "FileSize"), ("评分", "Rating"), ("颜色", "Color"), ("视觉分析", "VisualAnalysis") })
            menu.Items.Add(new MenuItem { Header = pair.Item1, Command = _viewModel.SortBrowserCommand, CommandParameter = pair.Item2 });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = _viewModel.SortDirectionLabel, Command = _viewModel.ToggleSortDirectionCommand });
        menu.IsOpen = true;
    }

    private void AssetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingViewModelSelection || _disposed || _pendingSelectionSync is not null) return;
        _pendingSelectionSync = Dispatcher.BeginInvoke(
            DispatcherPriority.DataBind,
            new Action(FlushAssetGridSelection));
    }

    private void AssetGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_disposed || e.ChangedButton != MouseButton.Right) return;
        var container = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not AssetVisualMatchView card) return;

        // Keep an existing extended selection when the context target is already selected;
        // otherwise promote the target to a single selection before ContextMenu opens.
        var nextIds = AssetLibraryContextSelectionPolicy.ResolveSelection(_viewModel.SelectedAssetIds, card.Asset.AssetId);
        if (!nextIds.SetEquals(_viewModel.SelectedAssetIds))
        {
            _applyingViewModelSelection = true;
            try { AssetGrid.ReplaceSelection(new[] { card }); }
            finally { _applyingViewModelSelection = false; }
            _viewModel.SyncSelection([card.Asset]);
            UpdateGridDiagnostics();
        }
    }

    private void FlushAssetGridSelection()
    {
        _pendingSelectionSync = null;
        if (_disposed || _applyingViewModelSelection) return;
        var selectedVisibleAssets = AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().Select(card => card.Asset).ToArray();
        var visibleAssetIds = _viewModel.AssetCards.Select(card => card.Asset.AssetId).ToHashSet();
        var projectedSelectionIds = _viewModel.SelectedAssetIds.Where(id => !visibleAssetIds.Contains(id)).ToHashSet();
        projectedSelectionIds.UnionWith(selectedVisibleAssets.Select(asset => asset.AssetId));
        if (!projectedSelectionIds.SetEquals(_viewModel.SelectedAssetIds))
            _viewModel.SyncVisibleSelection(selectedVisibleAssets, visibleAssetIds);
        UpdateGridDiagnostics();
    }

    private void AssetGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_disposed || e.ChangedButton != MouseButton.Left || IsAssetCardSource(e.OriginalSource as DependencyObject)) return;
        if (FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) is not null) return;

        _isMarqueeSelecting = true;
        _marqueeStart = e.GetPosition(AssetGrid);
        _marqueeControlSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _marqueeShiftSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        _marqueeBaseSelection = _viewModel.SelectedAssetIds.ToHashSet();
        if (!_marqueeControlSelection && !_marqueeShiftSelection) _marqueeBaseSelection.Clear();
        AssetGrid.Focus();
        AssetGrid.CaptureMouse();
        UpdateMarqueeSelection(e.GetPosition(AssetGrid));
        e.Handled = true;
    }

    private void AssetGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMarqueeSelecting || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateMarqueeSelection(e.GetPosition(AssetGrid));
        e.Handled = true;
    }

    private void AssetGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMarqueeSelecting || e.ChangedButton != MouseButton.Left) return;
        var end = e.GetPosition(AssetGrid);
        UpdateMarqueeSelection(end);
        CompleteMarqueeSelection(end);
        e.Handled = true;
    }

    private void AssetGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isMarqueeSelecting) CancelMarqueeSelection();
    }

    private void UpdateMarqueeSelection(Point current)
    {
        var width = Math.Max(0d, AssetGrid.RenderSize.Width);
        var height = Math.Max(0d, AssetGrid.RenderSize.Height);
        var left = Math.Clamp(Math.Min(_marqueeStart.X, current.X), 0d, width);
        var top = Math.Clamp(Math.Min(_marqueeStart.Y, current.Y), 0d, height);
        var right = Math.Clamp(Math.Max(_marqueeStart.X, current.X), 0d, width);
        var bottom = Math.Clamp(Math.Max(_marqueeStart.Y, current.Y), 0d, height);
        Canvas.SetLeft(AssetSelectionMarquee, left);
        Canvas.SetTop(AssetSelectionMarquee, top);
        AssetSelectionMarquee.Width = Math.Max(1d, right - left);
        AssetSelectionMarquee.Height = Math.Max(1d, bottom - top);
        AssetSelectionMarquee.Visibility = Visibility.Visible;
    }

    private void CompleteMarqueeSelection(Point end)
    {
        if (!_isMarqueeSelecting) return;
        var selection = CreateMarqueeRect(_marqueeStart, end, AssetGrid.RenderSize);
        var hitCards = GetIntersectingCards(selection);
        var hitIds = hitCards.Select(card => card.Asset.AssetId).ToHashSet();
        var nextIds = _marqueeControlSelection
            ? ToggleSelection(_marqueeBaseSelection, hitIds)
            : _marqueeShiftSelection
                ? _marqueeBaseSelection.Concat(hitIds).ToHashSet()
                : hitIds;

        _isMarqueeSelecting = false;
        AssetGrid.ReleaseMouseCapture();
        HideMarqueeSelection();
        ApplyMarqueeSelection(nextIds);
        _marqueeBaseSelection.Clear();
    }

    private void CancelMarqueeSelection()
    {
        if (!_isMarqueeSelecting)
        {
            HideMarqueeSelection();
            return;
        }
        _isMarqueeSelecting = false;
        if (AssetGrid.IsMouseCaptured) AssetGrid.ReleaseMouseCapture();
        HideMarqueeSelection();
        _marqueeBaseSelection.Clear();
    }

    private void HideMarqueeSelection()
    {
        AssetSelectionMarquee.Visibility = Visibility.Collapsed;
        AssetSelectionMarquee.Width = 0d;
        AssetSelectionMarquee.Height = 0d;
    }

    private void ApplyMarqueeSelection(IReadOnlySet<Guid> ids)
    {
        var cards = _viewModel.AssetCards.Where(card => ids.Contains(card.Asset.AssetId)).ToArray();
        _applyingViewModelSelection = true;
        try
        {
            AssetGrid.ReplaceSelection(cards);
        }
        finally { _applyingViewModelSelection = false; }

        if (_marqueeControlSelection || _marqueeShiftSelection)
            _viewModel.SyncVisibleSelection(cards.Select(card => card.Asset), _viewModel.AssetCards.Select(card => card.Asset.AssetId));
        else
            _viewModel.SyncSelection(cards.Select(card => card.Asset));
        UpdateGridDiagnostics();
    }

    private IReadOnlyList<AssetVisualMatchView> GetIntersectingCards(Rect selection)
    {
        var hits = new List<AssetVisualMatchView>();
        for (var index = 0; index < AssetGrid.Items.Count; index++)
        {
            if (AssetGrid.Items[index] is not AssetVisualMatchView card ||
                AssetGrid.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container) continue;
            try
            {
                var local = new Rect(new Point(), container.RenderSize);
                var bounds = container.TransformToAncestor(AssetGrid).TransformBounds(local);
                if (bounds.IntersectsWith(selection)) hits.Add(card);
            }
            catch (InvalidOperationException) { }
        }
        return hits;
    }

    private static HashSet<Guid> ToggleSelection(IEnumerable<Guid> baseSelection, IEnumerable<Guid> hitIds)
    {
        var result = baseSelection.ToHashSet();
        foreach (var id in hitIds)
            if (!result.Add(id)) result.Remove(id);
        return result;
    }

    private static Rect CreateMarqueeRect(Point start, Point end, Size bounds)
    {
        var width = Math.Max(0d, bounds.Width);
        var height = Math.Max(0d, bounds.Height);
        var left = Math.Clamp(Math.Min(start.X, end.X), 0d, width);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), 0d, height);
        var right = Math.Clamp(Math.Max(start.X, end.X), 0d, width);
        var bottom = Math.Clamp(Math.Max(start.Y, end.Y), 0d, height);
        return new(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
    }

    private static bool IsAssetCardSource(DependencyObject? source) => FindVisualParent<ListBoxItem>(source) is not null;

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetInputParent(current))
            if (current is T match) return match;
        return null;
    }

    private void ViewModel_SelectionRestoreRequested(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(ApplyViewModelSelection));
            return;
        }
        ApplyViewModelSelection();
    }

    private void ApplyViewModelSelection()
    {
        if (_disposed) return;
        if (_pendingSelectionSync is { } pendingSelectionSync)
        {
            _pendingSelectionSync = null;
            if (pendingSelectionSync.Status == DispatcherOperationStatus.Pending) pendingSelectionSync.Abort();
        }
        var selectedIds = _viewModel.SelectedAssetIds.ToHashSet();
        var desiredCards = _viewModel.AssetCards.Where(card => selectedIds.Contains(card.Asset.AssetId)).ToArray();
        if (AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().ToHashSet().SetEquals(desiredCards)) return;
        _applyingViewModelSelection = true;
        try
        {
            AssetGrid.ReplaceSelection(desiredCards);
        }
        finally
        {
            _applyingViewModelSelection = false;
        }
        UpdateGridDiagnostics();
    }

    private void ViewModel_ViewModeChanging(object? sender, AssetLibraryViewModeChangedEventArgs e)
    {
        var panel = FindVisualChild<VirtualizingAssetPanel>(AssetGrid);
        var firstVisibleIndex = panel?.FirstVisibleIndex ?? -1;
        _viewTransitionAnchor = firstVisibleIndex >= 0 && firstVisibleIndex < _viewModel.AssetCards.Count
            ? _viewModel.AssetCards[firstVisibleIndex].Asset.AssetId
            : _viewModel.SelectedAssets.FirstOrDefault()?.AssetId;
        _viewModel.RememberScrollAnchor(_viewTransitionAnchor);
    }

    private void ViewModel_ViewModeChanged(object? sender, AssetLibraryViewModeChangedEventArgs e)
    {
        var target = _viewModel.GetScrollAnchor(e.Current) ?? _viewTransitionAnchor;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_disposed || target is null) return;
            var card = _viewModel.AssetCards.FirstOrDefault(item => item.Asset.AssetId == target.Value);
            if (card is not null) AssetGrid.ScrollIntoView(card);
        }));
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private void UpdateGridDiagnostics() => _viewModel.UpdateAssetGridDiagnostics(
        AssetGrid.Items.Count,
        ReferenceEquals(AssetGrid.ItemsSource, _viewModel.AssetCards) ? "AssetCards" : AssetGrid.ItemsSource?.GetType().Name ?? "None",
        ReferenceEquals(AssetGrid.ItemsSource, _viewModel.AssetCards),
        DataContext?.GetType().Name ?? "None");

    public void FocusSearch()
    {
        AssetLibrarySearchBox.Focus();
        AssetLibrarySearchBox.SelectAll();
    }

    public void FocusInitial() => FocusSearch();

    public ContextMenu? OpenContextMenuForProductHarness()
    {
        if (AssetGrid.Items.Count == 0) return null;
        AssetGrid.ScrollIntoView(AssetGrid.Items[0]);
        AssetGrid.UpdateLayout();
        if (AssetGrid.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem item || item.ContextMenu is null) return null;
        item.ContextMenu.PlacementTarget = item;
        item.ContextMenu.Placement = PlacementMode.MousePoint;
        item.ContextMenu.IsOpen = true;
        return item.ContextMenu;
    }

    private void AssetContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var style = TryFindResource("PixelTart.Menu.Item") as Style;
        if (style is null) return;
        var template = TryFindResource("PixelTart.Menu.Item.Template") as ControlTemplate;
        var sectionStyle = TryFindResource("AssetContextSectionHeader") as Style;
        void Apply(ItemsControl parent)
        {
            foreach (var child in parent.Items.OfType<MenuItem>())
            {
                if (!ReferenceEquals(child.Style, sectionStyle))
                {
                    child.Style = style;
                    if (template is not null) child.Template = template;
                }
                Apply(child);
            }
        }
        Apply(menu);
    }

    public ContextMenu? OpenContextSubmenuForProductHarness(string header)
    {
        var menu = OpenContextMenuForProductHarness();
        menu?.UpdateLayout();
        if (menu?.Items.OfType<MenuItem>().FirstOrDefault(item => string.Equals(item.Header?.ToString(), header, StringComparison.Ordinal)) is { } submenu)
            submenu.IsSubmenuOpen = true;
        return menu;
    }

    public async Task<bool> OpenQuickLoupeForProductHarnessAsync()
    {
        if (AssetGrid.Items.Count == 0) return false;
        AssetGrid.ScrollIntoView(AssetGrid.Items[0]);
        AssetGrid.UpdateLayout();
        if (AssetGrid.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem item || item.DataContext is not AssetVisualMatchView card) return false;
        ConfigureCenteredQuickLoupe();
        _quickLoupeCard = card;
        await ShowQuickLoupeAsync(card);
        return true;
    }

    public FrameworkElement? GetQuickLoupeContentForProductHarness() =>
        AssetQuickLoupePopup.IsOpen ? AssetQuickLoupePopup.Child as FrameworkElement : null;

    public bool FocusQuickLoupeCardForProductHarness()
    {
        if (AssetGrid.Items.Count == 0) return false;
        AssetGrid.ScrollIntoView(AssetGrid.Items[0]);
        AssetGrid.UpdateLayout();
        return AssetGrid.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item && item.Focus();
    }

    public bool LeaveQuickLoupeForProductHarness()
    {
        QuickLoupeContainer.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
        return !AssetQuickLoupePopup.IsOpen;
    }

    public FrameworkElement? GetContextSubmenuContentForProductHarness(ContextMenu menu, string header)
    {
        var item = menu.Items.OfType<MenuItem>().FirstOrDefault(candidate => candidate.Header?.ToString() == header);
        item?.ApplyTemplate();
        return item?.Template.FindName("PART_Popup", item) is Popup { IsOpen: true, Child: FrameworkElement child } ? child : null;
    }

    public AssetViewerWindow? CreateViewerForProductHarness()
    {
        var paths = _viewModel.AssetCards.Select(card => _viewModel.GetDisplaySourcePath(card.Asset)).Where(File.Exists).ToArray();
        return paths.Length == 0 ? null : new AssetViewerWindow(paths, 0, _previewProvider);
    }

    private async void QuickLoupeButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_disposed || sender is not FrameworkElement { DataContext: AssetVisualMatchView card }) return;
        ConfigureCenteredQuickLoupe();
        _quickLoupeCard = card;
        await ShowQuickLoupeAsync(card);
    }

    private async void QuickLoupeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_disposed || sender is not FrameworkElement { DataContext: AssetVisualMatchView card }) return;
        ConfigureCenteredQuickLoupe();
        _quickLoupeCard = card;
        await ShowQuickLoupeAsync(card);
        e.Handled = true;
    }

    private async void ContextQuickPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_disposed || sender is not FrameworkElement { DataContext: AssetVisualMatchView card }) return;
        ConfigureCenteredQuickLoupe();
        _quickLoupeCard = card;
        await ShowQuickLoupeAsync(card);
        e.Handled = true;
    }

    private void ConfigureCenteredQuickLoupe()
    {
        AssetQuickLoupePopup.PlacementTarget = this;
        AssetQuickLoupePopup.Placement = PlacementMode.Center;
        AssetQuickLoupePopup.HorizontalOffset = 0;
        AssetQuickLoupePopup.VerticalOffset = 0;
        QuickLoupeContainer.Width = Math.Clamp(ActualWidth * 0.5d, 420d, 860d);
        QuickLoupeContainer.Height = Math.Clamp(ActualHeight * 0.55d, 300d, 680d);
    }

    private void QuickLoupeButton_MouseLeave(object sender, MouseEventArgs e) =>
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (!AssetQuickLoupePopup.IsMouseOver) HideQuickLoupe();
        }));

    private void QuickLoupePopup_MouseLeave(object sender, MouseEventArgs e) => HideQuickLoupe();

    private async Task ShowQuickLoupeAsync(AssetVisualMatchView card)
    {
        _quickLoupeCancellation?.Cancel();
        _quickLoupeCancellation?.Dispose();
        _quickLoupeCancellation = new CancellationTokenSource();
        var token = _quickLoupeCancellation.Token;
        QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · 正在载入高清预览…";
        QuickLoupeImage.Source = null;
        AssetQuickLoupePopup.IsOpen = true;
        try
        {
            var result = await _previewProvider.GetAsync(new(
                card.ThumbnailPath,
                AssetPreviewPurpose.QuickLoupe,
                AssetPreviewQuality.High,
                1600,
                card.Asset.IsMissing ? AssetThumbnailState.Missing : AssetThumbnailState.Available,
                card.Asset.AssetId,
                card.Asset.ContentHash), token);
            if (token.IsCancellationRequested || !ReferenceEquals(_quickLoupeCard, card)) return;
            if (!result.IsAvailable || result.Bitmap is null) { QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · 高清预览不可用"; return; }
            var bitmap = result.Bitmap;
            QuickLoupeImage.Source = bitmap;
            QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · {bitmap.PixelWidth} × {bitmap.PixelHeight}";
        }
        catch (OperationCanceledException) { }
        catch (IOException) { QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · 高清预览不可用"; }
        catch (NotSupportedException) { QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · 暂不支持此格式"; }
        catch (ArgumentException) { QuickLoupeTitle.Text = $"{card.Asset.DisplayName} · 高清预览不可用"; }
    }

    private void AssetGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        // A centered popup can receive the pointer directly from the gallery.
        // Let that transition finish before deciding whether the preview was left.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!AssetQuickLoupePopup.IsMouseOver) HideQuickLoupe();
        });
    }

    private void HideQuickLoupe()
    {
        _quickLoupeCancellation?.Cancel();
        _quickLoupeCancellation?.Dispose();
        _quickLoupeCancellation = null;
        _quickLoupeCard = null;
        if (AssetQuickLoupePopup is not null) AssetQuickLoupePopup.IsOpen = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => _viewModel.UpdateViewportWidth(e.NewSize.Width);

    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e) => SchedulePaneWidthCommit();

    private void OnPaneSplitterPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right) SchedulePaneWidthCommit();
    }

    private void SchedulePaneWidthCommit()
    {
        _pendingPaneWidthCommit?.Abort();
        _pendingPaneWidthCommit = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                _pendingPaneWidthCommit = null;
                if (!_disposed) PersistAndRebindPaneWidths();
            }));
    }

    private void PersistAndRebindPaneWidths()
    {
        // Preview splitters commit after their routed completion event. This deferred callback
        // runs after that commit; force layout before reading the completed mouse/keyboard width.
        UpdateLayout();
        var organizationPaneWidth = AssetOrganizationColumn.ActualWidth;
        var inspectorPaneWidth = AssetInspectorColumn.ActualWidth;
        _viewModel.UpdatePaneWidths(organizationPaneWidth, inspectorPaneWidth);

        // GridSplitter writes local Width values and would otherwise replace the responsive bindings.
        AssetCollectionColumn.Width = new GridLength(1d, GridUnitType.Star);
        BindingOperations.SetBinding(
            AssetOrganizationColumn,
            ColumnDefinition.WidthProperty,
            new Binding(nameof(AssetLibraryViewModel.OrganizationPaneColumnWidth)) { Source = _viewModel, Mode = BindingMode.OneWay });
        BindingOperations.SetBinding(
            AssetInspectorColumn,
            ColumnDefinition.WidthProperty,
            new Binding(nameof(AssetLibraryViewModel.InspectorPaneColumnWidth)) { Source = _viewModel, Mode = BindingMode.OneWay });
    }

    private async void FindSimilarPaletteMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AssetVisualMatchView card)
            await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Palette);
    }

    private async void FindSimilarVisualMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AssetVisualMatchView card)
            await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Similarity);
    }

    private async void AnalyzeVisualMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AssetVisualMatchView card)
            await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Analyze);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.ImeProcessed or Key.DeadCharProcessed || IsTextInputContext(e.OriginalSource)) return;
        if (_isMarqueeSelecting && e.Key == Key.Escape)
        {
            CancelMarqueeSelection();
            e.Handled = true;
            return;
        }
        if (AssetGrid.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SelectAllVisibleAssets();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.None &&
                (e.Key is Key.Home or Key.End or Key.PageUp or Key.PageDown))
            {
                NavigateAssetGrid(e.Key);
                e.Handled = true;
                return;
            }
        }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FocusSearch();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            _viewModel.FocusFolderClassifier();
            e.Handled = true;
        }
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
            await _viewModel.RepeatLastFolderMembershipAsync();
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            _viewModel.SetStatusMessage("输入标签后点击“应用标签”。");
        }
        else if (e.Key is >= Key.D0 and <= Key.D5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await _viewModel.RateSelectedAsync(e.Key - Key.D0);
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.SyncSelection([]);
            AssetGrid.UnselectAll();
            e.Handled = true;
        }
    }

    private void SelectAllVisibleAssets()
    {
        var cards = _viewModel.AssetCards.ToArray();
        _applyingViewModelSelection = true;
        try { AssetGrid.SelectAll(); }
        finally { _applyingViewModelSelection = false; }
        _viewModel.SyncSelection(cards.Select(card => card.Asset));
        UpdateGridDiagnostics();
    }

    private void AssetGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not AssetVisualMatchView card) return;
        var paths = _viewModel.AssetCards.Select(item => _viewModel.GetDisplaySourcePath(item.Asset)).Where(path => path.Length != 0).ToArray();
        var index = Array.IndexOf(paths, _viewModel.GetDisplaySourcePath(card.Asset));
        new AssetViewerWindow(paths, Math.Max(0, index), _previewProvider).Show();
        e.Handled = true;
    }

    private void AssetGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else e.Effects = DragDropEffects.None;
    }

    private async void AssetGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await _viewModel.ImportDroppedFilesAsync(paths);
    }

    private void NavigateAssetGrid(Key key)
    {
        if (AssetGrid.Items.Count == 0) return;
        var current = AssetGrid.SelectedIndex < 0 ? 0 : AssetGrid.SelectedIndex;
        var panel = FindVisualChild<VirtualizingAssetPanel>(AssetGrid);
        var target = key switch
        {
            Key.Home => 0,
            Key.End => AssetGrid.Items.Count - 1,
            Key.PageUp => panel?.GetPageTargetIndex(current, forward: false) ?? Math.Max(0, current - 10),
            Key.PageDown => panel?.GetPageTargetIndex(current, forward: true) ?? Math.Min(AssetGrid.Items.Count - 1, current + 10),
            _ => current
        };
        if (panel is not null)
        {
            if (key == Key.PageDown && target <= current && current < AssetGrid.Items.Count - 1) target = current + 1;
            if (key == Key.PageUp && target >= current && current > 0) target = current - 1;
        }
        AssetGrid.SelectedIndex = target;
        AssetGrid.ScrollIntoView(AssetGrid.Items[target]);
    }

    private static bool IsTextInputContext(object? source)
    {
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox) return true;
        for (var current = source as DependencyObject; current is not null; current = GetInputParent(current))
            if (current is TextBoxBase or PasswordBox or ComboBox) return true;
        return false;
    }

    private static DependencyObject? GetInputParent(DependencyObject current) =>
        current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
