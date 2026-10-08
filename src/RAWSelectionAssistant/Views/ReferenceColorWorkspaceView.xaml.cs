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
    private bool _wasLowWindow;


    private double _lastResponsiveWidth = -1;

    private ColorSpaceCamera? _inspectionCamera;
    private Point _filmstripDownPoint;
    private Point _nodeDragPoint;
    private ColorAdjustmentStackNode? _draggedNode;
    private ColorStudioZoomPanState _zoomPan => ImageViewport.State;
    private Point _panStart;
    private bool _panning;
    private bool _inspectingImageColor;
    private bool _cloudExpanded;
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
        ColorSpaceViewport.SurfaceSelectionChanged += OnSurfaceSelection;
        ToneZones.ZoneHovered += (_, zone) => { if (DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.HighlightToneZone(zone); };
        ToneZonesExpander.Collapsed += (_, _) => { ToneZones.ClearHover(); if (DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.ClearPreviewSelection(); };
        ImageViewport.State.Changed += (_, _) => { HighlightOverlay.ViewState = ImageViewport.State; HighlightOverlay.InvalidateVisual(); };
        _zoomPan.Changed += (_, _) => { ZoomLabel.Text = $"{_zoomPan.Zoom:P0}"; _editor?.RequestPreviewZoom(_zoomPan.Zoom, VisualTreeHelper.GetDpi(ImageViewport).DpiScaleX); };
        AdjustmentNodeList.DragOver += OnNodeDragOver;
        AdjustmentNodeList.DragLeave += (_, _) => ClearInsertion();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        Loaded += (_, _) => UpdateResponsiveLayout();
        WorkspaceGrid.SizeChanged += (_, _) => UpdateResponsiveLayout();
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

    private void OnResetDevelopGroup(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string group } && DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.Editor.ResetDevelopGroup(group); }
    private void OnAddLocalAdjustment(object sender, RoutedEventArgs e) { if (DataContext is ReferenceColorWorkspaceViewModel workspace) { workspace.Editor.AddAdjustmentNodeCommand.Execute("ColorRange"); AuxModes.SelectedIndex = 3; ToolModes.SelectedIndex = 1; } }
    private void OnColorHistoryKeyDown(object sender, KeyEventArgs e)
    {
        // A focused text editor owns Ctrl+Z/Y; it must not edit image history as well.
        if (Keyboard.FocusedElement is TextBox) return;
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
        if (_editor?.IsProMode == true && e.OriginalSource is System.Windows.Controls.Primitives.Thumb thumb && FindAncestor<Slider>(thumb) is { } slider && EditingRail.IsAncestorOf(slider)) _editor.BeginEditTransaction();
    }
    private void OnSliderDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_editor?.IsProMode == true && e.OriginalSource is DependencyObject control && EditingRail.IsAncestorOf(control)) _editor.CommitEditTransaction();
    }
    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is T found) return found;
        return null;
    }

    // The shell tunnels Escape before child controls. It asks the visible workspace
    // to clear transient inspection before interpreting Escape as route navigation.
    public bool TryClearTransientInspection()
    {
        if (TryCancelNumericDraft()) return true;
        if (Keyboard.FocusedElement is DependencyObject focused && FindAncestor<StudioCurveEditor>(focused)?.CancelPointerGesture() == true) return true;
        if (ColorSpaceViewport.CancelPointerGesture()) return true;
        if (_panning || _draggingDivider) { _panning = _draggingDivider = false; PreviewCanvas.ReleaseMouseCapture(); return true; }
        var workspace = DataContext as ReferenceColorWorkspaceViewModel;
        if (workspace?.NodeSyncOpen == true) { workspace.NodeSyncOpen = false; return true; }
        if (workspace?.AdjustmentCopyOpen == true) { workspace.AdjustmentCopyOpen = false; return true; }
        if (!_inspectingImageColor && _editor?.IsSampling != true && _editor?.ShowSelection != true && workspace?.HighlightedPixels.Count is not > 0)
            return true; // Studio has an explicit Back action; Esc only cancels transient editing/inspection.
        _inspectingImageColor = false;
        ColorInspectionHint.Visibility = Visibility.Collapsed;
        PreviewCanvas.Cursor = Cursors.Arrow;
        if (_editor?.IsSampling == true) _editor.CancelSamplingCommand.Execute(null);
        workspace?.ClearPreviewSelection(); ToneZones.ClearHover();
        return true;
    }
    public bool TryCancelNumericDraft() => Keyboard.FocusedElement is StudioNumericEditor number && IsAncestorOf(number) && number.CancelDraft();

    private void OnSamplingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && TryClearTransientInspection()) e.Handled = true;
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
        if (DataContext is ReferenceColorWorkspaceViewModel workspace)
        {
            if (_inspectingImageColor) await workspace.HighlightDisplayedImageSampleAsync(image, value);
            else await workspace.CompleteRangeSampleAsync((x + .5) / image.PixelWidth, (y + .5) / image.PixelHeight);
        }
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
            if (!target.IsSelected) workspace.SelectOnlyVisibleTarget(target);
            OpenFilmstripMenu(item, target, workspace); e.Handled = true; return;
        }
        var index = workspace.VisibleTargets.IndexOf(target); _filmstripDownPoint = e.GetPosition(this);
        var modifiers = Keyboard.Modifiers;
        workspace.SelectVisibleFilmstripTarget(index,
            modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Control));
        if (target.IsSelected) item.Focus(); else Filmstrip.Focus();
        if (!ReferenceEquals(workspace.ActiveTarget, target) || workspace.IsLoading) workspace.ActivateTargetCommand.Execute(target);
        e.Handled = true;
    }
    private void OpenFilmstripMenu(ListBoxItem anchor, ReferenceTargetItem target, ReferenceColorWorkspaceViewModel workspace)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Style = (Style)FindResource("PixelTart.Menu.Context") };
        MenuItem Action(string title, System.Windows.Input.ICommand command, object? parameter = null)
        {
            var item = new MenuItem { Command = command, CommandParameter = parameter, Style = (Style)FindResource("PixelTart.Menu.Item") };
            StudioTextExtension.Bind(item, HeaderedItemsControl.HeaderProperty, title); return item;
        }
        menu.Items.Add(Action("查看 / 设为当前图", workspace.ActivateTargetCommand, target));
        var rating = new MenuItem { Header = "评分" };
        StudioTextExtension.Bind(rating, HeaderedItemsControl.HeaderProperty, "评分");
        for (var value = 0; value <= 5; value++)
        {
            var current = value; var choice = new MenuItem { Header = value == 0 ? "清除评分" : new string('★', value) };
            if (value == 0) StudioTextExtension.Bind(choice, HeaderedItemsControl.HeaderProperty, "清除评分");
            choice.Click += (_, _) => { foreach (var photo in workspace.SelectedTargets.ToArray()) photo.Rating = current; };
            rating.Items.Add(choice);
        }
        menu.Items.Add(rating);
        var color = new MenuItem { Header = "颜色标记" };
        StudioTextExtension.Bind(color, HeaderedItemsControl.HeaderProperty, "颜色标记");
        foreach (var name in new[] { "", "红", "橙", "黄", "绿", "蓝", "紫" })
        {
            var current = name; var preview = new ReferenceTargetItem("") { ColorLabel = name };
            var choice = new MenuItem { Header = new Border { Width = 30, Height = 16, Background = preview.ColorLabelBrush, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) }, ToolTip = preview.ColorLabelAccessibleName };
            StudioTextExtension.Bind(choice, ToolTipProperty, name == "" ? "无色标" : name + "色标");
            StudioTextExtension.Bind(choice, System.Windows.Automation.AutomationProperties.NameProperty, name == "" ? "无色标" : name + "色标");
            choice.Click += (_, _) => { foreach (var photo in workspace.SelectedTargets.ToArray()) photo.ColorLabel = current; };
            color.Items.Add(choice);
        }
        menu.Items.Add(color); menu.Items.Add(new Separator());
        menu.Items.Add(Action("复制当前调整", workspace.CopyAdjustmentsCommand));
        menu.Items.Add(Action("应用已复制调整", workspace.ApplyAdjustmentsCommand));
        menu.Items.Add(Action("同步到所选", workspace.SyncSelectedCommand));
        menu.Items.Add(new Separator()); menu.Items.Add(Action("快速导出所选（源格式 / RAW→TIFF16）", workspace.ExportSelectedCommand));
        menu.Items.Add(Action("通过发布配方导出…", workspace.PreparePublishingCommand));
        menu.Items.Add(new Separator());
        var remove = new MenuItem { Header = "从当前批次移除", IsEnabled = !workspace.IsExporting };
        StudioTextExtension.Bind(remove, HeaderedItemsControl.HeaderProperty, "从当前批次移除");
        remove.Click += async (_, _) => await workspace.RemoveSelectedFromBatchAsync(); menu.Items.Add(remove);
        foreach (var item in menu.Items.OfType<MenuItem>()) AssetLibraryPage.AttachContextSubmenuPlacement(item);
        menu.IsOpen = true;
    }

    private ListBox? FindFilmstrip() => FindName("Filmstrip") as ListBox;

    private async void OnFilmstripKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !Filmstrip.IsKeyboardFocusWithin || DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is DependencyObject ratingFocus && FindAncestor<PixelTartRatingControl>(ratingFocus) is not null) return;
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.A && modifiers.HasFlag(ModifierKeys.Control)) { workspace.SelectAllVisible(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { if (workspace.ActiveTarget is { } active) workspace.SelectOnlyVisibleTarget(active); e.Handled = true; return; }
        var delta = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (delta == 0) return;
        e.Handled = true;
        await workspace.MoveVisibleTargetAsync(delta, modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Control));
        if (workspace.ActiveTarget is { } current) Filmstrip.ScrollIntoView(current);
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
        if (sender is ReferenceColorWorkspaceViewModel analysis) { HighlightOverlay.IsOriginal = analysis.AnalysisIsOriginal; if (args.PropertyName == nameof(ReferenceColorWorkspaceViewModel.PreviewHistogram)) ToneZones.ClearHover(); }
        if (args.PropertyName == nameof(ReferenceColorWorkspaceViewModel.ColorSpaceModel) && sender is ReferenceColorWorkspaceViewModel workspace)
        {
            if (workspace.ColorSpaceModel is null)
            {
                _inspectingImageColor = false; ColorInspectionHint.Visibility = Visibility.Collapsed; PreviewCanvas.Cursor = Cursors.Arrow;
            }
            _inspectionCamera = ColorSpaceViewport.State?.Camera ?? _inspectionCamera;
            ColorSpaceViewport.State = workspace.ColorSpaceModel is { } model
                ? ColorSpaceRendererContract.Create(model).WithMode(ColorCloudMode.Source) with { Camera = _inspectionCamera ?? ColorSpaceCamera.Default }
                : null;
        }
    }
    private void WorkspaceColorSpaceSelectionChanged(object? sender, ColorSpaceSelection selection)
    {
        if (ColorSpaceViewport.State is { } state) ColorSpaceViewport.State = state with { Selection = selection };
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
        if (args.PropertyName == nameof(TetherReferenceModeViewModel.SourcePixelSize)) _editor?.RequestPreviewZoom(_zoomPan.Zoom, VisualTreeHelper.GetDpi(ImageViewport).DpiScaleX);
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

    private bool _editRailOpen = true;

    private void OnToggleEditRail(object sender, RoutedEventArgs e) { _editRailOpen = !_editRailOpen; UpdateResponsiveLayout(); }
    private void UpdateResponsiveLayout()
    {
        if (LeftColumn is null || RightColumn is null || CenterColumn is null) return;
        var width = GetAvailableWidth();
        if (width <= 0) return;
        _lastResponsiveWidth = width;
        var focus = _editor?.FocusView == true;
        var compact = width < 1100;
        if (compact && !_wasCompact && IsLoaded) _editor?.SetResponsiveContext(true);
        _wasCompact = compact;
        var left = !focus && (_cloudExpanded || _editor?.IsContextVisible == true);
        var right = !focus && _editRailOpen;
        // Narrow windows use one rail at a time, leaving the photo as a real grid column.
        if (width < 950 && left && right && !_cloudExpanded) left = false;
        var rail = Math.Clamp(width * .21, 250, 320);
        var bottomDock = _cloudExpanded && left && width < 1050;
        LeftColumn.MinWidth = RightColumn.MinWidth = CenterColumn.MinWidth = 0;
        LeftColumn.Width = new GridLength(left && !bottomDock ? (_cloudExpanded ? Math.Clamp(width * .3, 280, 440) : Math.Min(290, rail)) : 0);
        // Include the rail margin in the column allocation. The shared Inspector style
        // previously forced 280 DIP into a 250 DIP column, pushing editors off screen.
        RightColumn.Width = new GridLength(right ? Math.Clamp(width * .25, 312, 368) : 0);
        CenterColumn.Width = new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(EditingRail, 2); Grid.SetColumn(ContextRail, 0);
        Grid.SetRow(ContextRail, bottomDock ? 1 : 0);
        Grid.SetColumnSpan(ContextRail, bottomDock ? 3 : 1);
        ContextRail.Margin = bottomDock ? new Thickness(0, 8, 0, 0) : new Thickness(0, 0, 8, 0);
        AuxDockRow.Height = bottomDock ? new GridLength(Math.Min(260, Math.Max(120, WorkspaceGrid.ActualHeight * .4))) : new GridLength(0);
        EditingRail.Width = ContextRail.Width = double.NaN;
        EditingRail.HorizontalAlignment = ContextRail.HorizontalAlignment = HorizontalAlignment.Stretch;
        EditingRail.Visibility = right ? Visibility.Visible : Visibility.Collapsed;
        ContextRail.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
        EditRailButton.Visibility = ContextRailButton.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        CentralAnalysis.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        ToneZonesExpander.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        var headerWrap = width < 1050;
        Grid.SetRow(HeaderActions, headerWrap ? 1 : 0); Grid.SetColumn(HeaderActions, headerWrap ? 0 : 1);
        Grid.SetColumnSpan(HeaderActions, headerWrap ? 2 : 1); Grid.SetColumnSpan(SourceBars, headerWrap ? 2 : 1);
        HeaderActions.Margin = new Thickness(0); HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
        if (ActualHeight is > 0 and < 530 && !_wasLowWindow) FilmstripExpander.IsExpanded = false;
        _wasLowWindow = ActualHeight is > 0 and < 530;
        UpdateAnalysisSpace();
        ColorSpaceViewport.Height = _cloudExpanded ? Math.Clamp(WorkspaceGrid.ActualHeight * .65, 300, 480) : 300;
    }
    private void UpdateAnalysisSpace()
    {
        if (AnalysisScroll is null || EditingRail.ActualHeight <= 0) return;
        AnalysisScroll.MaxHeight = Math.Max(60, Math.Min(240, EditingRail.ActualHeight * .34));
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

    private void OnExpandColorSpace(object sender, RoutedEventArgs e) => ToggleCloudDock();
    internal void ToggleCloudDock()
    {
        // Resize the existing dock, not a second viewport/window. Camera, selection,
        // pan mode and appearance therefore have exactly one owner across both sizes.
        _cloudExpanded = !_cloudExpanded;
        AuxModes.SelectedIndex = 1;
        ColorSpaceSection.IsExpanded = true;
        StudioToolPanel.Text(ExpandCloudButton, ContentControl.ContentProperty, _cloudExpanded ? "Collapse" : "Expand");
        UpdateResponsiveLayout();
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
            AuxModes.SelectedIndex = 1;
            ColorSpaceSection.IsExpanded = true;
            PreviewCanvas.Cursor = Cursors.Cross;
            ColorInspectionHint.Visibility = Visibility.Visible; Focus();
        }
    }

    private Window CreateInspectionWindow(string title, FrameworkElement content)
    {
        var owner = Window.GetWindow(this);
        var window = new Window { Title = title, Owner = owner, Content = content,
            Background = (Brush)FindResource("CanvasBackgroundBrush") };
        PlaceInspectionWindow(window, owner, new Size(1000, 780), .85);
        return window;
    }

    private static void PlaceInspectionWindow(Window window, Window? owner, Size preferred, double widthFraction)
    {
        // WorkArea is monitor-specific; SystemParameters.WorkArea always uses the primary display.
        var work = owner is null ? SystemParameters.WorkArea : ContextMenuMonitor.OwnerWorkArea(owner);
        var bounds = work;
        if (owner is not null)
        {
            var topLeft = owner.PointToScreen(new Point());
            var transform = PresentationSource.FromVisual(owner)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            bounds = new Rect(transform.Transform(topLeft), new Size(owner.ActualWidth, owner.ActualHeight));
        }
        var placement = ContextMenuMonitor.InspectionBounds(bounds, work,
            new Size(Math.Min(preferred.Width, work.Width * widthFraction), Math.Min(preferred.Height, work.Height * .85)));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = placement.Left; window.Top = placement.Top;
        window.Width = placement.Width; window.Height = placement.Height;
    }
}
