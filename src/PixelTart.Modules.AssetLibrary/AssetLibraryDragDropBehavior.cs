using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public static class AssetLibraryDragDropBehavior
{
    private const string DataFormat = "PixelTart.AssetLibrary.AssetIds.v1";
    private static readonly Dictionary<UIElement, Point> StartPoints = [];

    public static readonly DependencyProperty IsAssetSourceProperty = DependencyProperty.RegisterAttached(
        "IsAssetSource", typeof(bool), typeof(AssetLibraryDragDropBehavior), new PropertyMetadata(false, OnIsAssetSourceChanged));
    public static readonly DependencyProperty DropTargetProperty = DependencyProperty.RegisterAttached(
        "DropTarget", typeof(object), typeof(AssetLibraryDragDropBehavior), new PropertyMetadata(null, OnDropTargetChanged));

    public static void SetIsAssetSource(DependencyObject target, bool value) => target.SetValue(IsAssetSourceProperty, value);
    public static bool GetIsAssetSource(DependencyObject target) => (bool)target.GetValue(IsAssetSourceProperty);
    public static void SetDropTarget(DependencyObject target, object? value) => target.SetValue(DropTargetProperty, value);
    public static object? GetDropTarget(DependencyObject target) => target.GetValue(DropTargetProperty);

    private static void OnIsAssetSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not UIElement element) return;
        element.PreviewMouseLeftButtonDown -= OnSourceMouseDown;
        element.PreviewMouseMove -= OnSourceMouseMove;
        if (args.NewValue is true)
        {
            element.PreviewMouseLeftButtonDown += OnSourceMouseDown;
            element.PreviewMouseMove += OnSourceMouseMove;
        }
    }

    private static void OnDropTargetChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not UIElement element) return;
        element.DragEnter -= OnDragEnter;
        element.DragOver -= OnDragOver;
        element.Drop -= OnDrop;
        element.AllowDrop = IsWritableTargetShape(args.NewValue);
        if (element.AllowDrop)
        {
            element.DragEnter += OnDragEnter;
            element.DragOver += OnDragOver;
            element.Drop += OnDrop;
        }
    }

    private static void OnSourceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element) StartPoints[element] = e.GetPosition(element);
    }

    private static void OnSourceMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element || e.LeftButton != MouseButtonState.Pressed || !StartPoints.TryGetValue(element, out var start)) return;
        // A drag that starts on empty grid space belongs to the marquee selector.
        // Only an actual card may initiate the metadata-only asset drag.
        if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) is null) return;
        var current = e.GetPosition(element);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (element.DataContext is not AssetLibraryViewModel viewModel) return;
        var ids = viewModel.GetDragAssetIds();
        if (ids.Count == 0) return;
        var data = new DataObject();
        data.SetData(DataFormat, ids.ToArray());
        try { _ = DragDrop.DoDragDrop(element, data, DragDropEffects.Link); }
        finally { StartPoints.Remove(element); }
    }

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static async void OnDragEnter(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target, out var payload))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        if (!viewModel.CanDropPayload(payload) || !viewModel.CanDropOn(target))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            viewModel.ReportDropRejected(target);
            return;
        }
        e.Effects = DragDropEffects.Link;
        e.Handled = true;
        try { await viewModel.PreviewDropAsync(target); }
        catch (OperationCanceledException) { viewModel.ReportDropFailure("预览", new OperationCanceledException()); }
        catch (Exception exception) { viewModel.ReportDropFailure("预览", exception); }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target, out var payload))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = viewModel.CanDropPayload(payload) && viewModel.CanDropOn(target)
            ? DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static async void OnDrop(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target, out var payload)
            || !viewModel.CanDropPayload(payload)
            || !viewModel.CanDropOn(target))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            if (TryResolve(sender, e, out var rejectedViewModel, out var rejectedTarget, out _))
                rejectedViewModel.ReportDropRejected(rejectedTarget);
            return;
        }
        e.Effects = DragDropEffects.Link;
        e.Handled = true;
        try { await viewModel.ExecuteDropAsync(target); }
        catch (OperationCanceledException) { viewModel.ReportDropFailure("执行", new OperationCanceledException()); }
        catch (Exception exception) { viewModel.ReportDropFailure("执行", exception); }
    }

    private static bool TryResolve(
        object sender,
        DragEventArgs e,
        out AssetLibraryViewModel viewModel,
        out AssetLibraryDropTarget target,
        out IReadOnlyList<Guid> payload)
    {
        viewModel = null!;
        target = null!;
        payload = [];
        if (sender is not FrameworkElement element || !TryReadPayload(e.Data, out payload) || GetDropTarget(element) is not AssetLibraryDropTarget resolved)
            return false;
        if (!IsWritableTargetShape(resolved)) return false;
        target = resolved;
        viewModel = (target.Kind switch
        {
            AssetLibraryDropTargetKind.Folder when element.DataContext is AssetLibraryFolderNodeView folder
                && folder.FolderId == target.TargetId && folder.IsArchived == target.IsArchived => folder.Owner,
            AssetLibraryDropTargetKind.Tag when element.DataContext is AssetLibraryTagNodeView tag
                && tag.Tag.TagId == target.TargetId && tag.Tag.IsArchived == target.IsArchived => tag.Owner,
            _ => FindOwner(element)
        })!;
        return viewModel is not null;
    }

    private static bool TryReadPayload(IDataObject data, out IReadOnlyList<Guid> payload)
    {
        payload = [];
        try
        {
            if (!data.GetDataPresent(DataFormat)) return false;
            var raw = data.GetData(DataFormat);
            var ids = raw switch
            {
                Guid[] array => array,
                IReadOnlyList<Guid> list => list,
                IEnumerable<Guid> enumerable => enumerable.ToArray(),
                _ => []
            };
            payload = ids.Where(id => id != Guid.Empty).Distinct().ToArray();
            return payload.Count > 0;
        }
        catch (Exception) { payload = []; return false; }
    }

    private static bool IsWritableTargetShape(object? value)
    {
        if (value is not AssetLibraryDropTarget target || target.IsArchived) return false;
        return target.Kind switch
        {
            AssetLibraryDropTargetKind.Folder or AssetLibraryDropTargetKind.Tag => target.TargetId is Guid id && id != Guid.Empty,
            AssetLibraryDropTargetKind.RemoveFromCurrent => target.TargetId is null,
            _ => false
        };
    }

    private static AssetLibraryViewModel? FindOwner(FrameworkElement element)
    {
        for (FrameworkElement? current = element; current is not null; current = current.Parent as FrameworkElement)
            if (current.DataContext is AssetLibraryViewModel owner) return owner;
        return Application.Current?.Windows.OfType<Window>().Select(window => window.DataContext).OfType<AssetLibraryViewModel>().FirstOrDefault();
    }
}
