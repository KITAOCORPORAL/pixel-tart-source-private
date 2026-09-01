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
    private DispatcherOperation? _pendingPaneWidthCommit;
    private Guid? _viewTransitionAnchor;

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
        _pendingPaneWidthCommit?.Abort();
        _pendingPaneWidthCommit = null;
        _viewModel.SelectionRestoreRequested -= ViewModel_SelectionRestoreRequested;
        _viewModel.ViewModeChanging -= ViewModel_ViewModeChanging;
        _viewModel.ViewModeChanged -= ViewModel_ViewModeChanged;
        await _viewModel.DisposeAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ClearFilters();

    private void AssetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingViewModelSelection) return;
        _viewModel.SyncVisibleSelection(
            AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().Select(card => card.Asset),
            _viewModel.AssetCards.Select(card => card.Asset.AssetId));
        UpdateGridDiagnostics();
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
