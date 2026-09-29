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
    public ColorSpaceRendererState? State { get => _state; set { _state = value; InvalidateVisual(); } }
    public ColorCloudMode Mode => State?.Mode ?? ColorCloudMode.Overlay;
    public bool IsAvailable => State is not null;
    public ColorSpace3DViewport() { Focusable = true; ClipToBounds = true; MouseLeftButtonDown += OnMouseDown; MouseMove += OnMouseMove; MouseLeftButtonUp += OnMouseUp; MouseWheel += OnMouseWheel; }
    public void SetModel(ColorSpaceVisualizationModel model) => State = ColorSpaceRendererContract.Create(model);
    public void ResetCamera() { if (State is not null) State = State with { Camera = State.Camera.Reset(), IsFit = false }; }
    public void FitCamera() { if (State is not null) State = State.Fit(); }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing); drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var background = TryFindResource("CanvasBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(25, 25, 25)); drawing.DrawRectangle(background, null, new Rect(RenderSize));
        if (State is null) { DrawLabel(drawing, "3D 色彩空间 · 等待真实 ColorSpaceVisualizationModel", new Point(18, 18)); return; }
        DrawAxes(drawing);
        foreach (var cloud in State.Model.VisibleClouds)
            foreach (var point in ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight, 1))
                drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(point.Color.R, point.Color.G, point.Color.B)), null, new Point(point.X, point.Y), 2.2, 2.2);
        if (State.ShowMigrationVectors && State.Mode is ColorCloudMode.Migration or ColorCloudMode.Overlay)
            foreach (var vector in State.Model.MigrationVectors.Take(512)) DrawVector(drawing, vector);
        DrawLabel(drawing, $"L*  a*  b*   {State.Mode}   ·   左键旋转 / Shift+左键平移 / 滚轮缩放", new Point(12, Math.Max(12, ActualHeight - 28)));
    }
    private void DrawAxes(DrawingContext drawing) { var center = new Point(ActualWidth / 2, ActualHeight / 2); var pen = new Pen(TryFindResource("DividerBrush") as Brush ?? Brushes.Gray, 1); drawing.DrawLine(pen, new Point(12, center.Y), new Point(Math.Max(12, ActualWidth - 12), center.Y)); drawing.DrawLine(pen, new Point(center.X, 12), new Point(center.X, Math.Max(12, ActualHeight - 12))); DrawLabel(drawing, "a*", new Point(Math.Max(12, ActualWidth - 32), center.Y + 4)); DrawLabel(drawing, "L*", new Point(center.X + 6, 12)); DrawLabel(drawing, "b*", new Point(center.X + 6, Math.Max(12, ActualHeight - 24))); }
    private void DrawVector(DrawingContext drawing, ColorMigrationVector vector) { var cloud = new ColorSpaceCloud(1, 1, 1, 1, [vector.Source], "", new()); var source = ColorSpaceProjection.Project(cloud, State!.Camera, ActualWidth, ActualHeight).Single(); cloud = cloud with { Points = [vector.Matched] }; var matched = ColorSpaceProjection.Project(cloud, State.Camera, ActualWidth, ActualHeight).Single(); drawing.DrawLine(new Pen(Brushes.White, .7), new Point(source.X, source.Y), new Point(matched.X, matched.Y)); }
    private void DrawLabel(DrawingContext drawing, string text, Point origin) { var brush = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray; drawing.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, 1), origin); }
    private void OnMouseDown(object sender, MouseButtonEventArgs e) { Focus(); _pointer = e.GetPosition(this); CaptureMouse(); }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (_pointer is not { } previous || State is null || e.LeftButton != MouseButtonState.Pressed) return; var current = e.GetPosition(this); var delta = current - previous; _pointer = current; var camera = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? State.Camera.Pan(delta.X / Math.Max(1, ActualWidth), delta.Y / Math.Max(1, ActualHeight)) : State.Camera.Rotate(delta.X * .35, -delta.Y * .35); State = State with { Camera = camera, IsFit = false }; }
    private void OnMouseUp(object sender, MouseButtonEventArgs e) { _pointer = null; ReleaseMouseCapture(); }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) { if (State is null) return; State = State with { Camera = State.Camera.Zoom(e.Delta > 0 ? 1.12 : .89), IsFit = false }; }
}
