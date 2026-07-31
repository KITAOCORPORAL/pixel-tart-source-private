using System.Windows;

namespace RAWSelectionAssistant.Views;

public partial class UpgradeTutorialWindow : Window
{
    public UpgradeTutorialWindow() => InitializeComponent();

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
