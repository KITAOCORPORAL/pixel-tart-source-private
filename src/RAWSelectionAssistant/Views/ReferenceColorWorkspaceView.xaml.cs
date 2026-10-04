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
using RAWSelectionAssistant.Core.Services.Presets;
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
    private bool _inspectingImageColor;
    private ColorSpace3DViewport? _expandedCloud;
    private bool _draggingDivider;
    private ListBoxItem? _dropIndicator;
    private Guid? _dropDestination;
    private bool _dropAfter;
    private bool _refreshingNodeSelection;
    private string[]? _nativeDragBeforeOrder;
    private string? _nativeDragBeforeHash;
    private bool _nativeDragActive;

    internal object ReadNativeDragEvidence() => new
    {
        Active = _nativeDragActive,
        Pending = _draggedNode is not null,
        Destination = _dropDestination,
        After = _dropAfter,
        ThresholdDip = SystemParameters.MinimumVerticalDragDistance,
        Rows = AdjustmentNodeList.Items.Cast<ColorAdjustmentStackNode>().Select(node =>
        {
            var row = AdjustmentNodeList.ItemContainerGenerator.ContainerFromItem(node) as ListBoxItem;
            if (row is null || !row.IsVisible) return null;
            var screen = row.PointToScreen(new Point());
            var dpi = VisualTreeHelper.GetDpi(row);
            return new { node.Id, node.Name, X = screen.X, Y = screen.Y,
                Width = row.ActualWidth * dpi.DpiScaleX, Height = row.ActualHeight * dpi.DpiScaleY,
                Insertion = ReferenceEquals(row, _dropIndicator),
                Top = row.BorderThickness.Top, Bottom = row.BorderThickness.Bottom,
                Brush = row.BorderBrush?.ToString() };
        }).Where(row => row is not null).OrderBy(row => row!.Y).ToArray()
    };

    public ReferenceColorWorkspaceView()
    {
        InitializeComponent();
        ColorSpaceViewport.SelectionChanged += ColorSpaceViewport_SelectionChanged;
        ImageViewport.State.Changed += (_, _) => { HighlightOverlay.ViewState = ImageViewport.State; HighlightOverlay.InvalidateVisual(); };
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
        HighlightOverlay.ViewState = ImageViewport.State;
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
    private void OnPresetMouseEnter(object sender, MouseEventArgs e)
    {
        if (_editor is not null && sender is FrameworkElement element && element.DataContext is AdobeXmpPreset preset)
            _editor.HoverPreset(preset);
    }
    private void OnSliderDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (_editor?.IsProMode == true && e.OriginalSource is System.Windows.Controls.Primitives.Thumb thumb && FindAncestor<Slider>(thumb) is { } slider && LeftRail.IsAncestorOf(slider)) _editor.BeginEditTransaction();
    }
    private void OnSliderDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_editor?.IsProMode == true && e.OriginalSource is DependencyObject control && LeftRail.IsAncestorOf(control)) _editor.CommitEditTransaction();
    }
    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is T found) return found;
        return null;
    }

    private void OnSamplingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        _inspectingImageColor = false;
        ColorInspectionHint.Visibility = Visibility.Collapsed; PreviewCanvas.Cursor = Cursors.Arrow;
        if (_editor?.IsSampling == true) { _editor.CancelSamplingCommand.Execute(null); e.Handled = true; }
        if (DataContext is ReferenceColorWorkspaceViewModel workspace)
        {
            workspace.ClearColorSpaceHighlight();
            if (ColorSpaceViewport.State is { } state) ColorSpaceViewport.State = state with { Selection = ColorSpaceSelection.None };
            e.Handled = true;
        }
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
        try { _nativeDragActive = true; DragDrop.DoDragDrop(AdjustmentNodeList, node, DragDropEffects.Move); }
        finally { _nativeDragActive = false; ClearInsertion(); }
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

    private async void OnPreviewCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && _editor?.IsSampling != true) { _zoomPan.Fit(); e.Handled = true; return; }
        if ((e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)) || e.ChangedButton == MouseButton.Middle)
        { _panning = true; _panStart = e.GetPosition(PreviewCanvas); PreviewCanvas.CaptureMouse(); e.Handled = true; return; }
        if (_editor?.IsSampling != true && !_inspectingImageColor)
        {
            if (DataContext is ReferenceColorWorkspaceViewModel previewWorkspace) previewWorkspace.ClearColorSpaceHighlight();
            if (_editor?.EffectiveViewMode == "左右对比" && Math.Abs(e.GetPosition(PreviewCanvas).X - PreviewCanvas.ActualWidth * _editor.SplitPosition) < 10)
            { _draggingDivider = true; PreviewCanvas.CaptureMouse(); e.Handled = true; }
            return;
        }
        if (_editor is null || e.ChangedButton != MouseButton.Left) return;
        var mapped = ColorStudioSampleMapping.Map(e.GetPosition(PreviewCanvas), new Size(PreviewCanvas.ActualWidth, PreviewCanvas.ActualHeight),
            _editor.EffectiveViewMode, _editor.SplitPosition, _editor.SourceImage,
            _editor.MatchedImage, _zoomPan);
        if (mapped is not { } sample) { RecordNativeSample(e, null, null); return; }
        var (image, x, y) = sample;
        var pixels = new byte[4];
        var bgra = HistogramService.EnsureBgra32(image); bgra.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
        const int index = 0;
        var mode = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? "减少取样" : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? "增加取样" : _editor.SampleMode;
        if (!_inspectingImageColor) _editor.SampleMode = mode;
        var value = new VisualRgb24(pixels[index + 2], pixels[index + 1], pixels[index]);
        RecordNativeSample(e, new Point(x, y), value);
        if (DataContext is ReferenceColorWorkspaceViewModel workspace) await workspace.HighlightDisplayedImageSampleAsync(image, value);
        if (!_inspectingImageColor) _editor.CompleteDisplayedSample(value);
        e.Handled = true;
    }

    private void OnFilmstripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right)) return;
        if (DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        if (e.OriginalSource is not DependencyObject source) return;
        for (DependencyObject? ancestor = source; ancestor is not null;
             ancestor = ancestor is Visual or Visual3D ? VisualTreeHelper.GetParent(ancestor) : LogicalTreeHelper.GetParent(ancestor))
            if (ancestor is PixelTartRatingControl) return;
        var item = ItemsControl.ContainerFromElement(FindFilmstrip(), source) as ListBoxItem;
        if (item?.DataContext is not ReferenceTargetItem target) return;
        if (e.ChangedButton == MouseButton.Right)
        {
            if (!target.IsSelected) foreach (var photo in workspace.Targets) photo.IsSelected = ReferenceEquals(photo, target);
            OpenFilmstripMenu(item, target, workspace); e.Handled = true; return;
        }
        var index = workspace.Targets.IndexOf(target); _filmstripDownPoint = e.GetPosition(this);
        var modifiers = Keyboard.Modifiers;
        _selectionAnchor = workspace.SelectFilmstripTarget(index, _selectionAnchor,
            modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Control));
        if (target.IsSelected) item.Focus(); else Filmstrip.Focus();
        if (!ReferenceEquals(workspace.ActiveTarget, target) || workspace.IsLoading) workspace.ActivateTargetCommand.Execute(target);
        e.Handled = true;
    }
    private void OpenFilmstripMenu(ListBoxItem anchor, ReferenceTargetItem target, ReferenceColorWorkspaceViewModel workspace)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Style = (Style)FindResource("PixelTart.Menu.Context") };
        MenuItem Action(string title, System.Windows.Input.ICommand command, object? parameter = null) => new() { Header = title, Command = command, CommandParameter = parameter, Style = (Style)FindResource("PixelTart.Menu.Item") };
        menu.Items.Add(Action("查看 / 设为当前图", workspace.ActivateTargetCommand, target));
        var rating = new MenuItem { Header = "评分" };
        for (var value = 0; value <= 5; value++)
        {
            var current = value; var choice = new MenuItem { Header = value == 0 ? "清除评分" : new string('★', value) };
            choice.Click += (_, _) => { foreach (var photo in workspace.SelectedTargets.ToArray()) photo.Rating = current; };
            rating.Items.Add(choice);
        }
        menu.Items.Add(rating);
        var color = new MenuItem { Header = "颜色标记" };
        foreach (var name in new[] { "", "红", "橙", "黄", "绿", "蓝", "紫" })
        {
            var current = name; var preview = new ReferenceTargetItem("") { ColorLabel = name };
            var choice = new MenuItem { Header = new Border { Width = 30, Height = 16, Background = preview.ColorLabelBrush, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) }, ToolTip = preview.ColorLabelAccessibleName };
            System.Windows.Automation.AutomationProperties.SetName(choice, preview.ColorLabelAccessibleName);
            choice.Click += (_, _) => { foreach (var photo in workspace.SelectedTargets.ToArray()) photo.ColorLabel = current; };
            color.Items.Add(choice);
        }
        menu.Items.Add(color); menu.Items.Add(new Separator());
        menu.Items.Add(Action("复制当前调整", workspace.CopyAdjustmentsCommand));
        menu.Items.Add(Action("应用已复制调整", workspace.ApplyAdjustmentsCommand));
        menu.Items.Add(Action("同步到所选", workspace.SyncSelectedCommand));
        menu.Items.Add(new Separator()); menu.Items.Add(Action("快速导出所选（JPEG / RAW→TIFF）", workspace.ExportSelectedCommand));
        menu.Items.Add(Action("通过发布配方导出…", workspace.PreparePublishingCommand));
        menu.Items.Add(new Separator());
        var remove = new MenuItem { Header = "从当前批次移除", IsEnabled = !workspace.IsExporting };
        remove.Click += async (_, _) => await workspace.RemoveSelectedFromBatchAsync(); menu.Items.Add(remove);
        foreach (var item in menu.Items.OfType<MenuItem>()) AssetLibraryPage.AttachContextSubmenuPlacement(item);
        menu.IsOpen = true;
    }

    private ListBox? FindFilmstrip() => FindName("Filmstrip") as ListBox;

    private void OnFilmstripKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !Filmstrip.IsKeyboardFocusWithin) return;
        if (Keyboard.FocusedElement is DependencyObject focus && FindAncestor<PixelTartRatingControl>(focus) is not null) return;
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
        if (args.OldValue is ReferenceColorWorkspaceViewModel oldWorkspace) { oldWorkspace.PropertyChanged -= WorkspaceOnPropertyChanged; oldWorkspace.ColorSpaceSelectionChanged -= WorkspaceColorSpaceSelectionChanged; }
        if (args.NewValue is ReferenceColorWorkspaceViewModel newWorkspace) { newWorkspace.PropertyChanged += WorkspaceOnPropertyChanged; newWorkspace.ColorSpaceSelectionChanged += WorkspaceColorSpaceSelectionChanged; }
        _editor = (args.NewValue as ReferenceColorWorkspaceViewModel)?.Editor;
        if (_editor is not null) _editor.PropertyChanged += EditorOnPropertyChanged;
        UpdateResponsiveLayout();
    }
    private void WorkspaceOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is ReferenceColorWorkspaceViewModel analysis) HighlightOverlay.IsOriginal = analysis.AnalysisIsOriginal;
        if (args.PropertyName == nameof(ReferenceColorWorkspaceViewModel.ColorSpaceModel) && sender is ReferenceColorWorkspaceViewModel workspace)
        {
            if (workspace.ColorSpaceModel is null)
            {
                _inspectingImageColor = false; ColorInspectionHint.Visibility = Visibility.Collapsed; PreviewCanvas.Cursor = Cursors.Arrow;
            }
            ColorSpaceViewport.State = workspace.ColorSpaceModel is { } model ? ColorSpaceRendererContract.Create(model).WithMode(ColorCloudMode.Source) : null;
            ColorSpaceViewport.FitCamera();
            if (_expandedCloud is not null) { _expandedCloud.State = ColorSpaceViewport.State; _expandedCloud.FitCamera(); }
        }
    }
    private void WorkspaceColorSpaceSelectionChanged(object? sender, ColorSpaceSelection selection)
    {
        if (ColorSpaceViewport.State is { } state) ColorSpaceViewport.State = state with { Selection = selection };
        if (_expandedCloud?.State is { } expanded) _expandedCloud.State = expanded with { Selection = selection };
    }
    private void ColorSpaceViewport_SelectionChanged(object? sender, ColorSpaceSelection selection)
    {
        if (DataContext is ReferenceColorWorkspaceViewModel workspace)
        {
            workspace.HighlightCloudSelection(selection.PointIndex);
        }
    }

    private void EditorOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        HighlightOverlay.ViewMode = _editor?.EffectiveViewMode ?? "原片";
        HighlightOverlay.SplitPosition = _editor?.SplitPosition ?? .5;
        HighlightOverlay.InvalidateVisual();
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

    private bool _editRailOpen;
    private void OnToggleEditRail(object sender, RoutedEventArgs e) { _editRailOpen = !_editRailOpen; UpdateResponsiveLayout(); }
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

        Grid.SetColumn(LeftRail, narrow ? 1 : 0);
        Panel.SetZIndex(LeftRail, narrow ? 21 : 0);
        LeftRail.Width = narrow ? Math.Min(300, Math.Max(220, availableWidth - 40)) : double.NaN;
        LeftRail.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        LeftRail.Visibility = !focus && (!narrow || _editRailOpen) ? Visibility.Visible : Visibility.Collapsed;
        EditRailButton.Visibility = !focus && narrow ? Visibility.Visible : Visibility.Collapsed;
        LeftColumn.MinWidth = focus || narrow ? 0 : compact ? 280 : 240;
        RightColumn.MinWidth = focus || compact ? 0 : 280;
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
    private void OnColorSpaceReset(object sender, RoutedEventArgs e) => ColorSpaceViewport.ResetCamera();
    private void OnColorSpaceFit(object sender, RoutedEventArgs e) => ColorSpaceViewport.FitCamera();

    private void OnExpandReference(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        var navigator = new ReferenceNavigator { SourcePath = _editor.CurrentReferencePath };
        CreateInspectionWindow("参考图片 · " + Path.GetFileName(_editor.CurrentReferencePath), navigator).Show();
    }

    private void OnExpandColorSpace(object sender, RoutedEventArgs e)
    {
        if (_expandedCloud is not null) { Window.GetWindow(_expandedCloud)?.Activate(); return; }
        var viewport = new ColorSpace3DViewport { State = ColorSpaceViewport.State, DataContext = DataContext };
        foreach (var (property, path) in new[] { (ColorSpace3DViewport.PointSizeProperty, "CloudPointSize"), (ColorSpace3DViewport.PointOpacityProperty, "CloudPointOpacity"), (ColorSpace3DViewport.SelectionToleranceProperty, "SelectionTolerance") })
            viewport.SetBinding(property, new System.Windows.Data.Binding(path));
        _expandedCloud = viewport;
        viewport.SelectionChanged += ColorSpaceViewport_SelectionChanged;
        var panel = new DockPanel(); var actions = new StackPanel { Orientation = Orientation.Horizontal };
        void Add(string text, Action action) { var button = new Button { Content = text, Margin = new Thickness(4) }; button.Click += (_, _) => action(); actions.Children.Add(button); }
        Add("重置", viewport.ResetCamera); Add("适合", viewport.FitCamera);
        Add("清除高亮", () => { if (DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.ClearColorSpaceHighlight(); });
        DockPanel.SetDock(actions, Dock.Top); panel.Children.Add(actions);
        var controls = new CloudInspectionControls { DataContext = DataContext }; DockPanel.SetDock(controls, Dock.Top); panel.Children.Add(controls); panel.Children.Add(viewport);
        var window = CreateInspectionWindow("3D 色彩空间 · 点击取色 / 拖动旋转 / Shift 拖动平移", panel);
        window.PreviewKeyDown += OnSamplingKeyDown;
        window.Closed += (_, _) => { viewport.SelectionChanged -= ColorSpaceViewport_SelectionChanged; _expandedCloud = null; };
        window.Show(); viewport.FitCamera();
    }

    private async void OnInspectImageColor(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        try { await workspace.BuildColorSpaceModelAsync(); _inspectingImageColor = workspace.HasColorSpaceModel; }
        catch (OperationCanceledException) { return; }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException)
        { _inspectingImageColor = false; return; }
        if (_inspectingImageColor)
        {
            ColorSpaceViewport.FitCamera();
            ColorSpaceSection.IsExpanded = true;
            PreviewCanvas.Cursor = Cursors.Cross;
            ColorInspectionHint.Visibility = Visibility.Visible; Focus();
        }
    }

    private Window CreateInspectionWindow(string title, FrameworkElement content) => new()
    {
        Title = title, Owner = Window.GetWindow(this), Content = content,
        Width = Math.Min(1000, SystemParameters.WorkArea.Width * .85), Height = Math.Min(780, SystemParameters.WorkArea.Height * .85),
        Background = (Brush)FindResource("CanvasBackgroundBrush"), WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
}

public sealed class MatchEngineLabelConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value.ToString() switch
    { "Stable" => "稳定版", "MatchV4Beta" => "V4 匹配实验版", _ => value.ToString() ?? "" };
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => DependencyProperty.UnsetValue;
}
