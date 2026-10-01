using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

/// <summary>Read-only reference navigation reuses the target viewport's pixel mapping.</summary>
public partial class ReferenceNavigator : UserControl
{
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(ReferenceNavigator), new PropertyMetadata(string.Empty, SourcePathChanged));
    public string SourcePath { get => (string)GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public ColorStudioZoomPanState State => Preview.State;
    public BitmapSource? Image => Preview.Original;
    private Point? _panStart;
    private long _loadRevision;
    public Task LoadingTask { get; private set; } = Task.CompletedTask;
    public ReferenceNavigator()
    {
        InitializeComponent(); State.Changed += (_, _) => ZoomLabel.Text = $"{State.Zoom:P0}";
        Stage.LostMouseCapture += (_, _) => _panStart = null;
    }
    private static void SourcePathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ReferenceNavigator)sender).LoadingTask = ((ReferenceNavigator)sender).LoadSourceAsync();
    private async Task LoadSourceAsync()
    {
        var revision = ++_loadRevision; var path = SourcePath;
        _panStart = null; Stage.ReleaseMouseCapture(); Preview.Original = null; EmptyText.Visibility = Visibility.Visible;
        EmptyText.Text = string.IsNullOrWhiteSpace(path) ? "暂无参考图" : "正在读取参考图…";
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var bitmap = await Task.Run(() =>
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(Path.GetFullPath(path)); image.EndInit(); image.Freeze(); return image;
            });
            if (revision != _loadRevision) return;
            Preview.Original = bitmap; State.Fit(); EmptyText.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) { if (revision == _loadRevision) EmptyText.Text = "参考文件暂不可预览"; }
    }
    private void FitClick(object sender, RoutedEventArgs e) => State.Fit();
    private void ActualClick(object sender, RoutedEventArgs e) => State.SetZoom(1);
    private void ZoomInClick(object sender, RoutedEventArgs e) => State.SetZoom(State.Zoom * 1.25);
    private void ZoomOutClick(object sender, RoutedEventArgs e) => State.SetZoom(State.Zoom / 1.25);
    private void OnMouseDown(object sender, MouseButtonEventArgs e) { if (Image is null) return; _panStart = e.GetPosition(Stage); Stage.CaptureMouse(); e.Handled = true; }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (_panStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return; var current = e.GetPosition(Stage); State.PanBy(current - start); _panStart = current; e.Handled = true; }
    private void OnMouseUp(object sender, MouseButtonEventArgs e) { _panStart = null; Stage.ReleaseMouseCapture(); }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) { State.ZoomAbout(e.GetPosition(Stage), e.Delta > 0 ? 1.12 : 1 / 1.12); e.Handled = true; }
}
