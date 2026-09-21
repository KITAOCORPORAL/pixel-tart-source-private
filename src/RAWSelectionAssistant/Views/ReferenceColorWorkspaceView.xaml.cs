using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView : UserControl
{
    private TetherReferenceModeViewModel? _editor;

    public ReferenceColorWorkspaceView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        DataContextChanged += OnDataContextChanged;
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
        var focus = _editor?.FocusView == true;
        var compact = ActualWidth is > 0 and < 1320;
        var narrow = ActualWidth is > 0 and < 980;

        LeftColumn.MinWidth = focus || narrow ? 0 : compact ? 212 : 240;
        RightColumn.MinWidth = focus || compact ? 0 : 224;
        LeftColumn.Width = focus || narrow ? new GridLength(0) : new GridLength(compact ? .24 : .21, GridUnitType.Star);
        CenterColumn.Width = new GridLength(1, GridUnitType.Star);
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
        ContextRailButton.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
    }
}
