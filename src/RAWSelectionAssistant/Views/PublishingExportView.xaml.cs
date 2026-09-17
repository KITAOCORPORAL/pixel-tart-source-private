using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class PublishingExportView : UserControl
{
    public PublishingExportView() => InitializeComponent();
    private void OnPreviewDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not PublishingExportViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        foreach (var path in (string[])e.Data.GetData(DataFormats.FileDrop)!) { if (Directory.Exists(path)) viewModel.AddFolder(path); else viewModel.AddFiles([path]); }
    }
}
