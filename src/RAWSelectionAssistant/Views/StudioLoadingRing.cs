using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RAWSelectionAssistant.Views;

/// <summary>Unknown progress indicator. No invented percentage; stops when hidden.</summary>
public sealed class StudioLoadingRing : Control
{
    private readonly DispatcherTimer _timer;
    private double _angle;
    public StudioLoadingRing()
    {
        Width = Height = 20;
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += (_, _) => { _angle = (_angle + 24) % 360; InvalidateVisual(); };
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => UpdateAnimation();
        SetResourceReference(ForegroundProperty, "AccentBrush");
        System.Windows.Automation.AutomationProperties.SetName(this, "正在处理");
    }
    private void UpdateAnimation() { if (IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation) _timer.Start(); else _timer.Stop(); }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 2);
        context.PushTransform(new RotateTransform(_angle, center.X, center.Y));
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            context.PushOpacity((i + 1d) / 8);
            context.DrawEllipse(Foreground, null, new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)), 1.5, 1.5);
            context.Pop();
        }
        context.Pop();
    }
}
