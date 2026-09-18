using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RAWSelectionAssistant.Views;

/// <summary>Geometry-only original/matched comparison. Dragging never decodes or renders pixels.</summary>
public sealed class TetherReferenceSplitView : Grid
{
    private readonly Image _original = new();
    private readonly Image _matched = new();
    private readonly Border _hitArea = new() { Width = 18, Background = Brushes.Transparent, Cursor = Cursors.SizeWE, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Rectangle _line = new() { Width = 1, Fill = Brushes.White, Opacity = .8, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    private readonly Border _handle = new() { Width = 12, Height = 24, CornerRadius = new(6), Background = Brushes.White, Opacity = .82, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private bool _dragging;

    public TetherReferenceSplitView()
    {
        ClipToBounds = true; Children.Add(_original); Children.Add(_matched); Children.Add(_line); Children.Add(_handle); Children.Add(_hitArea);
        AutomationProperties.SetName(_hitArea, "拖动参考仿色分割线");
        SizeChanged += (_, _) => UpdateGeometry();
        _hitArea.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { SplitPosition = .5; e.Handled = true; return; } _dragging = true; _hitArea.CaptureMouse(); UpdateFrom(e); e.Handled = true; };
        _hitArea.MouseMove += (_, e) => { if (_dragging && e.LeftButton == MouseButtonState.Pressed) UpdateFrom(e); };
        _hitArea.MouseLeftButtonUp += (_, e) => { _dragging = false; _hitArea.ReleaseMouseCapture(); e.Handled = true; };
        _hitArea.LostMouseCapture += (_, _) => _dragging = false;
    }

    public static readonly DependencyProperty OriginalProperty = DependencyProperty.Register(nameof(Original), typeof(BitmapSource), typeof(TetherReferenceSplitView), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty MatchedProperty = DependencyProperty.Register(nameof(Matched), typeof(BitmapSource), typeof(TetherReferenceSplitView), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ImageStretchProperty = DependencyProperty.Register(nameof(ImageStretch), typeof(Stretch), typeof(TetherReferenceSplitView), new PropertyMetadata(Stretch.Uniform, Changed));
    public static readonly DependencyProperty SplitPositionProperty = DependencyProperty.Register(nameof(SplitPosition), typeof(double), typeof(TetherReferenceSplitView), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed, CoerceSplit));
    public BitmapSource? Original { get => (BitmapSource?)GetValue(OriginalProperty); set => SetValue(OriginalProperty, value); }
    public BitmapSource? Matched { get => (BitmapSource?)GetValue(MatchedProperty); set => SetValue(MatchedProperty, value); }
    public Stretch ImageStretch { get => (Stretch)GetValue(ImageStretchProperty); set => SetValue(ImageStretchProperty, value); }
    public double SplitPosition { get => (double)GetValue(SplitPositionProperty); set => SetValue(SplitPositionProperty, value); }
    private static object CoerceSplit(DependencyObject _, object value) => Math.Clamp((double)value, 0, 1);
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs _) => ((TetherReferenceSplitView)sender).UpdateGeometry();
    private void UpdateFrom(MouseEventArgs e) { if (ActualWidth > 0) SplitPosition = e.GetPosition(this).X / ActualWidth; }
    private void UpdateGeometry()
    {
        _original.Source = Original; _matched.Source = Matched; _original.Stretch = _matched.Stretch = ImageStretch;
        var x = Math.Clamp(ActualWidth * SplitPosition, 0, ActualWidth);
        _matched.Clip = new RectangleGeometry(new Rect(x, 0, Math.Max(0, ActualWidth - x), ActualHeight));
        _line.Margin = new(x, 0, 0, 0); _handle.Margin = new(x - _handle.Width / 2, 0, 0, 0); _hitArea.Margin = new(x - _hitArea.Width / 2, 0, 0, 0);
    }
}
