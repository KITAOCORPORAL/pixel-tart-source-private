using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>Windows surface for the platform-neutral ColorSpaceVisualizationModel.</summary>
public sealed class ColorSpace3DViewport : FrameworkElement
{
    private Point? _pointer;
    private ColorSpaceRendererState? _state;
    private long _renderCount;
    private double _lastRenderMilliseconds;
    private Point? _clickStart;
    public event EventHandler<ColorSpaceSelection>? SelectionChanged;
    internal object ReadNativeEvidence() => new
    {
        Bounds = ReferenceColorWorkspaceView.NativeBounds(this), State?.Camera, State?.IsFit,
        ModelLoaded = State is not null, SampleCount = State?.VisibleClouds.Sum(c => c.Points.Count) ?? 0,
        RenderCount = _renderCount, LastRenderMilliseconds = _lastRenderMilliseconds,
        Backend = "WPF DrawingContext / CPU point projection", IsMouseCaptured,
        ProjectedBounds = State is null ? null : ProjectedModelBounds()
    };
    private object? ProjectedModelBounds()
    {
        var points = State!.VisibleClouds.SelectMany(c => ColorSpaceProjection.Project(c, State.Camera, ActualWidth, ActualHeight)).ToArray();
        return points.Length == 0 ? null : new { MinX = points.Min(p => p.X), MaxX = points.Max(p => p.X), MinY = points.Min(p => p.Y), MaxY = points.Max(p => p.Y) };
    }
    public ColorSpaceRendererState? State { get => _state; set { _state = value; InvalidateVisual(); } }
    public ColorCloudMode Mode => State?.Mode ?? ColorCloudMode.Overlay;
    public bool IsAvailable => State is not null;
    public ColorSpace3DViewport()
    {
        Focusable = true; ClipToBounds = true;
        MouseLeftButtonDown += OnMouseDown; MouseMove += OnMouseMove; MouseLeftButtonUp += OnMouseUp; MouseWheel += OnMouseWheel;
        SizeChanged += (_, _) => { if (State?.IsFit == true) FitCamera(); else InvalidateVisual(); };
        LostMouseCapture += (_, _) => _pointer = null;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _pointer is not null) { _pointer = null; ReleaseMouseCapture(); e.Handled = true; } };
    }
    public void SetModel(ColorSpaceVisualizationModel model) => State = ColorSpaceRendererContract.Create(model);
    public void ResetCamera() { if (State is not null) State = State with { Camera = State.Camera.Reset(), IsFit = false }; }
    public void FitCamera() { if (State is not null) State = State.Fit(ActualWidth, ActualHeight); }
    protected override void OnRender(DrawingContext drawing)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _renderCount++;
        base.OnRender(drawing); drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var background = TryFindResource("CanvasBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(25, 25, 25)); drawing.DrawRectangle(background, null, new Rect(RenderSize));
        if (State is null) { DrawLabel(drawing, "点击“生成当前模型”查看色彩分布", new Point(18, 18)); return; }
        DrawAxes(drawing);
        DrawLabel(drawing, "L 明度 0–1 · a 绿↔红 · b 蓝↔黄", new Point(10, 8));
        if (ActualHeight >= 260) DrawLabel(drawing, "离中性轴越远，色度越高 · 点色＝原片采样", new Point(10, 26));
        foreach (var cloud in State.VisibleClouds)
            foreach (var point in ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight, 1))
            {
                var selected = State.Selection.Kind == ColorSpaceMarkerKind.SelectedCluster && State.Selection.PointIndex == point.PointIndex;
                drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(point.Color.R, point.Color.G, point.Color.B)), selected ? new Pen(Brushes.White, 2) : null, new Point(point.X, point.Y), selected ? 5 : 2.2, selected ? 5 : 2.2);
            }
        if (State.ShowMigrationVectors && State.Mode is ColorCloudMode.Migration or ColorCloudMode.Overlay)
            foreach (var vector in State.Model.MigrationVectors.Take(512)) DrawVector(drawing, vector);
        // Draw the selection last so dense clouds cannot cover the picked point.
        if (State.Selection.Kind == ColorSpaceMarkerKind.SelectedCluster)
            foreach (var point in State.VisibleClouds.SelectMany(cloud => ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight))
                .Where(point => point.PointIndex == State.Selection.PointIndex))
            {
                var center = new Point(point.X, point.Y);
                drawing.DrawEllipse(null, new Pen(Brushes.Black, 5), center, 9, 9);
                drawing.DrawEllipse(null, new Pen(Brushes.White, 2), center, 9, 9);
            }
        DrawLabel(drawing, "OKLab · 拖动旋转 / Shift 平移", new Point(12, Math.Max(12, ActualHeight - 22)));
        _lastRenderMilliseconds = clock.Elapsed.TotalMilliseconds;
    }
    private void DrawAxes(DrawingContext drawing)
    {
        // Project the coordinate guides with the same camera as the real color samples.
        var neutral = new RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis.VisualRgb24(150, 150, 150);
        ColorSpacePoint Point(double l, double a, double b) => new(new(l, a, b), neutral, 0, 0);
        var axes = new ColorSpaceCloud(1, 1, 1, 1, [Point(.5, -.4, 0), Point(.5, .4, 0), Point(0, 0, 0), Point(1, 0, 0), Point(.5, 0, -.4), Point(.5, 0, .4)], "axes", new());
        var points = ColorSpaceProjection.Project(axes, State!.Camera, ActualWidth, ActualHeight);
        var pen = new Pen(TryFindResource("DividerBrush") as Brush ?? Brushes.Gray, 1);
        foreach (var (start, end, label) in new[] { (0, 1, "a"), (2, 3, "L"), (4, 5, "b") })
        {
            drawing.DrawLine(pen, new Point(points[start].X, points[start].Y), new Point(points[end].X, points[end].Y));
            DrawLabel(drawing, label, new Point(Math.Clamp(points[end].X, 4, Math.Max(4, ActualWidth - 18)), Math.Clamp(points[end].Y, 4, Math.Max(4, ActualHeight - 38))));
        }
        if (ActualWidth >= 300 && ActualHeight >= 260)
        {
            foreach (var axis in new[]{0,1,2})
            foreach (var value in axis==0 ? new[]{0d,.25,.5,.75,1d} : new[]{-.4,-.2,0,.2,.4})
            {
                var marker = axis==0 ? Point(value,0,0) : axis==1 ? Point(.5,value,0) : Point(.5,0,value);
                var tick=ColorSpaceProjection.Project(axes with { Points=[marker] },State!.Camera,ActualWidth,ActualHeight)[0];
                if(tick.X<6 || tick.X>ActualWidth-38 || tick.Y<48 || tick.Y>ActualHeight-44)continue;
                drawing.DrawEllipse(Brushes.Gray,null,new Point(tick.X,tick.Y),1.8,1.8);
                DrawLabel(drawing,value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),new Point(tick.X+4,tick.Y));
            }
        }
    }
    private void DrawVector(DrawingContext drawing, ColorMigrationVector vector) { var cloud = new ColorSpaceCloud(1, 1, 1, 1, [vector.Source], "", new()); var source = ColorSpaceProjection.Project(cloud, State!.Camera, ActualWidth, ActualHeight).Single(); cloud = cloud with { Points = [vector.Matched] }; var matched = ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight).Single(); drawing.DrawLine(new Pen(Brushes.White, .7), new Point(source.X, source.Y), new Point(matched.X, matched.Y)); }
    private void DrawLabel(DrawingContext drawing, string text, Point origin) { var brush = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray; drawing.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), origin); }
    private void OnMouseDown(object sender, MouseButtonEventArgs e) { Focus(); _pointer = _clickStart = e.GetPosition(this); CaptureMouse(); }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (_pointer is not { } previous || State is null || e.LeftButton != MouseButtonState.Pressed) return; var current = e.GetPosition(this); var delta = current - previous; _pointer = current; var camera = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? ColorSpaceProjection.PanByDisplayDelta(State.Camera, delta.X, delta.Y, ActualWidth, ActualHeight) : State.Camera.Rotate(delta.X * .35, -delta.Y * .35); State = State with { Camera = camera, IsFit = false }; }
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var start = _clickStart; _pointer = null; _clickStart = null; ReleaseMouseCapture();
        if (State is null || start is not { } point || (e.GetPosition(this) - point).Length > SystemParameters.MinimumHorizontalDragDistance) return;
        var projected = State.VisibleClouds.SelectMany(cloud => ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight)).ToArray();
        var index = ColorSpaceProjection.HitTest(projected, point.X, point.Y);
        State = State with { Selection = index >= 0 ? new(ColorSpaceMarkerKind.SelectedCluster, index) : ColorSpaceSelection.None };
        SelectionChanged?.Invoke(this, State.Selection);
    }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) { if (State is null) return; State = State with { Camera = State.Camera.Zoom(e.Delta > 0 ? 1.12 : .89), IsFit = false }; }
}
