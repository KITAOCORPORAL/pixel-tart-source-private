using System.Windows;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioColorBalanceWheelTests
{
    [TestMethod]
    public void PointerMapsToExistingHueAndAmountContract()
    {
        Assert.AreEqual((0d, 0d), StudioColorBalanceWheel.FromPosition(new Vector(), 50));
        Assert.AreEqual((0d, 50d), StudioColorBalanceWheel.FromPosition(new Vector(25, 0), 50));
        Assert.AreEqual((90d, 100d), StudioColorBalanceWheel.FromPosition(new Vector(0, -80), 50));
        Assert.AreEqual((180d, 100d), StudioColorBalanceWheel.FromPosition(new Vector(-50, 0), 50));
        Assert.AreEqual((270d, 100d), StudioColorBalanceWheel.FromPosition(new Vector(0, 50), 50));
        Assert.AreEqual((0d, 0d), StudioColorBalanceWheel.FromPosition(new Vector(double.NaN, 0), 50));
    }
    [TestMethod]
    public void LevelsMidpointUsesExistingGammaExponentAndFiniteLimits()
    {
        Assert.AreEqual(1, StudioLevelsGraph.GammaForPosition(.5), 1e-10);
        Assert.AreEqual(2, StudioLevelsGraph.GammaForPosition(.25), 1e-10);
        Assert.AreEqual(.5, StudioLevelsGraph.GammaForPosition(Math.Sqrt(.5)), 1e-10);
        Assert.AreEqual(5, StudioLevelsGraph.GammaForPosition(-1));
        Assert.AreEqual(.1, StudioLevelsGraph.GammaForPosition(2));
    }
    [TestMethod]
    public Task ExportParentReusesTheActualExistingPipelineCommands() => RuntimeCorrectionWpfTests.RunSta(async () =>
    {
        RuntimeCorrectionWpfTests.EnsureTestApplication();
        using var workspace = new RAWSelectionAssistant.ViewModels.ReferenceColorWorkspaceViewModel(new MenuDialogs());
        var menu = StudioExportMenu.CreateMenu(workspace);
        var actions = menu.Items.OfType<System.Windows.Controls.MenuItem>().Where(i => i.Command is not null).ToArray();
        Assert.HasCount(3, actions);
        Assert.AreSame(workspace.ExportSelectedCommand, actions[0].Command);
        Assert.AreSame(workspace.ExportAllCommand, actions[1].Command);
        Assert.AreSame(workspace.PreparePublishingCommand, actions[2].Command);
        await Task.CompletedTask;
    });
    private sealed class MenuDialogs : RAWSelectionAssistant.Services.IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public RAWSelectionAssistant.Services.HelpAction ShowHelp() => RAWSelectionAssistant.Services.HelpAction.None;
        public void ShowFeedback() { }
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
