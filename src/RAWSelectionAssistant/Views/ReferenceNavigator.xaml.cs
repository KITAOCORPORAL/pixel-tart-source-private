using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>Read-only reference navigation reuses the target viewport's pixel mapping.</summary>
public partial class ReferenceNavigator : UserControl
{
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(ReferenceNavigator), new PropertyMetadata(string.Empty, SourcePathChanged));
    public string SourcePath { get => (string)GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public ColorStudioZoomPanState State => Preview.State;
    public BitmapSource? Image => Preview.Original;
    public static readonly DependencyProperty IsThumbnailProperty = DependencyProperty.Register(nameof(IsThumbnail), typeof(bool), typeof(ReferenceNavigator), new PropertyMetadata(false, ThumbnailChanged));
    public bool IsThumbnail { get => (bool)GetValue(IsThumbnailProperty); set => SetValue(IsThumbnailProperty, value); }
    private static void ThumbnailChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (ReferenceNavigator)sender;
        view.NavigationTools.Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        view.Stage.IsHitTestVisible = !(bool)e.NewValue;
    }
    private Point? _panStart;
    private long _loadRevision;
    private ColorStudioZoomPanState? _linkedState;
    private bool _following;
    public ColorStudioZoomPanState? LinkedState
    {
        get => _linkedState;
        set
        {
            if (_linkedState is not null) _linkedState.Changed -= LinkedChanged;
            _linkedState = value;
            if (_linkedState is not null) _linkedState.Changed += LinkedChanged;
            LinkedChanged(this, EventArgs.Empty);
        }
    }
    private void LinkedChanged(object? sender, EventArgs args)
    {
        if (_following || _linkedState is null || Image is null) return;
        _following = true;
        try { State.Follow(_linkedState); } finally { _following = false; }
    }
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
        StudioToolPanel.Text(EmptyText, TextBlock.TextProperty, string.IsNullOrWhiteSpace(path) ? "ReferenceEmpty" : "ReferenceLoading");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            // Reuse the product's ICC/orientation-aware, thread-detached loader.
            // BitmapImage alone showed the wrong orientation and retained a
            // thread-owned decoder. A preview is read-only; never rewrite it.
            var thumbnail = IsThumbnail;
            var bitmap = await Task.Run(() => StudioQuickExport.Load(path, thumbnail ? 384 : null));
            if (revision != _loadRevision) return;
            Preview.Original = bitmap; State.Fit(); LinkedChanged(this, EventArgs.Empty); EmptyText.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) { if (revision == _loadRevision) StudioToolPanel.Text(EmptyText, TextBlock.TextProperty, "ReferenceUnavailable"); }
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
