using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

public sealed class ColorStudioNodeTypeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ColorAdjustmentStackNode node && Enum.TryParse<ColorStudioNodeType>(parameter?.ToString(), out var type) && node.Type == type
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
