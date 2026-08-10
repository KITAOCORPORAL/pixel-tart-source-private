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

}
