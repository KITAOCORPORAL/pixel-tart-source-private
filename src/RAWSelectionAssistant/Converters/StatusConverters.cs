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
            ShootBookingStatus status => status switch
            {
                ShootBookingStatus.Tentative => "待确定",
                ShootBookingStatus.Confirmed => "已确认",
                ShootBookingStatus.Preparing => "准备中",
                ShootBookingStatus.Shooting => "拍摄中",
                ShootBookingStatus.Completed => "已拍摄",
                ShootBookingStatus.AwaitingSelectionDelivery => "待发送选片",
                ShootBookingStatus.AwaitingSelection => "待选片",
                ShootBookingStatus.Selected => "已选片",
                ShootBookingStatus.AwaitingRetouch => "待精修",
                ShootBookingStatus.Retouched => "已精修",
                ShootBookingStatus.AwaitingDelivery => "待交付",
                ShootBookingStatus.Delivered => "已交付",
                ShootBookingStatus.Cancelled => "已取消",
                ShootBookingStatus.Postponed => "已延期",
                _ => "未知状态"
            },
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

public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key && Application.Current?.TryFindResource(key) is Geometry geometry
            ? geometry
            : Geometry.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
