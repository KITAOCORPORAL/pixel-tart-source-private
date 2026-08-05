using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public sealed class TetherHistogramView : FrameworkElement
{
    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(
        nameof(Histogram), typeof(TetherHistogramData), typeof(TetherHistogramView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public TetherHistogramData? Histogram { get => (TetherHistogramData?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(75, 0, 0, 0)), new Pen(new SolidColorBrush(Color.FromArgb(70, 150, 150, 150)), 1), new Rect(0, 0, ActualWidth, ActualHeight), 5, 5);
        if (Histogram is null || ActualWidth <= 1 || ActualHeight <= 1) return;
        DrawChannel(drawingContext, Histogram.Luminance, Color.FromArgb(110, 225, 225, 225), 1);
        DrawChannel(drawingContext, Histogram.Red, Color.FromArgb(210, 255, 82, 82), 1.2);
        DrawChannel(drawingContext, Histogram.Green, Color.FromArgb(210, 80, 220, 125), 1.2);
        DrawChannel(drawingContext, Histogram.Blue, Color.FromArgb(210, 80, 145, 255), 1.2);
    }

    private void DrawChannel(DrawingContext drawing, int[] values, Color color, double thickness)
    {
        if (values.Length != 256) return;
        var max = Math.Max(1, values.Max());
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, ActualHeight), true, true);
            for (var index = 0; index < values.Length; index++)
            {
                var x = index / 255d * ActualWidth;
                var normalized = Math.Sqrt(values[index] / (double)max);
                context.LineTo(new Point(x, ActualHeight - normalized * Math.Max(1, ActualHeight - 2)), true, false);
            }
            context.LineTo(new Point(ActualWidth, ActualHeight), true, false);
        }
        geometry.Freeze();
        var fill = new SolidColorBrush(Color.FromArgb((byte)Math.Min(54, (int)color.A), color.R, color.G, color.B));
        drawing.DrawGeometry(fill, new Pen(new SolidColorBrush(color), thickness), geometry);
    }
}
