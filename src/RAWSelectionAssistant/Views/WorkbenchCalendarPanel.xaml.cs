using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class WorkbenchCalendarPanel : UserControl
{
    public WorkbenchCalendarPanel() => InitializeComponent();

    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: MonthDayViewModel day } border || DataContext is not WorkCalendarViewModel viewModel) return;
        viewModel.Month.SelectDateCommand.Execute(day);
        border.Focus();
        if (e.ClickCount == 2) viewModel.Month.CreateBookingCommand.Execute(day);
        e.Handled = true;
    }

    private void DayCell_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: MonthDayViewModel day } || DataContext is not WorkCalendarViewModel viewModel) return;
        var offset = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -7, Key.Down => 7, _ => 0 };
        if (offset != 0)
        {
            viewModel.MoveSelectedDate(offset);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            var first = day.VisibleBookings.FirstOrDefault();
            if (first is not null) viewModel.Month.OpenBookingCommand.Execute(first);
            else viewModel.Month.SelectDateCommand.Execute(day);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            viewModel.Month.SelectDateCommand.Execute(day);
            e.Handled = true;
        }
    }

    private void CreateBooking_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu contextMenu } &&
            contextMenu.PlacementTarget is FrameworkElement { DataContext: MonthDayViewModel day } &&
            DataContext is WorkCalendarViewModel viewModel)
            viewModel.Month.CreateBookingCommand.Execute(day);
    }

    private void OpenFullCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel viewModel)
            viewModel.NavigateCommand.Execute("WorkCalendar");
    }

    private void ViewDayDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu contextMenu } &&
            contextMenu.PlacementTarget is FrameworkElement { DataContext: MonthDayViewModel day } &&
            DataContext is WorkCalendarViewModel viewModel)
        {
            viewModel.Month.SelectDateCommand.Execute(day);
        }
    }
}
