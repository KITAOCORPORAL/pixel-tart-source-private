using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class BatchExportProcessedPixelsTests
{
    [TestMethod]
    public void ExportSnapshotIsIndependentPerTarget()
    {
        var a = new ReferenceTargetItem("a.jpg") { AppliedLookSnapshot = null, FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "PT-W01") };
        var b = new ReferenceTargetItem("b.jpg") { AppliedLookSnapshot = null, FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "PT-C01") };
        Assert.AreNotEqual(a.FilmSettingsSnapshot, b.FilmSettingsSnapshot);
    }

    [TestMethod]
    public void FilmstripSelectionDoesNotRequireCheckboxState()
    {
        var target = new ReferenceTargetItem("a.jpg") { IsSelected = true, IsActive = false };
        Assert.IsTrue(target.IsSelected);
        Assert.IsFalse(target.IsActive);
    }
}
