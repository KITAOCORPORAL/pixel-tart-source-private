using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Converters;

public sealed class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            MatchStatus status => status.ToChinese(),
            MediaOverallStatus status => status.ToChinese(),
            _ => string.Empty
        };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Resource(value switch
    {
        MatchStatus.Matched or MatchStatus.ManuallyConfirmed or MatchStatus.Copied => "SuccessBrush",
        MatchStatus.Conflict or MatchStatus.WaitingManualConfirmation => "WarningBrush",
        MatchStatus.NotFound or MatchStatus.CopyFailed => "DangerBrush",
        MatchStatus.Skipped => "TextSecondaryBrush",
        MediaOverallStatus.CompleteMatched or MediaOverallStatus.FullyCopied => "SuccessBrush",
        MediaOverallStatus.PartialMatched or MediaOverallStatus.PartiallyCopied or MediaOverallStatus.Conflict or MediaOverallStatus.WaitingConfirmation => "WarningBrush",
        MediaOverallStatus.NotFound or MediaOverallStatus.CopyFailed => "DangerBrush",
        _ => "TextSecondaryBrush"
    });
    private static Brush Resource(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class PositiveIntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 1 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ZeroIntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return string.Empty;
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
