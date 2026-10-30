using System.Globalization;using System.Windows.Data;
namespace RAWSelectionAssistant.Views;
public sealed class CanvasCaptureIdleConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is not true;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
