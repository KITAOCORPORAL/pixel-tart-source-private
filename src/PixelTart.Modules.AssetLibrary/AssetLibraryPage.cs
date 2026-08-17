using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage : UserControl
{
    private readonly AssetLibraryViewModel _viewModel;
    private readonly bool _enablePreviewFeatures;
    private readonly string? _demoDirectory;
    private bool _initialized;
    private bool _disposed;

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
        string? demoDirectory = null)
    {
        InitializeComponent();
        _enablePreviewFeatures = enablePreviewFeatures;
        _demoDirectory = enablePreviewFeatures ? demoDirectory : null;
        _viewModel = new AssetLibraryViewModel(databasePath, taskOperationBridge, moduleDiagnostics, enablePreviewFeatures);
        DataContext = _viewModel;
    }

    public AssetLibraryViewModel ViewModel => _viewModel;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _disposed) return;
        _initialized = true;
        try
        {
            await _viewModel.InitializeAsync();
            if (_enablePreviewFeatures && !string.IsNullOrWhiteSpace(_demoDirectory) && Directory.Exists(_demoDirectory))
                await _viewModel.ImportDemoDirectoryAsync(_demoDirectory);
            UpdateGridDiagnostics();
        }
        catch (Exception exception)
        {
            _viewModel.SetForegroundError($"素材库初始化失败：{exception.Message}");
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed || Application.Current?.MainWindow?.IsLoaded == true) return;
        _disposed = true;
        await _viewModel.DisposeAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ClearFilters();

    private void AssetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SyncSelection(AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().Select(card => card.Asset));
        UpdateGridDiagnostics();
    }

    private void UpdateGridDiagnostics() => _viewModel.UpdateAssetGridDiagnostics(
        AssetGrid.Items.Count,
        ReferenceEquals(AssetGrid.ItemsSource, _viewModel.AssetCards) ? "AssetCards" : AssetGrid.ItemsSource?.GetType().Name ?? "None",
        ReferenceEquals(AssetGrid.ItemsSource, _viewModel.AssetCards),
        DataContext?.GetType().Name ?? "None");

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
            _viewModel.SetForegroundError("输入标签后点击“应用标签”。");
        }
        else if (e.Key is >= Key.D0 and <= Key.D5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await _viewModel.RateSelectedAsync(e.Key - Key.D0);
        }
        else if (e.Key == Key.Escape)
        {
            AssetGrid.UnselectAll();
            e.Handled = true;
        }
    }
}
