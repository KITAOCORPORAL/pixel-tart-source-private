using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class OrganizeCommandStateTests
{
    [TestMethod]
    public async Task ImportingPhotos_EnablesPreviewAndPreviewEnablesExecute()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/photo.jpg", [1, 2, 3, 4]);
        var viewModel = new OrganizePhotosViewModel(new OrganizeService(), new StubDialogService())
        {
            OutputPath = temp.Combine("output")
        };

        var previewStateChanges = 0;
        viewModel.PreviewPlanCommand.CanExecuteChanged += (_, _) => previewStateChanges++;

        Assert.IsFalse(viewModel.PreviewPlanCommand.CanExecute(null));
        await viewModel.AddPathsAsync([source]);

        Assert.HasCount(1, viewModel.Photos);
        Assert.IsGreaterThan(0, viewModel.Groups.Count);
        Assert.IsTrue(viewModel.PreviewPlanCommand.CanExecute(null));
        Assert.IsGreaterThan(0, previewStateChanges);

        viewModel.PreviewPlanCommand.Execute(null);

        Assert.IsNotNull(viewModel.CurrentPlan);
        Assert.IsTrue(viewModel.ExecuteCommand.CanExecute(null));
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { }
        public void ShowError(string message) => Assert.Fail(message);
        public bool Confirm(string message, string title) => true;
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => candidates.FirstOrDefault();
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.WpfTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

        public string CreateFile(string relativePath, byte[] bytes)
        {
            var path = Combine(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
