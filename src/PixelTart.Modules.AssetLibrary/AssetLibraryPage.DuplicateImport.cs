using System.IO;
using System.Windows;
using RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage
{
    private TaskCompletionSource<DuplicateImportChoice>? _duplicateImportCompletion;
    private async Task<DuplicateImportChoice> ShowDuplicateImportAsync(DuplicateImportPrompt prompt, CancellationToken token)
    {
        if (_duplicateImportCompletion is not null) return DuplicateImportChoice.Skip;
        var completion = new TaskCompletionSource<DuplicateImportChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        _duplicateImportCompletion = completion;
        DuplicateImportName.Text = Path.GetFileName(prompt.SourcePath);
        DuplicateIncomingName.Text = "待导入 · " + Path.GetFileName(prompt.SourcePath);
        DuplicateExistingName.Text = "已有素材 · " + prompt.Existing.DisplayName;
        AsyncThumbnail.SetSourcePath(DuplicateIncomingImage, prompt.SourcePath);
        AsyncThumbnail.SetSourcePath(DuplicateExistingImage, _viewModel.GetDisplaySourcePath(prompt.Existing));
        DuplicateImportComparison.Visibility = Visibility.Collapsed;
        DuplicateImportSurface.Visibility = Visibility.Visible;
        DuplicateSkipButton.Focus();
        using var registration = token.Register(() => completion.TrySetCanceled(token));
        try { return await completion.Task; }
        finally
        {
            _duplicateImportCompletion = null; DuplicateImportSurface.Visibility = Visibility.Collapsed;
            AsyncThumbnail.SetSourcePath(DuplicateIncomingImage, null); AsyncThumbnail.SetSourcePath(DuplicateExistingImage, null);
        }
    }
    private void DuplicateSkip_Click(object sender, RoutedEventArgs e) => _duplicateImportCompletion?.TrySetResult(DuplicateImportChoice.Skip);
    private void DuplicateAnyway_Click(object sender, RoutedEventArgs e) => _duplicateImportCompletion?.TrySetResult(DuplicateImportChoice.ImportAnyway);
    private void DuplicateView_Click(object sender, RoutedEventArgs e) => DuplicateImportComparison.Visibility = Visibility.Visible;
}
