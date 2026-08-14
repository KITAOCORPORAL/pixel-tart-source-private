using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.AssetLibrary.Preview;

public partial class MainWindow : Window
{
    private AssetLibraryPreviewViewModel? _viewModel;
    private ClassifierWindow? _classifier;
    private Window? _preview;

    public MainWindow() { InitializeComponent(); PreviewKeyDown += OnPreviewKeyDown; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var requestedRoot = Environment.GetEnvironmentVariable("PIXEL_TART_ASSET_LIBRARY_ACCEPTANCE_ROOT");
        var root = string.IsNullOrWhiteSpace(requestedRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KitaoPhotoSelector.AssetLibraryV16Preview")
            : RequireAbsoluteAcceptanceRoot(requestedRoot);
        _viewModel = new(Path.Combine(root, "asset-library-v16-preview.db")); DataContext = _viewModel; await _viewModel.InitializeAsync();
        var demo = Environment.GetEnvironmentVariable("PIXEL_TART_ASSET_LIBRARY_DEMO_DIR");
        if (!string.IsNullOrWhiteSpace(demo) && Directory.Exists(demo)) await _viewModel.ImportDemoDirectoryAsync(demo);
    }

    private static string RequireAbsoluteAcceptanceRoot(string value)
    {
        if (!Path.IsPathFullyQualified(value)) throw new InvalidOperationException("PIXEL_TART_ASSET_LIBRARY_ACCEPTANCE_ROOT 必须是显式绝对目录。");
        var root = Path.GetFullPath(value);
        Directory.CreateDirectory(root);
        return root;
    }

    private async void OnClosed(object? sender, EventArgs e) { _classifier?.Close(); _preview?.Close(); if (_viewModel is not null) await _viewModel.DisposeAsync(); }
    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel?.ClearFilters();
    private void AssetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_viewModel is null) return; _viewModel.SyncSelection(AssetGrid.SelectedItems.Cast<AssetVisualMatchView>().Select(card => card.Asset)); }
    private void OpenClassifier_Click(object sender, RoutedEventArgs e) => OpenClassifier();
    private void OpenTagManager_Click(object sender, RoutedEventArgs e) => new TagManagerWindow { Owner = this, DataContext = _viewModel }.ShowDialog();

    private async void FindSimilarPaletteMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || (sender as FrameworkElement)?.DataContext is not AssetVisualMatchView card) return;
        await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Palette);
    }

    private async void FindSimilarVisualMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || (sender as FrameworkElement)?.DataContext is not AssetVisualMatchView card) return;
        await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Similarity);
    }

    private async void AnalyzeVisualMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || (sender as FrameworkElement)?.DataContext is not AssetVisualMatchView card) return;
        await _viewModel.ExecuteVisualContextActionAsync(card.Asset, VisualContextAction.Analyze);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None) { OpenClassifier(); e.Handled = true; }
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Shift) { e.Handled = true; await _viewModel.RepeatLastFolderMembershipAsync(); }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.None) { TagInputBox.Focus(); Keyboard.Focus(TagInputBox); e.Handled = true; }
        else if (e.Key == Key.Space) { TogglePreview(); e.Handled = true; }
        else if (e.Key is >= Key.D0 and <= Key.D5 && Keyboard.Modifiers == ModifierKeys.None) { await _viewModel.RateSelectedAsync(e.Key - Key.D0); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (_preview is not null) TogglePreview(); else AssetGrid.UnselectAll(); e.Handled = true; }
    }

    private void OpenClassifier()
    {
        if (_viewModel is null || _viewModel.SelectedAssets.Count == 0) return;
        _viewModel.FocusFolderClassifier();
        _classifier?.Close(); _classifier = new ClassifierWindow(_viewModel) { Owner = this }; _classifier.Closed += (_, _) => _classifier = null; _classifier.Show(); _classifier.Activate();
    }

    private void TogglePreview()
    {
        if (_preview is not null) { _preview.Close(); _preview = null; return; }
        if (_viewModel?.SelectedAsset is null) return;
        var image = new Image { Stretch = System.Windows.Media.Stretch.Uniform };
        AsyncThumbnail.SetSourcePath(image, _viewModel.SelectedAsset.SourcePath); AsyncThumbnail.SetDecodeWidth(image, 1200);
        _preview = new Window { Owner = this, Title = $"预览 · {_viewModel.SelectedAsset.DisplayName}", Width = 1100, Height = 800, Background = System.Windows.Media.Brushes.Black, Content = image, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        _preview.Closed += (_, _) => _preview = null; _preview.PreviewKeyDown += (_, args) => { if (args.Key is Key.Space or Key.Escape) { _preview?.Close(); args.Handled = true; } }; _preview.Show();
    }
}
