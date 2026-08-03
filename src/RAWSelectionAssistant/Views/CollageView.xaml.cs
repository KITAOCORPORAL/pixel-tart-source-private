using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class CollageView : UserControl
{
    private readonly DispatcherTimer _previewTimer;
    private readonly CollageExportService _renderer = new();
    private CollageViewModel? _viewModel;

    public CollageView()
    {
        InitializeComponent();
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RenderPreview(); };
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RenderPreview();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PreviewChanged -= PreviewChanged;
        _viewModel = e.NewValue as CollageViewModel;
        if (_viewModel is not null) _viewModel.PreviewChanged += PreviewChanged;
        RenderPreview();
    }

    private void PreviewChanged(object? sender, EventArgs e) { _previewTimer.Stop(); _previewTimer.Start(); }
    private void RenderPreview()
    {
        if (_viewModel is null || _viewModel.Project.Images.Count == 0) { PreviewImage.Source = null; EmptyCanvasText.Visibility = Visibility.Visible; return; }
        try { PreviewImage.Source = _renderer.Render(_viewModel.Project); EmptyCanvasText.Visibility = Visibility.Collapsed; } catch { PreviewImage.Source = null; EmptyCanvasText.Visibility = Visibility.Visible; }
    }
    private void OnDragOver(object sender, DragEventArgs e) { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true; }
    private void OnDrop(object sender, DragEventArgs e) { if(_viewModel is not null&&e.Data.GetData(DataFormats.FileDrop) is string[] paths)_viewModel.AddPaths(paths); }
    private void Fit_Click(object sender, RoutedEventArgs e) { CanvasScroller.ScrollToHorizontalOffset(0); CanvasScroller.ScrollToVerticalOffset(0); RenderPreview(); }
    private void PreviewImage_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) { _viewModel?.AdjustSelectedZoom(e.Delta > 0 ? .1 : -.1); e.Handled=true; }
}
