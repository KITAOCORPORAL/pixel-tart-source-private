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

    public ReferenceColorWorkspaceView()
    {
        InitializeComponent();
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
    private void OnNodeDragStart(object sender, MouseButtonEventArgs e)
    {
        _nodeDragPoint = e.GetPosition(AdjustmentNodeList);
        _draggedNode = (ItemsControl.ContainerFromElement(AdjustmentNodeList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as ColorAdjustmentStackNode;
    }
    private void OnNodeDragMove(object sender, MouseEventArgs e)
    {
        if (_draggedNode is null || e.LeftButton != MouseButtonState.Pressed ||
            (e.GetPosition(AdjustmentNodeList) - _nodeDragPoint).Length < SystemParameters.MinimumVerticalDragDistance) return;
        var node = _draggedNode; _draggedNode = null;
        DragDrop.DoDragDrop(AdjustmentNodeList, node, DragDropEffects.Move);
    }
    private void OnNodeDrop(object sender, DragEventArgs e)
    {
        if (_editor is null || e.Data.GetData(typeof(ColorAdjustmentStackNode)) is not ColorAdjustmentStackNode source) return;
        var destination = (ItemsControl.ContainerFromElement(AdjustmentNodeList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as ColorAdjustmentStackNode;
        if (destination is null) return;
        _editor.MoveAdjustmentNode(source.Id, destination.Id); e.Handled = true;
    }
    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (_editor is null) return;
        if (e.Key == Key.Enter) { _editor.CommitNodeRenameCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Escape) { _editor.CancelNodeRenameCommand.Execute(null); e.Handled = true; }
    }

    private void OnPreviewCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editor?.IsSampling != true) return;
        var mapped = ColorStudioSampleMapping.Map(e.GetPosition(PreviewCanvas), new Size(PreviewCanvas.ActualWidth, PreviewCanvas.ActualHeight),
            _editor.ViewMode, _editor.SplitPosition, _editor.SourceImage, _editor.MatchedImage);
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
        var narrow = availableWidth is > 0 and < 980;

        if (compact && !_wasCompact) _editor?.SetResponsiveContext(true);
        _wasCompact = compact;

        LeftColumn.MinWidth = focus || narrow ? 0 : compact ? 300 : 240;
        RightColumn.MinWidth = focus || compact ? 0 : 224;
        LeftColumn.Width = focus || narrow ? new GridLength(0) : compact ? new GridLength(300) : new GridLength(.19, GridUnitType.Star);
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
