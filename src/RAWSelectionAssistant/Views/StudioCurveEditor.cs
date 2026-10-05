using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

public sealed class StudioCurveEditor : StackPanel
{
    private readonly CurveSurface _surface;
    public StudioCurveEditor()
    {
        var channels = new ComboBox { ItemsSource = new[] { "RGB", "R", "G", "B" }, SelectedIndex = 0, Margin = new Thickness(0, 6, 0, 6) };
        Children.Add(channels); _surface = new CurveSurface { Height = 235, Margin = new Thickness(3) }; Children.Add(_surface);
        channels.SelectionChanged += (_, _) => _surface.Channel = channels.SelectedItem?.ToString()?.ToLowerInvariant() ?? "rgb";
        var actions = new WrapPanel();
        var reset = new Button { Margin = new Thickness(0, 5, 5, 5) }; StudioToolPanel.Text(reset, ContentControl.ContentProperty, "复位通道");
        reset.Click += (_, _) => _surface.Reset(); actions.Children.Add(reset);
        var remove = new Button { Margin = new Thickness(0, 5, 0, 5) }; StudioToolPanel.Text(remove, ContentControl.ContentProperty, "删除选中点");
        remove.Click += (_, _) => _surface.DeletePoint(); actions.Children.Add(remove); Children.Add(actions);
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
        StudioToolPanel.Text(hint, TextBlock.TextProperty, "点击增加控制点；拖动调整。Delete 删除内点，Esc 取消本次拖动。端点固定在 0 / 1 输入位置。"); Children.Add(hint);
    }
    private sealed class CurveSurface : FrameworkElement
    {
        private TetherReferenceModeViewModel? _editor;
        private string _channel = "rgb";
        private int _selected = -1;
        private IReadOnlyList<ColorStudioToolProcessor.CurvePoint>? _before;
        public string Channel { get => _channel; set { _channel = value; _selected = -1; InvalidateVisual(); } }
        public CurveSurface()
        {
            Focusable = true; ClipToBounds = true;
            Loaded += (_, _) => { Attach(); PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current, LanguageChanged, nameof(StudioLocalizationService.Language)); }; DataContextChanged += (_, _) => Attach();
            Unloaded += (_, _) => { if (_editor is not null) _editor.PropertyChanged -= Changed; _editor = null; PropertyChangedEventManager.RemoveHandler(StudioLocalizationService.Current, LanguageChanged, nameof(StudioLocalizationService.Language)); };
            MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += (_, _) => Finish();
            LostMouseCapture += (_, _) => { _editor?.CommitEditTransaction(); _before = null; };
            KeyDown += (_, e) => { if (e.Key == Key.Delete) { DeletePoint(); e.Handled = true; } else if (e.Key == Key.Escape && _before is { } before) { _editor?.SetCurvePoints(Channel, before); Finish(); e.Handled = true; } };
        }
        private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
        private void Attach()
        {
            var next = (DataContext as ReferenceColorWorkspaceViewModel)?.Editor;
            if (ReferenceEquals(next, _editor)) return;
            if (_editor is not null) _editor.PropertyChanged -= Changed;
            _editor = next; if (_editor is not null) _editor.PropertyChanged += Changed; InvalidateVisual();
        }
        private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(TetherReferenceModeViewModel.AdjustmentStack) or nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode)) InvalidateVisual(); }
        private IReadOnlyList<ColorStudioToolProcessor.CurvePoint> Points => ColorStudioToolProcessor.ReadCurve(_editor?.ToolNode(ColorStudioNodeType.Curve) ?? new(Guid.NewGuid(), ColorStudioNodeType.Curve, "曲线"), Channel);
        private Rect Plot => new(20, 10, Math.Max(1, ActualWidth - 30), Math.Max(1, ActualHeight - 34));
        private Point Screen(ColorStudioToolProcessor.CurvePoint p) => new(Plot.Left + p.X * Plot.Width, Plot.Bottom - p.Y * Plot.Height);
        private ColorStudioToolProcessor.CurvePoint Curve(Point p) => new(Math.Clamp((p.X - Plot.Left) / Plot.Width, 0, 1), Math.Clamp((Plot.Bottom - p.Y) / Plot.Height, 0, 1));
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 22, 24)), null, new Rect(RenderSize));
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(53, 59, 63)), 1);
            for (var i = 0; i <= 4; i++) { var t = i / 4d; dc.DrawLine(pen, Screen(new(t, 0)), Screen(new(t, 1))); dc.DrawLine(pen, Screen(new(0, t)), Screen(new(1, t))); }
            var points = Points; var geometry = new StreamGeometry();
            using (var g = geometry.Open()) { g.BeginFigure(Screen(new(0, ColorStudioToolProcessor.EvaluateCurve(points, 0))), false, false); for (var i = 1; i <= 256; i++) { var x = i / 256d; g.LineTo(Screen(new(x, ColorStudioToolProcessor.EvaluateCurve(points, x))), true, false); } }
            var brush = Channel switch { "r" => Brushes.Coral, "g" => Brushes.LightGreen, "b" => Brushes.DodgerBlue, _ => Brushes.Turquoise };
            dc.DrawGeometry(null, new Pen(brush, 2), geometry);
            for (var i = 0; i < points.Count; i++) dc.DrawEllipse(i == _selected ? Brushes.White : brush, new Pen(Brushes.Black, 1), Screen(points[i]), 4, 4);
            var text = StudioLocalizationService.Current;
            var label = _selected >= 0 && _selected < points.Count ? $"{text["输入"]} {points[_selected].X:0.###} → {text["输出"]} {points[_selected].Y:0.###}" : $"0                                   {text["输入"]} 1";
            dc.DrawText(new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(20, Plot.Bottom + 6));
        }
        private void Down(object sender, MouseButtonEventArgs e)
        {
            if (_editor is null) return;
            Focus(); var pos = e.GetPosition(this); if (!Plot.Contains(pos)) return;
            var list = Points.ToList(); _before = list.ToArray(); _editor.BeginEditTransaction();
            _selected = list.FindIndex(p => (Screen(p) - pos).Length < 9);
            if (_selected < 0)
            {
                if (list.Count >= 16) { _before = null; _editor.CommitEditTransaction(); return; }
                var p = Curve(pos); if (list.Any(x => Math.Abs(x.X - p.X) < .005)) { Finish(); return; }
                list.Add(p); list.Sort((a, b) => a.X.CompareTo(b.X)); _selected = list.IndexOf(p); _editor.SetCurvePoints(Channel, list);
            }
            CaptureMouse(); InvalidateVisual(); e.Handled = true;
        }
        private void Move(object sender, MouseEventArgs e)
        {
            if (!IsMouseCaptured || _selected < 0 || _editor is null) return;
            var list = Points.ToArray(); if (_selected >= list.Length) return;
            var p = Curve(e.GetPosition(this)); var x = _selected == 0 ? 0 : _selected == list.Length - 1 ? 1 : Math.Clamp(p.X, list[_selected - 1].X + .001, list[_selected + 1].X - .001);
            list[_selected] = new(x, p.Y); _editor.SetCurvePoints(Channel, list);
        }
        private void Finish() { _editor?.CommitEditTransaction(); _before = null; if (IsMouseCaptured) ReleaseMouseCapture(); }
        public void DeletePoint() { var points = Points; if (_selected <= 0 || _selected >= points.Count - 1) return; _editor?.SetCurvePoints(Channel, points.Where((_, i) => i != _selected).ToArray()); _selected = -1; InvalidateVisual(); }
        public void Reset() { _editor?.SetCurvePoints(Channel, new[] { new ColorStudioToolProcessor.CurvePoint(0, 0), new ColorStudioToolProcessor.CurvePoint(1, 1) }); _selected = -1; }
    }
}
