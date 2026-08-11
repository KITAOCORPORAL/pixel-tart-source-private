using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class BatchCompressionModal : UserControl
{
    public BatchCompressionModal() => InitializeComponent();

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not BatchCompressionViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) viewModel.AddFiles(paths);
        e.Handled = true;
    }
}
