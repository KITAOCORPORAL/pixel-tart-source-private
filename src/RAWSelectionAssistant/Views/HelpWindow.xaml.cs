using System.Windows;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();
    public HelpAction SelectedAction { get; private set; }

    private void Replay_Click(object sender, RoutedEventArgs e) => Choose(HelpAction.ReplayTutorial);
    private void Reset_Click(object sender, RoutedEventArgs e) => Choose(HelpAction.ResetTutorialData);
    private void Delete_Click(object sender, RoutedEventArgs e) => Choose(HelpAction.DeleteTutorialData);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Choose(HelpAction action) { SelectedAction = action; DialogResult = true; }
}
