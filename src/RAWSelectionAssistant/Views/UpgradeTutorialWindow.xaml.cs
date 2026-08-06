using System.Windows;

namespace RAWSelectionAssistant.Views;

public partial class UpgradeTutorialWindow : Window
{
    public UpgradeTutorialWindow() => InitializeComponent();

    public bool Accepted { get; private set; }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }
}
