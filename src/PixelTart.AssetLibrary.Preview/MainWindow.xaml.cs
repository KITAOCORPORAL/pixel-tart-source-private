using System.IO;
using System.Windows;

namespace PixelTart.AssetLibrary.Preview;

public partial class MainWindow : Window
{
    private AssetLibraryPreviewViewModel? _viewModel;
    public MainWindow() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KitaoPhotoSelector.AssetLibraryPreview");
        _viewModel = new AssetLibraryPreviewViewModel(Path.Combine(root, "asset-library-preview.db")); DataContext = _viewModel; await _viewModel.InitializeAsync();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null) await _viewModel.DisposeAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel?.ClearFilters();
}
