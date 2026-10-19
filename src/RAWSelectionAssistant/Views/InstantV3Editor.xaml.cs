using System.Windows.Input;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class InstantV3Editor:System.Windows.Controls.UserControl
{
 public InstantV3Editor()=>InitializeComponent();
 private void PaperClicked(object sender,MouseButtonEventArgs e){if(DataContext is ReferenceColorWorkspaceViewModel vm)vm.RequestLightPrintPreview();}
}
