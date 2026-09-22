using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView : UserControl
{
    private TetherReferenceModeViewModel? _editor;
    private bool _wasCompact;
    private double _lastResponsiveWidth = -1;
    private int _selectionAnchor = -1;
    private Point _filmstripDownPoint;

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
    }

    private void OnFilmstripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        if (e.OriginalSource is not DependencyObject source) return;
        var item = ItemsControl.ContainerFromElement(FindFilmstrip(), source) as ListBoxItem;
        if (item?.DataContext is not ReferenceTargetItem target) return;
        var index = workspace.Targets.IndexOf(target); _filmstripDownPoint = e.GetPosition(this);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selectionAnchor >= 0)
        {
            var start = Math.Min(_selectionAnchor, index); var end = Math.Max(_selectionAnchor, index); foreach (var entry in workspace.Targets) entry.IsSelected = false; for (var i = start; i <= end; i++) workspace.Targets[i].IsSelected = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) target.IsSelected = !target.IsSelected;
        else { foreach (var entry in workspace.Targets) entry.IsSelected = false; target.IsSelected = true; }
        _selectionAnchor = index; workspace.ActivateTargetCommand.Execute(target); e.Handled = true;
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
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selectionAnchor >= 0)
        {
            var start = Math.Min(_selectionAnchor, next); var end = Math.Max(_selectionAnchor, next); foreach (var target in workspace.Targets) target.IsSelected = false; for (var i = start; i <= end; i++) workspace.Targets[i].IsSelected = true;
        }
        else if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { foreach (var target in workspace.Targets) target.IsSelected = false; targetAt.IsSelected = true; _selectionAnchor = next; }
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

        LeftColumn.MinWidth = focus || narrow ? 0 : compact ? 212 : 240;
        RightColumn.MinWidth = focus || compact ? 0 : 224;
        LeftColumn.Width = focus || narrow ? new GridLength(0) : new GridLength(compact ? .24 : .19, GridUnitType.Star);
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
