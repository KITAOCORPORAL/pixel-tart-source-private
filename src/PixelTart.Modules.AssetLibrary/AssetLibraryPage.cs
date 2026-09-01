using System.IO;
using System.Windows;
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
        IAssetLibraryLoadStateController? loadStateController = null)
    {
        InitializeComponent();
        _enablePreviewFeatures = enablePreviewFeatures && loadStateController?.DisablePreviewFixtures != true;
        _demoDirectory = _enablePreviewFeatures ? demoDirectory : null;
        _viewModel = new AssetLibraryViewModel(databasePath, taskOperationBridge, moduleDiagnostics, _enablePreviewFeatures, workspaceSettings, logService, loadStateController);
        _viewModel.SelectionRestoreRequested += ViewModel_SelectionRestoreRequested;
        _viewModel.ViewModeChanging += ViewModel_ViewModeChanging;
        _viewModel.ViewModeChanged += ViewModel_ViewModeChanged;
        AssetGrid.PreviewMouseLeftButtonDown += AssetGrid_PreviewMouseLeftButtonDown;
        AssetGrid.PreviewMouseMove += AssetGrid_PreviewMouseMove;
        AssetGrid.PreviewMouseLeftButtonUp += AssetGrid_PreviewMouseLeftButtonUp;
        AssetGrid.LostMouseCapture += AssetGrid_LostMouseCapture;
        DataContext = _viewModel;
    }

    public AssetLibraryViewModel ViewModel => _viewModel;

    private async void OnLoaded(object sender, RoutedEventArgs e)
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
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
        AssetGrid.PreviewMouseLeftButtonUp -= AssetGrid_PreviewMouseLeftButtonUp;
        AssetGrid.LostMouseCapture -= AssetGrid_LostMouseCapture;
        CancelMarqueeSelection();
        await _viewModel.DisposeAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ClearFilters();

    private void AssetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingViewModelSelection || _disposed || _pendingSelectionSync is not null) return;
        _pendingSelectionSync = Dispatcher.BeginInvoke(
            DispatcherPriority.DataBind,
            new Action(FlushAssetGridSelection));
    }

    private void FlushAssetGridSelection()
    {
        _pendingSelectionSync = null;
        if (_disposed || _applyingViewModelSelection) return;
        _viewModel.SyncVisibleSelection(
            AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().Select(card => card.Asset),
            _viewModel.AssetCards.Select(card => card.Asset.AssetId));
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
            AssetGrid.SelectedItems.Clear();
            foreach (var card in cards) AssetGrid.SelectedItems.Add(card);
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
        var selectedIds = _viewModel.SelectedAssetIds.ToHashSet();
        _applyingViewModelSelection = true;
        try
        {
            AssetGrid.SelectedItems.Clear();
            foreach (var card in _viewModel.AssetCards.Where(card => selectedIds.Contains(card.Asset.AssetId)))
                AssetGrid.SelectedItems.Add(card);
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
