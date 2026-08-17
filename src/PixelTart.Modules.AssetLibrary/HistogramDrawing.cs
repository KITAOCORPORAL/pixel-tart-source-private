using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed class HistogramDrawing : FrameworkElement
{
    public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(AssetVisualAnalysisResult), typeof(HistogramDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public AssetVisualAnalysisResult? Analysis { get => (AssetVisualAnalysisResult?)GetValue(AnalysisProperty); set => SetValue(AnalysisProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 16, 17)), null, new Rect(RenderSize));
        if (Analysis is null || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        DrawChannel(drawingContext, Analysis.HistogramR, Color.FromArgb(160, 236, 90, 80));
        DrawChannel(drawingContext, Analysis.HistogramG, Color.FromArgb(150, 90, 210, 120));
        DrawChannel(drawingContext, Analysis.HistogramB, Color.FromArgb(150, 80, 135, 240));
    }

    private void DrawChannel(DrawingContext context, IReadOnlyList<uint> bins, Color color)
    {
        var max = Math.Max(1u, bins.Max()); var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new(0, RenderSize.Height), true, true);
            for (var index = 0; index < 256; index++) stream.LineTo(new(index / 255d * RenderSize.Width, RenderSize.Height - bins[index] / (double)max * RenderSize.Height), true, false);
            stream.LineTo(new(RenderSize.Width, RenderSize.Height), true, false);
        }
        geometry.Freeze(); context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }
}
