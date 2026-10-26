using System.Windows;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();
    public HelpAction SelectedAction => HelpAction.None;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
