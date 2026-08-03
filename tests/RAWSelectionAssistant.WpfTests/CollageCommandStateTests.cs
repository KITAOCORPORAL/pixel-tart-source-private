using System.IO;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CollageCommandStateTests
{
    [TestMethod]
    public void ImportingPhotos_EnablesExportCommand()
    {
        using var temp = new TempDirectory();
        var files = Enumerable.Range(1, 4).Select(index => temp.CreateFile($"photo-{index}.png")).ToArray();
        var viewModel = new CollageViewModel(new CollageExportService(), new StubDialogService());

        Assert.IsFalse(viewModel.ExportCommand.CanExecute(null));
        viewModel.AddPaths(files);

        Assert.HasCount(4, viewModel.Images);
        Assert.IsTrue(viewModel.ExportCommand.CanExecute(null));
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
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => candidates.FirstOrDefault();
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.WpfTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public string CreateFile(string name) { var path = System.IO.Path.Combine(Path, name); File.WriteAllBytes(path, [1, 2, 3]); return path; }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
