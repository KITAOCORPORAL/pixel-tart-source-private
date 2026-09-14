using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// Metadata-only drag/drop for the inspiration tray and collections. It accepts the
/// existing gallery asset payload and a dedicated tray-entry payload; no source path
/// is placed on the data object and no source file is moved or copied.
/// </summary>
public static class InspirationDragDropBehavior
{
    internal const string AssetDataFormat = "PixelTart.AssetLibrary.AssetIds.v1";
    internal const string EntryDataFormat = "PixelTart.Inspiration.TrayEntryIds.v1";
    private static readonly Dictionary<UIElement, Point> StartPoints = [];

    public static readonly DependencyProperty SourceEntryIdProperty = DependencyProperty.RegisterAttached(
        "SourceEntryId", typeof(Guid?), typeof(InspirationDragDropBehavior), new PropertyMetadata(null, OnSourceChanged));
    public static readonly DependencyProperty SourceCollectionIdProperty = DependencyProperty.RegisterAttached(
        "SourceCollectionId", typeof(Guid?), typeof(InspirationDragDropBehavior), new PropertyMetadata(null));
    public static readonly DependencyProperty TargetKindProperty = DependencyProperty.RegisterAttached(
        "TargetKind", typeof(string), typeof(InspirationDragDropBehavior), new PropertyMetadata(null, OnTargetChanged));
    public static readonly DependencyProperty TargetCollectionIdProperty = DependencyProperty.RegisterAttached(
        "TargetCollectionId", typeof(Guid?), typeof(InspirationDragDropBehavior), new PropertyMetadata(null));
    public static readonly DependencyProperty TargetEntryIdProperty = DependencyProperty.RegisterAttached(
        "TargetEntryId", typeof(Guid?), typeof(InspirationDragDropBehavior), new PropertyMetadata(null));

