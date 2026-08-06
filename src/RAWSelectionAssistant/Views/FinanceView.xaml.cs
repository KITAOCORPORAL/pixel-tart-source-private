using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class FinanceView : UserControl
{
    public FinanceView() => InitializeComponent();
    private async void FinanceView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FinanceViewModel viewModel) await viewModel.RefreshAsync();
    }
}
