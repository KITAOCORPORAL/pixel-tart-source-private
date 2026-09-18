using System.Windows;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceAdjustmentSaveDialog : Window
{
    private readonly string _currentName;
    public ReferenceAdjustmentSaveDialog(string currentName)
    {
        InitializeComponent(); _currentName = currentName;
        NameInput.Text = $"{currentName} 现场调整"; NameInput.SelectAll();
    }

    public ReferenceAdjustmentSaveChoice? Choice { get; private set; }
    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (NameInput is null) return;
        NameInput.IsEnabled = SaveAsNewOption.IsChecked == true;
        NameInput.Text = SaveAsNewOption.IsChecked == true ? $"{_currentName} 现场调整" : _currentName;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var update = UpdateExistingOption.IsChecked == true;
        var name = update ? _currentName : NameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) { NameInput.Focus(); return; }
        Choice = new(update, name, SetDefaultOption.IsChecked == true); DialogResult = true;
    }
}
