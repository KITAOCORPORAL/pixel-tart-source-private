using System.Diagnostics;
using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public partial class CandidateSelectionWindow : Window
{
    public CandidateSelectionWindow(IReadOnlyList<RawFileEntry> candidates)
    {
        InitializeComponent();
        DataContext = candidates;
        CandidatesGrid.SelectedIndex = 0;
    }

    public RawFileEntry? SelectedCandidate { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is not RawFileEntry candidate)
        {
            MessageBox.Show(this, "请选择一个 RAW 候选文件。", Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedCandidate = candidate;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is RawFileEntry candidate && File.Exists(candidate.FullPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{candidate.FullPath}\"") { UseShellExecute = true });
        }
    }
}
