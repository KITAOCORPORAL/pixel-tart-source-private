using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public partial class DocumentDropChoiceWindow : Window
{
    public DocumentDropChoiceWindow() => InitializeComponent();
    public BookingDocumentLinkMode? Choice { get; private set; }
    private void Reference_Click(object sender, RoutedEventArgs e) { Choice = BookingDocumentLinkMode.Reference; DialogResult = true; }
    private void ManagedCopy_Click(object sender, RoutedEventArgs e) { Choice = BookingDocumentLinkMode.ManagedCopy; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { Choice = null; DialogResult = false; }
}
