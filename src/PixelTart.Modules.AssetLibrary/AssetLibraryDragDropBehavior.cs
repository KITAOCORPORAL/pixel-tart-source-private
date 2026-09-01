using System.Windows;
using System.Windows.Input;

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
        element.AllowDrop = args.NewValue is AssetLibraryDropTarget;
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
        var current = e.GetPosition(element);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (element.DataContext is not AssetLibraryViewModel viewModel) return;
        var ids = viewModel.GetDragAssetIds();
        if (ids.Count == 0) return;
        var data = new DataObject();
        data.SetData(DataFormat, ids.ToArray());
        StartPoints.Remove(element);
        _ = DragDrop.DoDragDrop(element, data, DragDropEffects.Link);
    }

    private static async void OnDragEnter(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target)) return;
        e.Effects = viewModel.CanDropOn(target) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
        await viewModel.PreviewDropAsync(target);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target)) return;
        e.Effects = viewModel.CanDropOn(target) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private static async void OnDrop(object sender, DragEventArgs e)
    {
        if (!TryResolve(sender, e, out var viewModel, out var target) || !viewModel.CanDropOn(target)) return;
        e.Effects = DragDropEffects.Link;
        e.Handled = true;
        await viewModel.ExecuteDropAsync(target);
    }

    private static bool TryResolve(object sender, DragEventArgs e, out AssetLibraryViewModel viewModel, out AssetLibraryDropTarget target)
    {
        viewModel = null!;
        target = null!;
        if (!e.Data.GetDataPresent(DataFormat) || sender is not FrameworkElement element || GetDropTarget(element) is not AssetLibraryDropTarget resolved) return false;
        target = resolved;
        viewModel = (target.Kind switch
        {
            AssetLibraryDropTargetKind.Folder when element.DataContext is AssetLibraryFolderNodeView folder => folder.Owner,
            AssetLibraryDropTargetKind.Tag when element.DataContext is AssetLibraryTagNodeView tag => tag.Owner,
            _ => FindOwner(element)
        })!;
        return viewModel is not null;
    }

    private static AssetLibraryViewModel? FindOwner(FrameworkElement element)
    {
        for (FrameworkElement? current = element; current is not null; current = current.Parent as FrameworkElement)
            if (current.DataContext is AssetLibraryViewModel owner) return owner;
        return Application.Current?.Windows.OfType<Window>().Select(window => window.DataContext).OfType<AssetLibraryViewModel>().FirstOrDefault();
    }
}
