using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class WorkCalendarView : UserControl
{
    private WorkCalendarViewModel? _subscribedViewModel;
    public WorkCalendarView() => InitializeComponent();

    private async void WorkCalendarView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkCalendarViewModel viewModel) return;
        if (!ReferenceEquals(_subscribedViewModel, viewModel))
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.SearchFocusRequested -= ViewModel_SearchFocusRequested;
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel.DayDetailsNavigationRequested -= ViewModel_DayDetailsNavigationRequested;
            }
            _subscribedViewModel = viewModel;
            viewModel.SearchFocusRequested += ViewModel_SearchFocusRequested;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            viewModel.DayDetailsNavigationRequested += ViewModel_DayDetailsNavigationRequested;
        }
        await viewModel.ActivateAsync();
        ApplyCalendarToolbarLayout(CalendarToolbar.ActualWidth);
        Focus();
        if (viewModel.IsDetailsOpen) DayDetailsPanel.PrepareForNavigation();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkCalendarViewModel.IsDetailsOpen) && sender is WorkCalendarViewModel { IsDetailsOpen: true })
            Dispatcher.BeginInvoke(DayDetailsPanel.PrepareForNavigation);
    }

    private void ViewModel_DayDetailsNavigationRequested(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (sender is WorkCalendarViewModel { IsDetailsOpen: true }) DayDetailsPanel.PrepareForNavigation();
        else DayDetailsHost.Focus();
    });

    private void CalendarToolbar_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyCalendarToolbarLayout(e.NewSize.Width);

    private void ApplyCalendarToolbarLayout(double width)
    {
        if (CalendarFilterSearchGroup is null || CalendarDateNavigationGroup is null || SearchBox is null) return;
        var compact = width > 0 && width < 1180;
        Grid.SetRow(CalendarFilterSearchGroup, compact ? 1 : 0);
        Grid.SetColumn(CalendarFilterSearchGroup, compact ? 0 : 4);
        Grid.SetColumnSpan(CalendarFilterSearchGroup, compact ? 5 : 1);
        CalendarFilterSearchGroup.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        CalendarFilterSearchGroup.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);
        CalendarDateNavigationGroup.MinWidth = compact ? 360 : 392;
        SearchBox.Width = width switch
        {
            < 1050 => 150,
            < 1400 => 170,
            _ => 210
        };
    }

    private void ViewModel_SearchFocusRequested(object? sender, EventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

}