    public static void SetSourceEntryId(DependencyObject target, Guid? value) => target.SetValue(SourceEntryIdProperty, value);
    public static Guid? GetSourceEntryId(DependencyObject target) => (Guid?)target.GetValue(SourceEntryIdProperty);
    public static void SetSourceCollectionId(DependencyObject target, Guid? value) => target.SetValue(SourceCollectionIdProperty, value);
    public static Guid? GetSourceCollectionId(DependencyObject target) => (Guid?)target.GetValue(SourceCollectionIdProperty);
    public static void SetTargetKind(DependencyObject target, string? value) => target.SetValue(TargetKindProperty, value);
    public static string? GetTargetKind(DependencyObject target) => (string?)target.GetValue(TargetKindProperty);
    public static void SetTargetCollectionId(DependencyObject target, Guid? value) => target.SetValue(TargetCollectionIdProperty, value);
    public static Guid? GetTargetCollectionId(DependencyObject target) => (Guid?)target.GetValue(TargetCollectionIdProperty);
    public static void SetTargetEntryId(DependencyObject target, Guid? value) => target.SetValue(TargetEntryIdProperty, value);
    public static Guid? GetTargetEntryId(DependencyObject target) => (Guid?)target.GetValue(TargetEntryIdProperty);

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not UIElement element) return;
        element.PreviewMouseLeftButtonDown -= SourceMouseDown;
        element.PreviewMouseMove -= SourceMouseMove;
        if (args.NewValue is Guid id && id != Guid.Empty)
        {
            element.PreviewMouseLeftButtonDown += SourceMouseDown;
            element.PreviewMouseMove += SourceMouseMove;
        }
    }

    private static void OnTargetChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not UIElement element) return;
        element.DragOver -= TargetDragOver;
        element.Drop -= TargetDrop;
        element.AllowDrop = args.NewValue is string kind && kind is "Tray" or "Collection" or "TrayOrder" or "CollectionOrder";
        if (!element.AllowDrop) return;
        element.DragOver += TargetDragOver;
        element.Drop += TargetDrop;
    }

    private static void SourceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element) StartPoints[element] = e.GetPosition(element);
    }

    private static void SourceMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element || e.LeftButton != MouseButtonState.Pressed ||
            !StartPoints.TryGetValue(element, out var start)) return;
        var current = e.GetPosition(element);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var entryId = GetSourceEntryId(element);
        if (entryId is null || entryId == Guid.Empty) return;

        var ids = ResolveSelectedEntryIds(element, entryId.Value);
        var payload = new InspirationEntryDragPayload(ids, GetSourceCollectionId(element));
        var data = new DataObject(EntryDataFormat, payload);
        try { _ = DragDrop.DoDragDrop(element, data, DragDropEffects.Link | DragDropEffects.Move); }
        finally { StartPoints.Remove(element); }
    }

    private static Guid[] ResolveSelectedEntryIds(FrameworkElement source, Guid fallback)
    {
        var list = FindVisualParent<ListBox>(source);
        if (list is null || list.SelectedItems.Count == 0 ||
            !list.SelectedItems.Cast<object>().OfType<InspirationTrayCardView>().Any(item => item.TrayEntryId == fallback))
            return [fallback];
        return list.SelectedItems.Cast<object>().OfType<InspirationTrayCardView>()
            .Select(item => item.TrayEntryId).Where(id => id != Guid.Empty).Distinct().ToArray();
    }

    private static void TargetDragOver(object sender, DragEventArgs e)
    {
        e.Effects = CanAccept(sender, e.Data) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private static async void TargetDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (sender is not FrameworkElement element || FindOwner(element) is not { } owner) return;
        var kind = GetTargetKind(element);
        var collectionId = GetTargetCollectionId(element);
        var targetEntryId = GetTargetEntryId(element);
        try
        {
            if (TryReadAssetIds(e.Data, out var assetIds))
            {
                if (kind == "Tray") await owner.AddAssetIdsToInspirationTrayAsync(assetIds);
                else if (kind is "Collection" or "CollectionOrder" && collectionId is Guid destination)
                    await owner.AddAssetIdsToCollectionAsync(destination, assetIds);
                else return;
                e.Effects = DragDropEffects.Link;
                return;
            }

            if (!TryReadEntryPayload(e.Data, out var payload)) return;
            if (kind is "Collection" or "CollectionOrder" && collectionId is Guid targetCollection)
            {
                if (kind == "CollectionOrder" && payload.SourceCollectionId == targetCollection && targetEntryId is Guid before)
                    await owner.ReorderCollectionEntriesAsync(targetCollection, payload.TrayEntryIds, before);
                else
                    await owner.MoveEntriesToCollectionAsync(targetCollection, payload.TrayEntryIds, payload.SourceCollectionId);
            }
            else if (kind == "TrayOrder" && payload.SourceCollectionId is null && targetEntryId is Guid trayBefore)
                await owner.ReorderTrayEntriesAsync(payload.TrayEntryIds, trayBefore);
            else return;
            e.Effects = DragDropEffects.Move;
        }
        catch (OperationCanceledException) { owner.SetStatusMessage("灵感拖放已取消。"); }
        catch (Exception exception) { owner.SetStatusMessage($"灵感拖放失败：{exception.Message}"); }
    }

    private static bool CanAccept(object sender, IDataObject data)
    {
        if (sender is not FrameworkElement element || FindOwner(element) is null) return false;
        var kind = GetTargetKind(element);
        var hasCollection = GetTargetCollectionId(element) is Guid id && id != Guid.Empty;
        if (TryReadAssetIds(data, out _)) return kind == "Tray" || hasCollection && kind is "Collection" or "CollectionOrder";
        if (!TryReadEntryPayload(data, out _)) return false;
        return kind == "TrayOrder" || hasCollection && kind is "Collection" or "CollectionOrder";
    }

    private static bool TryReadAssetIds(IDataObject data, out Guid[] ids)
    {
        ids = [];
        if (!data.GetDataPresent(AssetDataFormat)) return false;
        ids = data.GetData(AssetDataFormat) switch
        {
            Guid[] values => values.Where(id => id != Guid.Empty).Distinct().ToArray(),
            IEnumerable<Guid> values => values.Where(id => id != Guid.Empty).Distinct().ToArray(),
            _ => []
        };
        return ids.Length > 0;
    }

    private static bool TryReadEntryPayload(IDataObject data, out InspirationEntryDragPayload payload)
    {
        payload = new([], null);
        if (!data.GetDataPresent(EntryDataFormat) || data.GetData(EntryDataFormat) is not InspirationEntryDragPayload value) return false;
        var ids = value.TrayEntryIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return false;
        payload = value with { TrayEntryIds = ids };
        return true;
    }

    private static AssetLibraryViewModel? FindOwner(FrameworkElement element)
    {
        for (DependencyObject? current = element; current is not null; current = GetParent(current))
            if (current is FrameworkElement framework && framework.DataContext is AssetLibraryViewModel owner) return owner;
        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current) => current is Visual or System.Windows.Media.Media3D.Visual3D
        ? VisualTreeHelper.GetParent(current)
        : LogicalTreeHelper.GetParent(current);
}

public sealed record InspirationEntryDragPayload(IReadOnlyList<Guid> TrayEntryIds, Guid? SourceCollectionId);
