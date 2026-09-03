using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetTagManagerView : UserControl
{
    public AssetTagManagerView() => InitializeComponent();

    private void OnManagedTagSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not AssetLibraryViewModel viewModel || sender is not ListBox list) return;
        viewModel.SetP3MergeSourceTags(list.SelectedItems.OfType<AssetTag>());
    }
}

public sealed class AssetTagManagerViewportHeightConverter : IValueConverter
{
    private const double ReservedPageHeight = 550d;
    private const double MinimumViewportHeight = 60d;
    private const double MaximumViewportHeight = 300d;

    public static double Calculate(double pageHeight) =>
        Math.Clamp(pageHeight - ReservedPageHeight, MinimumViewportHeight, MaximumViewportHeight);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double pageHeight && double.IsFinite(pageHeight)
            ? Calculate(pageHeight)
            : MinimumViewportHeight;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
