using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class MonthCalendarView : UserControl
{
    public MonthCalendarView() => InitializeComponent();
    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: MonthDayViewModel day } || DataContext is not MonthCalendarViewModel viewModel) return;
        for (var current = e.OriginalSource as System.Windows.DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Button) return;
        if (e.ClickCount == 2) viewModel.CreateBookingCommand.Execute(day);
        else viewModel.SelectDateCommand.Execute(day);
    }

    private static MonthDayViewModel? ContextDay(object sender) =>
        sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: MonthDayViewModel day } } } ? day : null;

    private void CreateBooking_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ContextDay(sender) is { } day && DataContext is MonthCalendarViewModel viewModel)
            viewModel.CreateBookingCommand.Execute(day);
    }

    private void CloseDay_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ContextDay(sender) is { } day && DataContext is MonthCalendarViewModel viewModel)
            viewModel.CloseDayCommand.Execute(day);
    }

    private void OpenDay_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ContextDay(sender) is { } day && DataContext is MonthCalendarViewModel viewModel)
            viewModel.OpenDayCommand.Execute(day);
    }

    private void ViewDay_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ContextDay(sender) is not { } day) return;
        if (FindParent<WorkCalendarView>(this)?.DataContext is WorkCalendarViewModel calendar)
            _ = calendar.OpenDayDetailsForDateAsync(day.Date);
        else if (DataContext is MonthCalendarViewModel viewModel)
            viewModel.SelectDateCommand.Execute(day);
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        for (var current = child; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private void DaySettings_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ContextDay(sender) is { } day && DataContext is MonthCalendarViewModel viewModel)
            viewModel.SelectDateCommand.Execute(day);
    }
}
