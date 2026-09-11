using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetViewerWindow : Window
{
    private readonly IAssetThumbnailProvider _thumbnails;
    private readonly IReadOnlyList<string> _paths;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly ScaleTransform _scale = new(1, 1);
    private int _index;

    public AssetViewerWindow(IReadOnlyList<string> paths, int startIndex, IAssetThumbnailProvider? thumbnails = null)
    {
        _paths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _paths.Count - 1));
        _thumbnails = thumbnails ?? AsyncThumbnail.Provider;
        Title = "Pixel Tart Viewer";
        Background = new SolidColorBrush(Color.FromRgb(8, 10, 12));
        WindowState = WindowState.Maximized;
        WindowStyle = WindowStyle.None;
        KeyDown += OnKeyDown;
        MouseWheel += OnMouseWheel;
        _image.RenderTransformOrigin = new Point(.5, .5);
        _image.RenderTransform = _scale;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(_image);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
        foreach (var item in new[] { ("上一张", (Action)Previous), ("适应", Fit), ("100%", Actual), ("−", ZoomOut), ("+", ZoomIn), ("下一张", Next), ("返回", Close) })
        {
            var button = new Button { Content = item.Item1, Margin = new Thickness(4), Padding = new Thickness(12, 6, 12, 6), Command = new ViewerCommand(item.Item2) };
            controls.Children.Add(button);
        }
        Grid.SetRow(controls, 1); grid.Children.Add(controls); Content = grid;
        Loaded += async (_, _) => await LoadCurrentAsync();
    }

    private async Task LoadCurrentAsync()
    {
        if (_paths.Count == 0) { Title = "Pixel Tart Viewer · 没有可查看的素材"; return; }
        var path = _paths[_index];
        var result = await _thumbnails.GetAsync(new(path, 512));
        _image.Source = result.Bitmap;
        Title = result.IsAvailable ? $"Pixel Tart Viewer · {System.IO.Path.GetFileName(path)}" : $"Pixel Tart Viewer · {result.PlaceholderMessage}";
        Fit();
    }

    private void Fit() { _image.Stretch = Stretch.Uniform; _scale.ScaleX = _scale.ScaleY = 1; }
    private void Actual() { _image.Stretch = Stretch.None; _scale.ScaleX = _scale.ScaleY = 1; }
    private void ZoomIn() => SetZoom(_scale.ScaleX * 1.2);
    private void ZoomOut() => SetZoom(_scale.ScaleX / 1.2);
    private void SetZoom(double value) { var zoom = Math.Clamp(value, .1, 8); _scale.ScaleX = _scale.ScaleY = zoom; }
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
