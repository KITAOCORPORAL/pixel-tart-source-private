using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>A graphical editor of the existing hue/amount pair, not another color model.</summary>
internal sealed class StudioColorBalanceWheel : FrameworkElement
{
    private readonly Func<TetherReferenceModeViewModel?> _editor;
    private readonly ColorStudioToolParameter _hue, _amount;
    private bool _dragging;
    internal StudioColorBalanceWheel(Func<TetherReferenceModeViewModel?> editor, ColorStudioToolParameter hue, ColorStudioToolParameter amount)
    {
        _editor = editor; _hue = hue; _amount = amount;
        Height = 150; Focusable = true; Cursor = Cursors.Cross; Margin = new Thickness(0, 3, 0, 5);
        StudioToolPanel.Text(this, System.Windows.Automation.AutomationProperties.NameProperty, "ColorBalanceWheel");
        MouseLeftButtonDown += (_, e) =>
        {
            if (_editor() is not { } model) return;
            Focus(); model.BeginEditTransaction(); _dragging = true; CaptureMouse(); Update(e.GetPosition(this)); e.Handled = true;
        };
        MouseMove += (_, e) => { if (_dragging) { Update(e.GetPosition(this)); e.Handled = true; } };
        MouseLeftButtonUp += (_, e) => { Finish(false); e.Handled = true; };
        LostMouseCapture += (_, _) => Finish(false);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _dragging) { Finish(true); e.Handled = true; } };
        Unloaded += (_, _) => Finish(true);
    }
    internal static (double Hue, double Amount) FromPosition(Vector position, double radius)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(radius) || radius <= 0) return (0, 0);
        var hue = Math.Atan2(-position.Y, position.X) * 180 / Math.PI;
        return ((hue + 360) % 360, Math.Clamp(position.Length / radius * 100, 0, 100));
    }
    private Point Center => new(ActualWidth / 2, ActualHeight / 2);
    private double Radius => Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 8);
    private void Update(Point point)
    {
        if (_editor() is not { } model) return;
        var pair = FromPosition(point - Center, Radius);
        model.SetToolParameter(_hue, pair.Hue); model.SetToolParameter(_amount, pair.Amount); InvalidateVisual();
    }
    private void Finish(bool cancel)
    {
        if (!_dragging) return;
        _dragging = false;
        if (cancel) _editor()?.CancelEditTransaction(); else _editor()?.CommitEditTransaction();
        if (IsMouseCaptured) ReleaseMouseCapture(); InvalidateVisual();
    }
    internal bool CancelPointerGesture() { if (!_dragging) return false; Finish(true); return true; }
    protected override void OnRender(DrawingContext dc)
    {
        var c = Center; var r = Radius;
        // Hue is the existing engine's angle. Radial distance is its amount;
        // this is a control visualization, not a gamut or sampled photograph.
        for (var i = 0; i < 180; i++)
        {
            var a = i * 2 * Math.PI / 180; var b = (i * 2 + 2.2) * Math.PI / 180;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open()) { g.BeginFigure(c, true, true); g.LineTo(c + new Vector(Math.Cos(a), -Math.Sin(a)) * r, true, false); g.LineTo(c + new Vector(Math.Cos(b), -Math.Sin(b)) * r, true, false); }
            dc.DrawGeometry(new SolidColorBrush(HueColor(i * 2)), null, geometry);
        }
        var neutral = new RadialGradientBrush(); neutral.GradientStops.Add(new GradientStop(Color.FromArgb(245, 135, 139, 139), 0)); neutral.GradientStops.Add(new GradientStop(Color.FromArgb(0, 135, 139, 139), 1));
        dc.DrawEllipse(neutral, new Pen(Brushes.Gray, 1), c, r, r);
        dc.DrawLine(new Pen(Brushes.Gray, .5), c - new Vector(r, 0), c + new Vector(r, 0));
        dc.DrawLine(new Pen(Brushes.Gray, .5), c - new Vector(0, r), c + new Vector(0, r));
        var hue = (_editor()?.ToolValue(_hue) ?? 0) * Math.PI / 180;
        var amount = (_editor()?.ToolValue(_amount) ?? 0) / 100;
        dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 4), c + new Vector(Math.Cos(hue), -Math.Sin(hue)) * r * amount, 5, 5);
        dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 2), c + new Vector(Math.Cos(hue), -Math.Sin(hue)) * r * amount, 5, 5);
    }
    private static Color HueColor(double hue)
    {
        var h = hue / 60; var x = 1 - Math.Abs(h % 2 - 1);
        var (r, g, b) = (int)h switch { 0 => (1d,x,0d), 1 => (x,1d,0d), 2 => (0d,1d,x), 3 => (0d,x,1d), 4 => (x,0d,1d), _ => (1d,0d,x) };
        return Color.FromRgb((byte)(r * 210), (byte)(g * 210), (byte)(b * 210));
    }
}
