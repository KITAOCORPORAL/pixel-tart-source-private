using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>Selector icon only. The full photo continues through the versioned renderer.</summary>
public sealed class LightPrintFrameThumbnail : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(LightPrintFrame), typeof(LightPrintFrameThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public LightPrintFrame? Frame { get => (LightPrintFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Frame is not { } frame || ActualWidth <= 0 || ActualHeight <= 0) return;
        var paper = frame.Color switch { "Cool" => Color.FromRgb(221,227,232), "Neutral" => Color.FromRgb(230,230,228), _ => Color.FromRgb(236,232,219) };
        Brush fill = new SolidColorBrush(paper);
        if (frame.Pattern == "Foil") fill = new LinearGradientBrush(Color.FromRgb(161,172,181), Color.FromRgb(239,241,242), 35);
        var bounds = new Rect(1,1,Math.Max(1,ActualWidth-2),Math.Max(1,ActualHeight-2));
        dc.DrawRectangle(fill, null, bounds);
        if (frame.Pattern == "Dots")
            for (double y=5; y<bounds.Bottom; y+=7)
                for (double x=5; x<bounds.Right; x+=7)
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(176,163,145)), null, new Point(x,y), 1, 1);
        var window = new Rect(7,6,Math.Max(1,ActualWidth-14),Math.Max(1,ActualHeight-22));
        var ink = new SolidColorBrush(Color.FromRgb(30,31,33));
        if (frame.Window == "Circle") dc.DrawEllipse(ink, null, new Point(window.Left+window.Width/2,window.Top+window.Height/2), Math.Min(window.Width,window.Height)/2, Math.Min(window.Width,window.Height)/2);
        else dc.DrawRectangle(ink, null, window);
    }
}
