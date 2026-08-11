using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RAWSelectionAssistant.Views;

public sealed class EnumEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
