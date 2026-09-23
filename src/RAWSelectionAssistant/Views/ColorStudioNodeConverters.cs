using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

public sealed class ColorStudioNodeStrengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ColorAdjustmentStackNode node) return "";
        var key = node.Type switch
        {
            ColorStudioNodeType.ReferenceMatch => "match_strength",
            ColorStudioNodeType.ColorRange => "strength",
            ColorStudioNodeType.TransitionBlend => "amount",
            _ => "profile_amount"
        };
        var fallback = node.Type == ColorStudioNodeType.TransitionBlend ? .25 : 100d;
        var amount = node.NumericParameters.TryGetValue(key, out var number) ? number :
            node.Type == ColorStudioNodeType.Film ? node.FilmSettings?.ProfileAmount ?? fallback : fallback;
        return $"{amount * (node.Type == ColorStudioNodeType.TransitionBlend ? 100 : 1):0}%";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ColorStudioSampleBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is VisualRgb24 rgb ? new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B)) : Brushes.Transparent;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
