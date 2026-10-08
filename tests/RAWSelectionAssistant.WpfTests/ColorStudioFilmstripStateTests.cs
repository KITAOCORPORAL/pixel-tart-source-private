using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorStudioFilmstripStateTests
{
    [TestMethod]
    public Task SyncDialogFreezesSourceTargetsAndCountsAcrossFilterAndActiveChanges() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-FrozenSync-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
            await workspace.LoadTargetAsync(SaveImage(root, "source.png", 100));
            var source = workspace.ActiveTarget!; source.Rating = 5; workspace.Editor.Exposure = .75;
            Assert.IsFalse(workspace.OpenAdjustmentCopyCommand.CanExecute(null), "Only the source selected means zero destinations.");
            var target = new ReferenceTargetItem(SaveImage(root, "target.png", 120)) { IsSelected = true, Rating = 5 };
            var hidden = new ReferenceTargetItem(SaveImage(root, "hidden.png", 140)) { IsSelected = true, Rating = 1 };
            workspace.Targets.Add(target); workspace.Targets.Add(hidden); workspace.MinimumRating = 4;
            workspace.OpenAdjustmentCopyCommand.Execute(null);
            var summary = workspace.PendingSyncSummary;
            Assert.AreEqual(1, workspace.PendingSyncTargetCount);
            target.IsSelected = false; workspace.MinimumRating = 0;
            await workspace.LoadTargetAsync(hidden.Path); workspace.Editor.Exposure = -1;
            Assert.AreEqual(summary, workspace.PendingSyncSummary);
            workspace.ConfirmAdjustmentCopyCommand.Execute(null);
            Assert.AreEqual(.75, target.ColorAdjustmentStackSnapshot!.Nodes.Single(x => x.Type == ColorStudioNodeType.Develop).NumericParameters["exposure"]);
            Assert.AreEqual(-1, workspace.Editor.Exposure, "The new current photograph must not become the sync source or destination.");
            Assert.AreEqual(5, target.Rating);
            await workspace.LoadTargetAsync(source.Path);
            source.IsSelected = false; target.IsSelected = true; hidden.IsSelected = false;
            workspace.OpenNodeSyncCommand.Execute(null);
            Assert.AreEqual(1, workspace.PendingSyncTargetCount, "Source need not be selected.");
            workspace.CancelNodeSyncCommand.Execute(null);
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public Task FilteredSelectionRangeAndHiddenTargetsUseOneVisibleOrder() => RunSta(async () =>
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
        for (var i = 0; i < 6; i++) workspace.Targets.Add(new ReferenceTargetItem($"{i}.jpg") { Rating = i % 2 == 0 ? 5 : 1, ColorLabel = "蓝" });
        workspace.Targets[1].IsSelected = true; workspace.MinimumRating = 4;
        CollectionAssert.AreEqual(new[] { "0.jpg", "2.jpg", "4.jpg" }, workspace.VisibleTargets.Select(x => x.FileName).ToArray());
        workspace.SelectVisibleFilmstripTarget(0); workspace.SelectVisibleFilmstripTarget(2, shift: true);
        CollectionAssert.AreEqual(new[] { "0.jpg", "2.jpg", "4.jpg" }, workspace.SelectedTargets.Select(x => x.FileName).ToArray());
        Assert.AreEqual(1, workspace.HiddenSelectedTargetCount); Assert.AreEqual(4, workspace.AllSelectedTargets.Count());
        workspace.SetSelectedRatingCommand.Execute(3);
        Assert.AreEqual(0, workspace.VisibleTargets.Count); Assert.AreEqual(4, workspace.HiddenSelectedTargetCount);
        Assert.IsFalse(workspace.ExportSelectedCommand.CanExecute(null));
        Assert.AreEqual(1, workspace.Targets[1].Rating, "Hidden selections must not receive a visible selection action.");
        workspace.ClearFilmstripFiltersCommand.Execute(null); Assert.AreEqual(6, workspace.VisibleTargets.Count); Assert.AreEqual(4, workspace.SelectedTargetCount);
        workspace.FilterScope = "Selected"; workspace.SelectVisibleFilmstripTarget(0, control: true);
        Assert.AreEqual(3, workspace.VisibleTargets.Count); Assert.AreEqual(3, workspace.SelectedTargetCount);
        await Task.CompletedTask;
    });
    [TestMethod]
    public Task PerTargetUndoDoesNotImportPreviousPhotographsAdjustments() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        editor.ApplyTargetSnapshot(null, null, null, false, a); editor.Exposure = 1; editor.Exposure = 2;
        var aStack = editor.AdjustmentStack.DeepClone();
        editor.ApplyTargetSnapshot(null, null, null, false, b);
        Assert.AreEqual(0, editor.Exposure); Assert.IsFalse(editor.UndoAdjustmentCommand.CanExecute(null));
        editor.Brightness = 30; editor.UndoAdjustmentCommand.Execute(null); Assert.AreEqual(0, editor.Brightness); Assert.AreEqual(0, editor.Exposure);
        editor.ApplyTargetSnapshot(null, null, aStack, false, a); Assert.AreEqual(2, editor.Exposure);
        editor.UndoAdjustmentCommand.Execute(null); Assert.AreEqual(1, editor.Exposure); Assert.AreEqual(0, editor.Brightness);
        editor.RedoAdjustmentCommand.Execute(null); Assert.AreEqual(2, editor.Exposure);
        await Task.CompletedTask;
    });
    [TestMethod]
    public Task SessionReloadRestoresFilteredBatchAndActiveImageWithoutRewritingFiles() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-FilmstripSession-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var a = SaveImage(root, "a.png", 60); var b = SaveImage(root, "b.png", 130);
            var hashes = new[] { File.ReadAllBytes(a), File.ReadAllBytes(b) };
            var path = Path.Combine(root, "batch.ptstudio.json");
            using (var first = new ReferenceColorWorkspaceViewModel(new Dialogs()))
            {
                await first.LoadTargetAsync(a); first.Editor.Exposure = .6; first.ActiveTarget!.Rating = 4; first.ActiveTarget.ColorLabel = "蓝";
                await first.LoadTargetAsync(b); first.Editor.Exposure = -.4; first.ActiveTarget!.Rating = 2; first.ActiveTarget.ColorLabel = "红";
                first.FilterScope = "Png"; first.MinimumRating = 3; first.ColorLabelFilter = "蓝";
                await first.SaveSessionAsync(path);
            }
            using var restored = new ReferenceColorWorkspaceViewModel(new Dialogs()); await restored.LoadSessionAsync(path);
            Assert.AreEqual(2, restored.Targets.Count); Assert.AreEqual(b, restored.ActiveTarget!.Path);
            Assert.AreEqual(-.4, restored.Editor.Exposure); Assert.AreEqual("蓝", restored.ColorLabelFilter); Assert.AreEqual(3, restored.MinimumRating);
            Assert.AreEqual(1, restored.VisibleTargetCount); Assert.AreEqual(a, restored.VisibleTargets[0].Path); Assert.AreEqual(1, restored.HiddenSelectedTargetCount);
            CollectionAssert.AreEqual(hashes[0], File.ReadAllBytes(a)); CollectionAssert.AreEqual(hashes[1], File.ReadAllBytes(b));
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public Task SyncExcludesSourceAndHiddenTargetsAndBatchUndoRestoresTargets() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-BatchUndo-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
            await workspace.LoadTargetAsync(SaveImage(root, "source.png", 100)); workspace.Editor.Exposure = 1;
            workspace.ActiveTarget!.Rating = 5;
            var target = new ReferenceTargetItem("target.jpg") { IsSelected = true, Rating = 5, ColorLabel = "蓝" };
            var hidden = new ReferenceTargetItem("hidden.jpg") { IsSelected = true, Rating = 1, ColorLabel = "红" };
            workspace.Targets.Add(target); workspace.Targets.Add(hidden); workspace.MinimumRating = 4;
            Assert.AreEqual(1, workspace.SyncDestinationTargets.Count);
            workspace.SyncSelectedCommand.Execute(null);
            Assert.AreEqual(1, target.ColorAdjustmentStackSnapshot!.Nodes.Single(x => x.Type == ColorStudioNodeType.Develop).NumericParameters["exposure"]);
            Assert.IsNull(hidden.ColorAdjustmentStackSnapshot); Assert.AreEqual("蓝", target.ColorLabel); Assert.AreEqual(5, target.Rating);
            workspace.UndoBatchAdjustmentCommand.Execute(null); Assert.IsNull(target.ColorAdjustmentStackSnapshot); Assert.AreEqual(1, workspace.Editor.Exposure);
            workspace.RedoBatchAdjustmentCommand.Execute(null); Assert.IsNotNull(target.ColorAdjustmentStackSnapshot);
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public void V4RefusesUnsupportedStackInsteadOfDroppingDevelopOrFilm()
    {
        var reference = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "仿色");
        Assert.IsTrue(TetherReferenceModeViewModel.SupportsExperimentalV4Stack(new([reference]), null));
        Assert.IsFalse(TetherReferenceModeViewModel.SupportsExperimentalV4Stack(new([reference, new(Guid.NewGuid(), ColorStudioNodeType.Develop, "曝光")]), null));
        Assert.IsFalse(TetherReferenceModeViewModel.SupportsExperimentalV4Stack(new([reference]), new PixelTartFilmSettings { Enabled = true }));
        Assert.IsFalse(TetherReferenceModeViewModel.SupportsExperimentalV4Stack(new([reference, reference with { Id = Guid.NewGuid() }]), null));
    }
    [TestMethod]
    public Task InvalidSessionAndMissingSourcesDoNotDiscardStoredAdjustments() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-MissingSession-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
            var path = SaveImage(root, "active.png", 80); await workspace.LoadTargetAsync(path); workspace.Editor.Exposure = .8;
            var invalid = Path.Combine(root, "invalid.json"); await File.WriteAllTextAsync(invalid, "{\"version\":999,\"targets\":[]}");
            await Assert.ThrowsAsync<InvalidDataException>(() => workspace.LoadSessionAsync(invalid));
            Assert.AreEqual(path, workspace.ActiveTarget!.Path); Assert.AreEqual(.8, workspace.Editor.Exposure);
            var session = Path.Combine(root, "missing.ptstudio.json"); await workspace.SaveSessionAsync(session); File.Delete(path);
            await workspace.LoadSessionAsync(session);
            Assert.IsNull(workspace.ActiveTarget); Assert.HasCount(1, workspace.Targets); Assert.AreEqual(ReferenceTargetStatus.Failed, workspace.Targets[0].Status);
            Assert.AreEqual(.8, workspace.Targets[0].ColorAdjustmentStackSnapshot!.Nodes.Single(x => x.Type == ColorStudioNodeType.Develop).NumericParameters["exposure"]);
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public Task SyncRefreshesActiveEditorAndNewProcessingVersionSurvivesSnapshot() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ActiveSync-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
            await workspace.LoadTargetAsync(SaveImage(root, "active.png", 80));
            var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.Develop, "曝光", NumericParameters: new Dictionary<string, double> { ["exposure"] = .5 });
            var stack = new ColorAdjustmentStack([node], ProcessingVersion: 2);
            workspace.SyncSelectedColorNodes(stack, new HashSet<Guid> { node.Id }, [workspace.ActiveTarget!]);
            Assert.AreEqual(.5, workspace.Editor.Exposure); Assert.AreEqual(2, workspace.Editor.AdjustmentStack.ProcessingVersion);
            workspace.UndoBatchAdjustmentCommand.Execute(null); Assert.AreEqual(0, workspace.Editor.Exposure);
            workspace.RedoBatchAdjustmentCommand.Execute(null); Assert.AreEqual(.5, workspace.Editor.Exposure);
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public Task AdjustedThumbnailsFollowFrozenLatestStackForActiveAndSyncedTargets() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ThumbnailState-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
            var a = SaveImage(root, "a.png", 80); var b = SaveImage(root, "b.png", 80);
            await workspace.LoadTargetAsync(a); var first = workspace.ActiveTarget!;
            await workspace.LoadTargetAsync(b); var active = workspace.ActiveTarget!;
            await workspace.FlushThumbnailsAsync(); var original = Pixel(first.Thumbnail!);
            workspace.Editor.Exposure = 1; workspace.SyncSelectedCommand.Execute(null);
            workspace.Editor.Exposure = -.5; workspace.SyncSelectedCommand.Execute(null);
            await workspace.FlushThumbnailsAsync();
            Assert.IsLessThan(original, Pixel(first.Thumbnail!), "The last synchronized negative exposure must reach the inactive thumbnail.");
            Assert.AreEqual(Pixel(active.Thumbnail!), Pixel(first.Thumbnail!));
            StringAssert.Contains(first.ThumbnailStatus, "当前调整后");
            workspace.Editor.Exposure = 1; await workspace.FlushThumbnailsAsync();
            Assert.IsGreaterThan(original, Pixel(active.Thumbnail!), "The active edit must refresh its thumbnail without changing target.");
            Assert.IsLessThan(original, Pixel(first.Thumbnail!), "Editing the active image must not alter the inactive frozen stack.");
            CollectionAssert.AreEqual(File.ReadAllBytes(a), File.ReadAllBytes(b), "Thumbnail rendering must not write source images.");
        }
        finally { Directory.Delete(root, true); }
        static byte Pixel(BitmapSource image)
        {
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var values = new byte[converted.PixelWidth * converted.PixelHeight * 4];
            converted.CopyPixels(values, converted.PixelWidth * 4, 0); return values[2];
        }
    });
    private static string SaveImage(string root, string name, byte value)
    {
        var path = Path.Combine(root, name); var bitmap = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Rgb24, null, Enumerable.Repeat(value, 16 * 16 * 3).ToArray(), 48);
        StudioQuickExport.Encode(bitmap, path, path); return path;
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false; public HelpAction ShowHelp() => HelpAction.None; public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false; public void RevealFile(string path) { }
    }
}
