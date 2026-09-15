using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// Read-only, staged full-resolution viewer. The bounded thumbnail channel provides the
/// first frame; a separate source decoder then replaces it with original pixels.
/// </summary>
public sealed class AssetViewerWindow : Window
{
    private readonly IAssetThumbnailProvider _thumbnails;
    private readonly IReadOnlyList<string> _paths;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _loading = new()
    {
        Text = "正在载入原图…",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(12, 7, 12, 7),
        Visibility = Visibility.Collapsed
    };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly ScrollViewer _scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.Both };
    private readonly Dictionary<string, BitmapSource> _fullResolutionCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _loadCancellation;
    private Point? _panStart;
    private double _panHorizontal;
    private double _panVertical;
    private int _loadGeneration;
    private int _index;

    public AssetViewerWindow(IReadOnlyList<string> paths, int startIndex, IAssetThumbnailProvider? thumbnails = null)
    {
        _paths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _paths.Count - 1));
        _thumbnails = thumbnails ?? AsyncThumbnail.Provider;
        Title = "像素蛋挞 · 查看大图";
        Background = new SolidColorBrush(Color.FromRgb(8, 10, 12));
        WindowState = WindowState.Maximized;
        WindowStyle = WindowStyle.None;
        AutomationProperties.SetAutomationId(this, "AssetFullPreviewWindow");
        KeyDown += OnKeyDown;
        MouseWheel += OnMouseWheel;
        Closed += (_, _) => CancelCurrentLoad();
        _image.LayoutTransform = _scale;
        _image.Cursor = Cursors.Hand;
        AutomationProperties.SetAutomationId(_image, "AssetFullPreviewImage");
        AutomationProperties.SetName(_image, "原图预览");
        _image.MouseLeftButtonDown += BeginPan;
        _image.MouseMove += ContinuePan;
        _image.MouseLeftButtonUp += EndPan;
        _loading.SetResourceReference(ForegroundProperty, "Brush.Text.Primary");
        _loading.SetResourceReference(BackgroundProperty, "Brush.Surface.Elevated");
        AutomationProperties.SetAutomationId(_loading, "AssetFullPreviewLoading");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _scroller.Content = _image;
        var stage = new Grid();
        stage.Children.Add(_scroller);
        stage.Children.Add(_loading);
        grid.Children.Add(stage);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
        foreach (var item in new[] { ("上一张", (Action)Previous), ("适应窗口", Fit), ("100%", Actual), ("−", ZoomOut), ("+", ZoomIn), ("下一张", Next), ("返回", Close) })
        {
            var button = new Button { Content = item.Item1, Margin = new Thickness(4), Padding = new Thickness(12, 6, 12, 6), Command = new ViewerCommand(item.Item2) };
            button.SetResourceReference(StyleProperty, "PixelTart.Button.Secondary");
            controls.Children.Add(button);
        }
        Grid.SetRow(controls, 1);
        grid.Children.Add(controls);
        Content = grid;
        Loaded += async (_, _) => await LoadCurrentAsync();
    }

    internal bool IsShowingFullResolution { get; private set; }
    internal int DisplayedPixelWidth => (_image.Source as BitmapSource)?.PixelWidth ?? 0;
    internal int DisplayedPixelHeight => (_image.Source as BitmapSource)?.PixelHeight ?? 0;

    private async Task LoadCurrentAsync()
    {
        if (_paths.Count == 0) { Title = "像素蛋挞 · 没有可查看的照片"; return; }
        CancelCurrentLoad();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        var generation = Interlocked.Increment(ref _loadGeneration);
        var path = _paths[_index];
        IsShowingFullResolution = false;

        try
        {
            var firstFrame = await _thumbnails.GetAsync(new(path, 512), cancellationToken);
            if (!IsCurrent(generation, cancellationToken)) return;
            _image.Source = firstFrame.Bitmap;
            Title = firstFrame.IsAvailable ? $"查看大图 · {Path.GetFileName(path)}" : $"查看大图 · {firstFrame.PlaceholderMessage}";
            Fit();
            if (!firstFrame.IsAvailable || !File.Exists(path)) return;

            _loading.Visibility = Visibility.Visible;
            var fullResolution = await LoadFullResolutionAsync(path, cancellationToken);
            if (!IsCurrent(generation, cancellationToken)) return;
            _image.Source = fullResolution;
            IsShowingFullResolution = true;
            _loading.Visibility = Visibility.Collapsed;
            Title = $"查看大图 · {Path.GetFileName(path)} · {fullResolution.PixelWidth} × {fullResolution.PixelHeight}";
            Fit();
            PruneCache(path);
            PreloadNeighbor(_index - 1, cancellationToken);
            PreloadNeighbor(_index + 1, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { ShowFullResolutionFailure(path); }
        catch (NotSupportedException) { ShowFullResolutionFailure(path); }
        catch (ArgumentException) { ShowFullResolutionFailure(path); }
    }

    private async Task<BitmapSource> LoadFullResolutionAsync(string path, CancellationToken cancellationToken)
    {
        if (_fullResolutionCache.TryGetValue(path, out var cached)) return cached;
        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            // Deliberately no DecodePixelWidth: 100% means one source pixel per image pixel.
            image.UriSource = new Uri(Path.GetFullPath(path));
            image.EndInit();
            image.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            return (BitmapSource)image;
        }, cancellationToken);
        _fullResolutionCache[path] = result;
        return result;
    }

    private async void PreloadNeighbor(int index, CancellationToken parentToken)
    {
        if (index < 0 || index >= _paths.Count) return;
        var path = _paths[index];
        if (!File.Exists(path) || _fullResolutionCache.ContainsKey(path)) return;
        try { _ = await LoadFullResolutionAsync(path, parentToken); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (NotSupportedException) { }
        catch (ArgumentException) { }
    }

    private void PruneCache(string currentPath)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentPath };
        if (_index > 0) keep.Add(_paths[_index - 1]);
        if (_index + 1 < _paths.Count) keep.Add(_paths[_index + 1]);
        foreach (var key in _fullResolutionCache.Keys.Where(key => !keep.Contains(key)).ToArray()) _fullResolutionCache.Remove(key);
    }

    private bool IsCurrent(int generation, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && generation == Volatile.Read(ref _loadGeneration);

    private void CancelCurrentLoad()
    {
        _loading.Visibility = Visibility.Collapsed;
        if (_loadCancellation is null) return;
        try { _loadCancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        _loadCancellation.Dispose();
        _loadCancellation = null;
    }

    private void ShowFullResolutionFailure(string path)
    {
        _loading.Visibility = Visibility.Collapsed;
        Title = $"查看大图 · {Path.GetFileName(path)} · 原图载入失败";
    }

    private void Fit() { _image.Stretch = Stretch.Uniform; _scale.ScaleX = _scale.ScaleY = 1; _scroller.ScrollToHome(); }
    private void Actual() { _image.Stretch = Stretch.None; _scale.ScaleX = _scale.ScaleY = 1; }
    private void ZoomIn() => SetZoom(_scale.ScaleX * 1.2);
    private void ZoomOut() => SetZoom(_scale.ScaleX / 1.2);
    private void SetZoom(double value) { var zoom = Math.Clamp(value, .1, 8); _scale.ScaleX = _scale.ScaleY = zoom; }
    private void BeginPan(object sender, MouseButtonEventArgs e)
    {
        _panStart = e.GetPosition(_scroller); _panHorizontal = _scroller.HorizontalOffset; _panVertical = _scroller.VerticalOffset;
        _image.Cursor = Cursors.ScrollAll; _image.CaptureMouse(); e.Handled = true;
    }
    private void ContinuePan(object sender, MouseEventArgs e)
    {
        if (_panStart is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(_scroller);
        _scroller.ScrollToHorizontalOffset(_panHorizontal + start.X - current.X);
        _scroller.ScrollToVerticalOffset(_panVertical + start.Y - current.Y);
    }
    private void EndPan(object sender, MouseButtonEventArgs e)
    {
        _panStart = null; _image.ReleaseMouseCapture(); _image.Cursor = Cursors.Hand; e.Handled = true;
    }
    private async void Previous() { if (_index > 0) { _index--; await LoadCurrentAsync(); } }
    private async void Next() { if (_index + 1 < _paths.Count) { _index++; await LoadCurrentAsync(); } }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) { if (e.Delta > 0) ZoomIn(); else ZoomOut(); }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key) { case Key.Escape: Close(); break; case Key.Left: Previous(); break; case Key.Right: Next(); break; case Key.D0: Actual(); break; case Key.Add: case Key.OemPlus: ZoomIn(); break; case Key.Subtract: case Key.OemMinus: ZoomOut(); break; }
    }

    private sealed class ViewerCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
