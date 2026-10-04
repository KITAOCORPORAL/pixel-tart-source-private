using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed class HistogramDrawing : FrameworkElement
{
    public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(AssetVisualAnalysisResult), typeof(HistogramDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public AssetVisualAnalysisResult? Analysis { get => (AssetVisualAnalysisResult?)GetValue(AnalysisProperty); set => SetValue(AnalysisProperty, value); }

    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(nameof(Histogram), typeof(VisualHistogram), typeof(HistogramDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public VisualHistogram? Histogram { get => (VisualHistogram?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
    public static readonly DependencyProperty ShowLumaProperty = DependencyProperty.Register(nameof(ShowLuma), typeof(bool), typeof(HistogramDrawing), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowLuma { get => (bool)GetValue(ShowLumaProperty); set => SetValue(ShowLumaProperty, value); }

    public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register(nameof(Channel), typeof(string), typeof(HistogramDrawing), new FrameworkPropertyMetadata("RGB", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Channel { get => (string)GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }
    private uint _maximum;
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 16, 17)), null, new Rect(RenderSize));
        if ((Analysis is null && Histogram is null) || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(80, 150, 155, 160)), .5);
        for (var i = 1; i < 4; i++) { drawingContext.DrawLine(grid, new Point(ActualWidth*i/4,0),new Point(ActualWidth*i/4,ActualHeight)); drawingContext.DrawLine(grid,new Point(0,ActualHeight*i/4),new Point(ActualWidth,ActualHeight*i/4)); }
        _maximum = new[] { Histogram?.R ?? Analysis!.HistogramR, Histogram?.G ?? Analysis!.HistogramG, Histogram?.B ?? Analysis!.HistogramB }.SelectMany(x=>x).DefaultIfEmpty(1u).Max();
        if (Channel is "RGB" or "R") DrawChannel(drawingContext, Histogram?.R ?? Analysis!.HistogramR, Color.FromArgb(160, 236, 90, 80));
        if (Channel is "RGB" or "G") DrawChannel(drawingContext, Histogram?.G ?? Analysis!.HistogramG, Color.FromArgb(150, 90, 210, 120));
        if (Channel is "RGB" or "B") DrawChannel(drawingContext, Histogram?.B ?? Analysis!.HistogramB, Color.FromArgb(150, 80, 135, 240));
        if (Channel == "亮度" || (Channel == "RGB" && ShowLuma)) DrawChannel(drawingContext, Histogram?.Luma ?? Analysis!.HistogramLuma, Color.FromRgb(225, 225, 225), outline: true);
    }

    private void DrawChannel(DrawingContext context, IReadOnlyList<uint> bins, Color color, bool outline = false)
    {
        var max = Math.Max(1u, Channel == "RGB" && !outline ? _maximum : bins.Max()); var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new(0, RenderSize.Height), !outline, !outline);
            for (var index = 0; index < 256; index++) stream.LineTo(new(index / 255d * RenderSize.Width, RenderSize.Height - bins[index] / (double)max * RenderSize.Height), true, false);
            if (!outline) stream.LineTo(new(RenderSize.Width, RenderSize.Height), true, false);
        }
        geometry.Freeze(); var brush = new SolidColorBrush(color);
        context.DrawGeometry(outline ? null : brush, outline ? new Pen(brush, 1.5) : null, geometry);
    }
}
