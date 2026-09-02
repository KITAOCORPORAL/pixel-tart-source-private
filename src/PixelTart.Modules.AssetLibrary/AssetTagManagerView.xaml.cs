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
