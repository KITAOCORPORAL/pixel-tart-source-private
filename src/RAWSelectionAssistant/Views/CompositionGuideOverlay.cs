using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public sealed class CompositionGuideOverlay : FrameworkElement
{
    public static readonly DependencyProperty GuideModeProperty = DependencyProperty.Register(
        nameof(GuideMode), typeof(TetherGuideMode), typeof(CompositionGuideOverlay), new FrameworkPropertyMetadata(TetherGuideMode.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GuideBrushProperty = DependencyProperty.Register(
        nameof(GuideBrush), typeof(Brush), typeof(CompositionGuideOverlay), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public TetherGuideMode GuideMode { get => (TetherGuideMode)GetValue(GuideModeProperty); set => SetValue(GuideModeProperty, value); }
    public Brush GuideBrush { get => (Brush)GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (GuideMode == TetherGuideMode.None || ActualWidth <= 0 || ActualHeight <= 0) return;
        var pen = new Pen(GuideBrush, 1) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        switch (GuideMode)
        {
            case TetherGuideMode.Thirds:
                DrawLine(drawing, pen, ActualWidth / 3, 0, ActualWidth / 3, ActualHeight);
                DrawLine(drawing, pen, ActualWidth * 2 / 3, 0, ActualWidth * 2 / 3, ActualHeight);
                DrawLine(drawing, pen, 0, ActualHeight / 3, ActualWidth, ActualHeight / 3);
                DrawLine(drawing, pen, 0, ActualHeight * 2 / 3, ActualWidth, ActualHeight * 2 / 3);
                break;
            case TetherGuideMode.CenterCross:
                DrawLine(drawing, pen, ActualWidth / 2, 0, ActualWidth / 2, ActualHeight);
                DrawLine(drawing, pen, 0, ActualHeight / 2, ActualWidth, ActualHeight / 2);
                break;
            case TetherGuideMode.SafeArea:
                drawing.DrawRectangle(null, pen, new Rect(ActualWidth * .05, ActualHeight * .05, ActualWidth * .9, ActualHeight * .9));
                drawing.DrawRectangle(null, pen, new Rect(ActualWidth * .1, ActualHeight * .1, ActualWidth * .8, ActualHeight * .8));
                break;
            default:
                DrawRatio(drawing, pen, Ratio(GuideMode));
                break;
        }
    }

    private void DrawRatio(DrawingContext drawing, Pen pen, double ratio)
    {
        if (ratio <= 0) return;
        var width = ActualWidth;
        var height = width / ratio;
        if (height > ActualHeight) { height = ActualHeight; width = height * ratio; }
        drawing.DrawRectangle(null, pen, new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
    }

    private static double Ratio(TetherGuideMode mode) => mode switch
    {
        TetherGuideMode.Square => 1,
        TetherGuideMode.Ratio4x5 => 4d / 5,
        TetherGuideMode.Ratio3x4 => 3d / 4,
        TetherGuideMode.Ratio2x3 => 2d / 3,
        TetherGuideMode.Ratio16x9 => 16d / 9,
        TetherGuideMode.Ratio9x16 => 9d / 16,
        _ => 0
    };

    private static void DrawLine(DrawingContext drawing, Pen pen, double x1, double y1, double x2, double y2) => drawing.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
}
