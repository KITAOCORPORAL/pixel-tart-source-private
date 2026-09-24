using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView : UserControl
{
    private TetherReferenceModeViewModel? _editor;
    private bool _wasCompact;
    private double _lastResponsiveWidth = -1;
    private int _selectionAnchor = -1;
    private Point _filmstripDownPoint;
    private Point _nodeDragPoint;
    private ColorAdjustmentStackNode? _draggedNode;
    private ColorStudioZoomPanState _zoomPan => ImageViewport.State;
    private Point _panStart;
    private bool _panning;
    private bool _draggingDivider;
    private ListBoxItem? _dropIndicator;
    private Guid? _dropDestination;
    private bool _dropAfter;
    private bool _refreshingNodeSelection;
    private string[]? _nativeDragBeforeOrder;
    private string? _nativeDragBeforeHash;

    public ReferenceColorWorkspaceView()
    {
        InitializeComponent();
        _zoomPan.Changed += (_, _) => ZoomLabel.Text = $"{_zoomPan.Zoom:P0}";
        AdjustmentNodeList.DragOver += OnNodeDragOver;
        AdjustmentNodeList.DragLeave += (_, _) => ClearInsertion();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        Loaded += (_, _) => UpdateResponsiveLayout();
        LayoutUpdated += (_, _) =>
        {
            var width = GetAvailableWidth();
            if (Math.Abs(width - _lastResponsiveWidth) > .5) UpdateResponsiveLayout();
        };
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnFilmstripKeyDown;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnFilmstripMouseDown), true);
        PreviewKeyDown += OnSamplingKeyDown;
        PreviewKeyDown += OnColorHistoryKeyDown;
        AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent, new System.Windows.Controls.Primitives.DragStartedEventHandler(OnSliderDragStarted), true);
        AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent, new System.Windows.Controls.Primitives.DragCompletedEventHandler(OnSliderDragCompleted), true);
    }

    private void OnColorHistoryKeyDown(object sender, KeyEventArgs e)
    {
        if (_editor?.IsProMode != true || Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.Z && _editor.UndoAdjustmentCommand.CanExecute(null)) { _editor.UndoAdjustmentCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Y && _editor.RedoAdjustmentCommand.CanExecute(null)) { _editor.RedoAdjustmentCommand.Execute(null); e.Handled = true; }
    }
    private void OnSliderDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (_editor?.IsProMode == true && e.OriginalSource is System.Windows.Controls.Primitives.Thumb thumb && FindAncestor<Slider>(thumb) is not null) _editor.BeginEditTransaction();
    }
    private void OnSliderDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_editor?.IsProMode == true) _editor.CommitEditTransaction();
    }
    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is T found) return found;
        return null;
    }

    private void OnSamplingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _editor?.IsSampling == true) { _editor.CancelSamplingCommand.Execute(null); e.Handled = true; }
    }

    private void OnPreviewCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var point = e.GetPosition(PreviewCanvas);
        if (_editor?.EffectiveViewMode == "并排对比" && point.X >= PreviewCanvas.ActualWidth / 2) point.X -= (PreviewCanvas.ActualWidth + 1) / 2;
        _zoomPan.ZoomAbout(point, e.Delta > 0 ? 1.12 : 1 / 1.12); e.Handled = true;
    }
    private void OnPreviewCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning && !_draggingDivider) return;
        _panning = _draggingDivider = false; PreviewCanvas.ReleaseMouseCapture(); e.Handled = true;
    }
    private void OnPreviewCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingDivider && _editor is not null) { _editor.SplitPosition = e.GetPosition(PreviewCanvas).X / Math.Max(1, PreviewCanvas.ActualWidth); e.Handled = true; return; }
        if (!_panning || (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)) return;
        var current = e.GetPosition(PreviewCanvas); _zoomPan.PanBy(current - _panStart); _panStart = current; e.Handled = true;
    }
    private void OnPreviewCanvasMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_editor?.IsSampling == true) return;
        _zoomPan.Fit(); e.Handled = true;
    }
    private void OnPreviewLostCapture(object sender, MouseEventArgs e) => _panning = _draggingDivider = false;
    private void OnFitClick(object sender, RoutedEventArgs e) => _zoomPan.Fit();
    private void OnActualSizeClick(object sender, RoutedEventArgs e) => _zoomPan.SetZoom(1);
    private void OnZoomInClick(object sender, RoutedEventArgs e) => _zoomPan.SetZoom(_zoomPan.Zoom * 1.25);
    private void OnZoomOutClick(object sender, RoutedEventArgs e) => _zoomPan.SetZoom(_zoomPan.Zoom / 1.25);
    private void OnNodeEnableClick(object sender, RoutedEventArgs e)
    {
        if (_editor is null || sender is not CheckBox { DataContext: ColorAdjustmentStackNode node }) return;
        _editor.SelectedAdjustmentNode = node; _editor.ToggleAdjustmentNodeCommand.Execute(null); e.Handled = true;
    }
    private System.Windows.Input.ICommand? NodeCommand(string? key) => key switch
    {
        "rename" => _editor?.StartNodeRenameCommand, "duplicate" => _editor?.DuplicateAdjustmentNodeCommand,
        "reset" => _editor?.ResetAdjustmentNodeCommand, "up" => _editor?.MoveAdjustmentNodeUpCommand,
        "down" => _editor?.MoveAdjustmentNodeDownCommand, "delete" => _editor?.DeleteAdjustmentNodeCommand, _ => null
    };
    private void OnNodeOverflowClick(object sender, RoutedEventArgs e)
    {
        if (_editor is null || sender is not Button { DataContext: ColorAdjustmentStackNode node, ContextMenu: { } menu } button) return;
        _editor.SelectedAdjustmentNode = node;
        foreach (var item in menu.Items.OfType<MenuItem>()) item.IsEnabled = NodeCommand(item.Tag as string)?.CanExecute(null) == true;
        menu.PlacementTarget = button; menu.IsOpen = true; e.Handled = true;
    }
    private void OnNodeMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && NodeCommand(item.Tag as string) is { } command && command.CanExecute(null)) command.Execute(null);
    }
    private void OnNodeDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && (FindAncestor<Button>(source) is not null || FindAncestor<CheckBox>(source) is not null)) { _draggedNode = null; return; }
        _nodeDragPoint = e.GetPosition(AdjustmentNodeList);
        _draggedNode = (ItemsControl.ContainerFromElement(AdjustmentNodeList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as ColorAdjustmentStackNode;
        if (_draggedNode is not null && ColorStudioAcceptanceFixture.Requested)
        {
            _nativeDragBeforeOrder = _editor?.AdjustmentNodes.Select(node => node.Name).ToArray();
            _nativeDragBeforeHash = ColorStudioAcceptanceFixture.HashImage(_editor?.MatchedImage);
        }
    }
    private void OnNodeDragMove(object sender, MouseEventArgs e)
    {
        if (_draggedNode is null || e.LeftButton != MouseButtonState.Pressed ||
            (e.GetPosition(AdjustmentNodeList) - _nodeDragPoint).Length < SystemParameters.MinimumVerticalDragDistance) return;
        var node = _draggedNode; _draggedNode = null;
        try { DragDrop.DoDragDrop(AdjustmentNodeList, node, DragDropEffects.Move); }
        finally { ClearInsertion(); }
    }
    private void ClearInsertion()
    {
        if (_dropIndicator is not null) { _dropIndicator.ClearValue(Control.BorderBrushProperty); _dropIndicator.ClearValue(Control.BorderThicknessProperty); }
        _dropIndicator = null; _dropDestination = null;
    }
    private void OnNodeDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ColorAdjustmentStackNode))) return;
        var item = ItemsControl.ContainerFromElement(AdjustmentNodeList, e.OriginalSource as DependencyObject) as ListBoxItem;
        ClearInsertion();
        if (item?.DataContext is not ColorAdjustmentStackNode node) return;
        _dropIndicator = item; _dropDestination = node.Id; _dropAfter = e.GetPosition(item).Y >= item.ActualHeight / 2;
        item.SetResourceReference(Control.BorderBrushProperty, "AccentValueBrush");
        item.BorderThickness = _dropAfter ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
        e.Effects = DragDropEffects.Move; e.Handled = true;
    }
    private void OnNodeDrop(object sender, DragEventArgs e)
    {
        if (_editor is null || e.Data.GetData(typeof(ColorAdjustmentStackNode)) is not ColorAdjustmentStackNode source) return;
        var destination = (ItemsControl.ContainerFromElement(AdjustmentNodeList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as ColorAdjustmentStackNode;
        if (destination is null) return;
        _editor.InsertAdjustmentNode(source.Id, _dropDestination ?? destination.Id, _dropAfter); ClearInsertion(); e.Handled = true;
        if (ColorStudioAcceptanceFixture.Requested)
            _ = ColorStudioAcceptanceFixture.RecordNativeNodeDragAsync(_editor, _nativeDragBeforeOrder ?? [], _nativeDragBeforeHash, source.Name, destination.Name, _dropAfter);
        _nativeDragBeforeOrder = null; _nativeDragBeforeHash = null;
    }
    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (_editor is null) return;
        if (e.Key == Key.Enter) { _editor.CommitNodeRenameCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Escape) { _editor.CancelNodeRenameCommand.Execute(null); e.Handled = true; }
    }

    private void OnPreviewCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && _editor?.IsSampling != true) { _zoomPan.Fit(); e.Handled = true; return; }
        if ((e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)) || e.ChangedButton == MouseButton.Middle)
        { _panning = true; _panStart = e.GetPosition(PreviewCanvas); PreviewCanvas.CaptureMouse(); e.Handled = true; return; }
        if (_editor?.IsSampling != true)
        {
            if (_editor?.EffectiveViewMode == "左右对比" && Math.Abs(e.GetPosition(PreviewCanvas).X - PreviewCanvas.ActualWidth * _editor.SplitPosition) < 10)
            { _draggingDivider = true; PreviewCanvas.CaptureMouse(); e.Handled = true; }
            return;
        }
        var mapped = ColorStudioSampleMapping.Map(e.GetPosition(PreviewCanvas), new Size(PreviewCanvas.ActualWidth, PreviewCanvas.ActualHeight),
            _editor.EffectiveViewMode, _editor.SplitPosition, _editor.SourceImage, _editor.MatchedImage, _zoomPan);
        if (mapped is not { } sample) return;
        var (image, x, y) = sample;
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        var bgra = HistogramService.EnsureBgra32(image); bgra.CopyPixels(pixels, image.PixelWidth * 4, 0);
        var index = (y * image.PixelWidth + x) * 4;
        var mode = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? "减少取样" : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? "增加取样" : _editor.SampleMode;
        _editor.SampleMode = mode;
        _editor.CompleteDisplayedSample(new VisualRgb24(pixels[index + 2], pixels[index + 1], pixels[index]));
        e.Handled = true;
    }

    private void OnFilmstripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        if (e.OriginalSource is not DependencyObject source) return;
        for (DependencyObject? ancestor = source; ancestor is not null;
             ancestor = ancestor is Visual or Visual3D ? VisualTreeHelper.GetParent(ancestor) : LogicalTreeHelper.GetParent(ancestor))
            if (ancestor is PixelTartRatingControl) return;
        var item = ItemsControl.ContainerFromElement(FindFilmstrip(), source) as ListBoxItem;
        if (item?.DataContext is not ReferenceTargetItem target) return;
        var index = workspace.Targets.IndexOf(target); _filmstripDownPoint = e.GetPosition(this);
        var modifiers = Keyboard.Modifiers;
        _selectionAnchor = workspace.SelectFilmstripTarget(index, _selectionAnchor,
            modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Control));
        workspace.ActivateTargetCommand.Execute(target); e.Handled = true;
    }
    private ListBox? FindFilmstrip() => FindName("Filmstrip") as ListBox;

    private void OnFilmstripKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || Keyboard.FocusedElement is not DependencyObject focus || FindAncestor<ListBoxItem>(focus) is not { } item || ItemsControl.ItemsControlFromItemContainer(item) != FindFilmstrip()) return;
        if (_editor is null || DataContext is not ReferenceColorWorkspaceViewModel workspace || workspace.Targets.Count == 0) return;
        var index = workspace.ActiveTarget is null ? 0 : workspace.Targets.IndexOf(workspace.ActiveTarget);
        if (e.Key == Key.Escape) { foreach (var target in workspace.Targets) target.IsSelected = false; if (workspace.ActiveTarget is not null) workspace.ActiveTarget.IsSelected = true; e.Handled = true; return; }
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { foreach (var target in workspace.Targets) target.IsSelected = true; e.Handled = true; return; }
        var delta = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (delta == 0) return;
        var next = Math.Clamp(index + delta, 0, workspace.Targets.Count - 1); var targetAt = workspace.Targets[next];
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Shift) || !modifiers.HasFlag(ModifierKeys.Control))
            _selectionAnchor = workspace.SelectFilmstripTarget(next, _selectionAnchor,
                modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Control));
        workspace.ActivateTargetCommand.Execute(targetAt); e.Handled = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (_editor is not null) _editor.PropertyChanged -= EditorOnPropertyChanged;
        _editor = (args.NewValue as ReferenceColorWorkspaceViewModel)?.Editor;
        if (_editor is not null) _editor.PropertyChanged += EditorOnPropertyChanged;
        UpdateResponsiveLayout();
    }

    private void EditorOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_refreshingNodeSelection && args.PropertyName is nameof(TetherReferenceModeViewModel.AdjustmentNodes) or nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode))
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, () =>
            {
                if (_editor?.SelectedAdjustmentNode is not { } selected) return;
                var index = _editor.AdjustmentNodes.ToList().FindIndex(n => n.Id == selected.Id);
                if (index < 0 || AdjustmentNodeList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem { IsSelected: true }) return;
                _refreshingNodeSelection = true;
                try
                {
                    AdjustmentNodeList.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, -1);
                    AdjustmentNodeList.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, index);
                }
                finally { _refreshingNodeSelection = false; }
            });
        if (args.PropertyName == nameof(TetherReferenceModeViewModel.IsSampling)) PreviewCanvas.Cursor = _editor?.IsSampling == true ? Cursors.Cross : null;
        if (args.PropertyName is nameof(TetherReferenceModeViewModel.FocusView) or nameof(TetherReferenceModeViewModel.ContextRailOpen))
            UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        if (LeftColumn is null || RightColumn is null || CenterColumn is null) return;
        var availableWidth = GetAvailableWidth();
        _lastResponsiveWidth = availableWidth;
        var focus = _editor?.FocusView == true;
        var compact = availableWidth is > 0 and < 1440;
        var narrow = availableWidth is > 0 and < 740;

        if (compact && !_wasCompact) _editor?.SetResponsiveContext(true);
        _wasCompact = compact;

        LeftColumn.MinWidth = focus || narrow ? 0 : compact ? 300 : 240;
        RightColumn.MinWidth = focus || compact ? 0 : 224;
        LeftColumn.Width = focus || narrow ? new GridLength(0) : new GridLength(320);
        CenterColumn.MinWidth = compact ? 240 : 520;
        CenterColumn.Width = focus || compact ? new GridLength(1, GridUnitType.Star) : new GridLength(.63, GridUnitType.Star);
        RightColumn.Width = focus || compact ? new GridLength(0) : new GridLength(.18, GridUnitType.Star);
        if (compact)
        {
            Grid.SetColumn(RightRail, 1);
            Panel.SetZIndex(RightRail, 20);
            RightRail.Width = Math.Min(340, Math.Max(280, ActualWidth * .3));
            RightRail.HorizontalAlignment = HorizontalAlignment.Right;
            RightRail.Background = (System.Windows.Media.Brush)FindResource("SurfacePrimaryBrush");
            RightRail.Padding = new Thickness(14);
        }
        else
        {
            Grid.SetColumn(RightRail, 2);
            Panel.SetZIndex(RightRail, 0);
            RightRail.Width = double.NaN;
            RightRail.HorizontalAlignment = HorizontalAlignment.Stretch;
            RightRail.Background = null;
            RightRail.Padding = new Thickness(0);
        }
        if (compact)
        {
            Grid.SetRow(HeaderActions, 1);
            Grid.SetColumn(HeaderActions, 0);
            Grid.SetColumnSpan(HeaderActions, 2);
            HeaderActions.Margin = new Thickness(0, 8, 0, 0);
            HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumnSpan(SourceBars, 2);
        }
        else
        {
            Grid.SetRow(HeaderActions, 0);
            Grid.SetColumn(HeaderActions, 1);
            Grid.SetColumnSpan(HeaderActions, 1);
            HeaderActions.Margin = new Thickness(0);
            HeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumnSpan(SourceBars, 1);
        }
        ContextRailButton.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
    }

    private double GetAvailableWidth()
    {
        var width = ActualWidth;
        if (Window.GetWindow(this)?.Content is FrameworkElement root && root.ActualWidth > 0)
        {
            try
            {
                var origin = TranslatePoint(new Point(0, 0), root);
                width = Math.Min(width, Math.Max(0, root.ActualWidth - origin.X));
            }
            catch (InvalidOperationException) { }
        }
        return width;
    }
}
