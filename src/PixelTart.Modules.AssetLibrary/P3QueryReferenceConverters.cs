using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed class P3ReferenceOptionsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not AssetQueryField field) return Array.Empty<P3QueryValueOption>();
        return field switch
        {
            AssetQueryField.Folder => AsOptions(values[1]),
            AssetQueryField.Tag => AsOptions(values[2]),
            _ => Array.Empty<P3QueryValueOption>()
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IReadOnlyList<P3QueryValueOption> AsOptions(object value) =>
        value is IEnumerable source
            ? source.OfType<P3QueryValueOption>().ToArray()
            : Array.Empty<P3QueryValueOption>();
}

public sealed class P3ReferenceLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var value = values.ElementAtOrDefault(0) as string ?? string.Empty;
        if (values.ElementAtOrDefault(1) is not AssetQueryField field) return value;
        var source = field == AssetQueryField.Folder
            ? values.ElementAtOrDefault(2)
            : values.ElementAtOrDefault(3);
        var match = source is IEnumerable options
            ? options.OfType<P3QueryValueOption>().FirstOrDefault(option =>
                string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            : null;
        return match?.Label ?? $"失效引用：{value}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
