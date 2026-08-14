using System.IO;
using System.Windows.Input;
using System.Windows;

namespace PixelTart.AssetLibrary.Preview;

public partial class MainWindow : Window
{
    private AssetLibraryPreviewViewModel? _viewModel;
    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

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

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            FolderList.Focus();
            Keyboard.Focus(FolderList);
            _viewModel?.FocusFolderClassifier();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Shift && _viewModel is not null)
        {
            e.Handled = true;
            await _viewModel.RepeatLastFolderMembershipAsync();
        }
    }
}
