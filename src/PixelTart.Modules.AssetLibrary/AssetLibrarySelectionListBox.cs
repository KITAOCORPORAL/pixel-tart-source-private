using System.Collections;
using System.Windows.Controls;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>Use WPF's selection transaction when restoring a whole logical selection.</summary>
public sealed class AssetLibrarySelectionListBox : ListBox
{
    public void ReplaceSelection(IEnumerable items) => SetSelectedItems(items);
}
