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
        if (State is null) { DrawLabel(drawing, "3D 色彩空间 · 等待真实 ColorSpaceVisualizationModel", new Point(18, 18)); return; }
        DrawAxes(drawing);
        foreach (var cloud in State.VisibleClouds)
            foreach (var point in ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight, 1))
                drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(point.Color.R, point.Color.G, point.Color.B)), null, new Point(point.X, point.Y), 2.2, 2.2);
        if (State.ShowMigrationVectors && State.Mode is ColorCloudMode.Migration or ColorCloudMode.Overlay)
            foreach (var vector in State.Model.MigrationVectors.Take(512)) DrawVector(drawing, vector);
        DrawLabel(drawing, $"L*  a*  b*   {State.Mode}   ·   左键旋转 / Shift+左键平移 / 滚轮缩放", new Point(12, Math.Max(12, ActualHeight - 28)));
        _lastRenderMilliseconds = clock.Elapsed.TotalMilliseconds;
    }
    private void DrawAxes(DrawingContext drawing) { var center = new Point(ActualWidth / 2, ActualHeight / 2); var pen = new Pen(TryFindResource("DividerBrush") as Brush ?? Brushes.Gray, 1); drawing.DrawLine(pen, new Point(12, center.Y), new Point(Math.Max(12, ActualWidth - 12), center.Y)); drawing.DrawLine(pen, new Point(center.X, 12), new Point(center.X, Math.Max(12, ActualHeight - 12))); DrawLabel(drawing, "a*", new Point(Math.Max(12, ActualWidth - 32), center.Y + 4)); DrawLabel(drawing, "L*", new Point(center.X + 6, 12)); DrawLabel(drawing, "b*", new Point(center.X + 6, Math.Max(12, ActualHeight - 24))); }
    private void DrawVector(DrawingContext drawing, ColorMigrationVector vector) { var cloud = new ColorSpaceCloud(1, 1, 1, 1, [vector.Source], "", new()); var source = ColorSpaceProjection.Project(cloud, State!.Camera, ActualWidth, ActualHeight).Single(); cloud = cloud with { Points = [vector.Matched] }; var matched = ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight).Single(); drawing.DrawLine(new Pen(Brushes.White, .7), new Point(source.X, source.Y), new Point(matched.X, matched.Y)); }
    private void DrawLabel(DrawingContext drawing, string text, Point origin) { var brush = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray; drawing.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, 1), origin); }
    private void OnMouseDown(object sender, MouseButtonEventArgs e) { Focus(); _pointer = e.GetPosition(this); CaptureMouse(); }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (_pointer is not { } previous || State is null || e.LeftButton != MouseButtonState.Pressed) return; var current = e.GetPosition(this); var delta = current - previous; _pointer = current; var camera = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? ColorSpaceProjection.PanByDisplayDelta(State.Camera, delta.X, delta.Y, ActualWidth, ActualHeight) : State.Camera.Rotate(delta.X * .35, -delta.Y * .35); State = State with { Camera = camera, IsFit = false }; }
    private void OnMouseUp(object sender, MouseButtonEventArgs e) { _pointer = null; ReleaseMouseCapture(); }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) { if (State is null) return; State = State with { Camera = State.Camera.Zoom(e.Delta > 0 ? 1.12 : .89), IsFit = false }; }
}
