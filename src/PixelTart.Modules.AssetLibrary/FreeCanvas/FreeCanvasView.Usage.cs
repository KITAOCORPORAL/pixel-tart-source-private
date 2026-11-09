using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary.FreeCanvas;

public sealed partial class FreeCanvasView
{
    private static readonly DependencyPropertyKey UsedMaterialKeysPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(UsedMaterialKeys), typeof(IReadOnlySet<(Guid LibraryId, Guid AssetId)>), typeof(FreeCanvasView),
        new PropertyMetadata(new HashSet<(Guid LibraryId, Guid AssetId)>()));
    public static readonly DependencyProperty UsedMaterialKeysProperty = UsedMaterialKeysPropertyKey.DependencyProperty;
    public IReadOnlySet<(Guid LibraryId, Guid AssetId)> UsedMaterialKeys =>
        (IReadOnlySet<(Guid LibraryId, Guid AssetId)>)GetValue(UsedMaterialKeysProperty);
    private CanvasDocument? _usageDocument;

    private void UpdateUsedMaterials()
    {
        if (ReferenceEquals(_usageDocument, Editor.Document)) return;
        _usageDocument = Editor.Document;
        var keys = Editor.Document.Objects
            .Where(item => item.LibraryId != Guid.Empty && item.AssetId is Guid id && id != Guid.Empty)
            .Select(item => (item.LibraryId, item.AssetId!.Value)).ToHashSet();
        // Selection and transforms do not rebuild the shelf; document edits only replace a changed key set.
        if (!UsedMaterialKeys.SetEquals(keys)) SetValue(UsedMaterialKeysPropertyKey, keys);
    }
}

public sealed class CanvasMaterialUsageVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is CanvasObject item && item.LibraryId != Guid.Empty &&
        item.AssetId is Guid id && id != Guid.Empty &&
        values[1] is IReadOnlySet<(Guid LibraryId, Guid AssetId)> keys && keys.Contains((item.LibraryId, id))
            ? Visibility.Visible : Visibility.Collapsed;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Usage is derived from the current canvas document.");
}
