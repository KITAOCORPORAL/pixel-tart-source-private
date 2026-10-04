using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed class ToneZoneDrawing : FrameworkElement
{
    public static readonly DependencyProperty DistributionProperty = DependencyProperty.Register(nameof(Distribution), typeof(ElevenZoneDistribution), typeof(ToneZoneDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ElevenZoneDistribution? Distribution { get => (ElevenZoneDistribution?)GetValue(DistributionProperty); set => SetValue(DistributionProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        if (Distribution?.Ratios.Count != 11 || ActualWidth < 90) return;
        string[] labels = ["0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];
        var row = ActualHeight / 11;
        for (var index = 0; index < 11; index++)
        {
            var y = index * row; var ratio = Distribution[index];
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(45, 48, 49)), null, new Rect(26, y + 3, ActualWidth - 85, row - 6));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(67, 175, 161)), null, new Rect(26, y + 3, (ActualWidth - 85) * ratio, row - 6));
            Label(labels[index], 0); Label($"{ratio:P1}", ActualWidth - 53);
            void Label(string text, double x) => dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        }
    }
}
