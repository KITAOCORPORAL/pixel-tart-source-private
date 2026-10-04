using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed class ToneZoneDrawing : FrameworkElement
{
    public static readonly DependencyProperty DistributionProperty = DependencyProperty.Register(nameof(Distribution), typeof(ElevenZoneDistribution), typeof(ToneZoneDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ElevenZoneDistribution? Distribution { get => (ElevenZoneDistribution?)GetValue(DistributionProperty); set => SetValue(DistributionProperty, value); }
    public event EventHandler<int>? ZoneHovered;
    private int _hovered = -1;
    public ToneZoneDrawing()
    {
        MouseMove += (_, e) => { var zone = Math.Clamp((int)(e.GetPosition(this).Y / Math.Max(1, ActualHeight) * 11), 0, 10); if (zone != _hovered) { _hovered = zone; ZoneHovered?.Invoke(this, zone); InvalidateVisual(); } };
        MouseLeave += (_, _) => { _hovered = -1; ZoneHovered?.Invoke(this, -1); InvalidateVisual(); };
        ToolTip = "悬停影调色块：临时显示对应线性亮度区间；离开或 Esc 清除";
    }
    public void ClearHover() { _hovered = -1; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        if (Distribution?.Ratios.Count != 11 || ActualWidth < 130) return;
        string[] labels = ["0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var row = ActualHeight / 11;
        for (var index = 0; index < 11; index++)
        {
            var y = index * row; var ratio = Distribution[index];
            var linear = (index + .5) / 11; var encoded = linear <= .0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - .055;
            var gray = (byte)Math.Round(encoded * 255);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(gray, gray, gray)), new Pen(index == _hovered ? Brushes.Turquoise : Brushes.Gray, 1), new Rect(0, y + 2, 22, Math.Max(2, row - 4)));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(45, 48, 49)), null, new Rect(49, y + 3, ActualWidth - 108, row - 6));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(67, 175, 161)), null, new Rect(49, y + 3, (ActualWidth - 108) * ratio, row - 6));
            Label(labels[index], 26); Label($"{ratio:P1}", ActualWidth - 53);
            void Label(string text, double x) => dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        }
    }
}
