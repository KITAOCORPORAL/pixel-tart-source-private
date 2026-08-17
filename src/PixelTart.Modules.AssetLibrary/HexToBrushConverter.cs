using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && ColorConverter.ConvertFromString(hex) is Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
