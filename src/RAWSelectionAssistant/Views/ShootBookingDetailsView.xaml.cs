using System.Windows.Controls;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class ShootBookingDetailsView : UserControl
{
    public ShootBookingDetailsView() => InitializeComponent();

    public void PrepareForNavigation()
    {
        if (DataContext is ShootBookingDetailsViewModel viewModel) viewModel.SelectedTabIndex = 0;
        OverviewScroll.ScrollToTop();
        Focus();
    }
}
