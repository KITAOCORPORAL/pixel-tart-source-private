using System.IO;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class BatchExportProcessedPixelsTests
{
    [TestMethod]
    public async Task CopiedAdjustmentsRemainFrozenAndProtectFilmstripMetadata()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-RUX-D-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder), new PixelBackend());
        var first = new ReferenceTargetItem(CreatePng(folder, "first", 70, 80, 90)) { AppliedLookSnapshot = Look("red"), IsSelected = true };
        var second = new ReferenceTargetItem(CreatePng(folder, "second", 30, 90, 120)) { AppliedLookSnapshot = Look("blue"), Rating = 4, ColorLabel = "蓝" };
        workspace.Targets.Add(first); workspace.Targets.Add(second);
        await workspace.ActivateTargetCommand.ExecuteAsync(first);
        Assert.IsTrue(workspace.CopyAdjustmentsCommand.CanExecute(null));
        workspace.CopyCurrentAdjustments();
        first.IsSelected = false; second.IsSelected = true;
        await workspace.ActivateTargetCommand.ExecuteAsync(second);
        workspace.ApplyCopiedAdjustments();
        Assert.AreEqual("red", second.AppliedLookSnapshot!.Name);
        Assert.AreEqual(4, second.Rating); Assert.AreEqual("蓝", second.ColorLabel);
        Assert.AreEqual("red", workspace.Editor.SelectedLook!.Name);
    }

    [TestMethod]
    public async Task PublishingReceivesProcessedFrozenTargetsInsteadOfOriginalFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-RUX-D-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder), new PixelBackend());
        var first = new ReferenceTargetItem(CreatePng(folder, "first", 70, 80, 90)) { AppliedLookSnapshot = Look("red"), IsSelected = true };
        var second = new ReferenceTargetItem(CreatePng(folder, "second", 30, 90, 120)) { AppliedLookSnapshot = Look("blue"), IsSelected = true };
        workspace.Targets.Add(first); workspace.Targets.Add(second);
        IReadOnlyList<string>? paths = null;
        workspace.OpenPublishing = values => paths = values;
        await workspace.PreparePublishingCommand.ExecuteAsync(null);
        Assert.IsNotNull(paths); Assert.HasCount(2, paths);
        Assert.IsTrue(paths.All(path => File.Exists(path) && path != first.Path && path != second.Path));
        Assert.IsFalse(workspace.IsExporting);
    }

    [TestMethod]
    public async Task AsyncCommandStillRejectsReentryByDefault()
    {
        var release = new TaskCompletionSource(); var calls = 0;
        var command = new RAWSelectionAssistant.Utilities.AsyncRelayCommand(async _ => { calls++; await release.Task; });
        var running = command.ExecuteAsync(null);
        Assert.IsFalse(command.CanExecute(null));
        await command.ExecuteAsync(null); Assert.AreEqual(1, calls);
        release.SetResult(); await running;
        Assert.IsTrue(command.CanExecute(null));
    }
    [TestMethod]
    public async Task FilmstripRapidActivationKeepsLatestClickAndReusesUnchangedPreview()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-Activation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var backend = new PixelBackend();
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder), backend);
            var first = new ReferenceTargetItem(CreatePng(folder, "first", 70, 80, 90)) { AppliedLookSnapshot = Look("red") };
            var second = new ReferenceTargetItem(CreatePng(folder, "second", 30, 90, 120)) { AppliedLookSnapshot = Look("blue") };
            workspace.Targets.Add(first); workspace.Targets.Add(second);
            await workspace.ActivateTargetCommand.ExecuteAsync(first);
            var firstImage = workspace.Editor.SourceImage;
            var firstFrame = workspace.Editor.MatchedImage;
            var firstRenderCount = backend.RenderedLooks.Count;
            await workspace.Editor.ApplyCommand.ExecuteAsync(null);
            Assert.HasCount(firstRenderCount, backend.RenderedLooks, "Unchanged preview must use its cached result.");
            Assert.AreSame(firstFrame, workspace.Editor.MatchedImage);
            backend.BlockAfterFirstRender = true;
            var secondActivation = workspace.ActivateTargetCommand.ExecuteAsync(second);
            await backend.SecondRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(workspace.ActivateTargetCommand.CanExecute(first), "An in-flight render must not disable target selection.");
            var latestActivation = workspace.ActivateTargetCommand.ExecuteAsync(first);
            await Task.WhenAll(secondActivation, latestActivation).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(first, workspace.ActiveTarget);
            Assert.AreSame(firstImage, workspace.Editor.SourceImage);
            Assert.AreSame(firstFrame, workspace.Editor.MatchedImage);
            Assert.IsTrue(first.IsActive); Assert.IsFalse(second.IsActive);
            Assert.IsFalse(workspace.IsLoading); Assert.IsFalse(workspace.Editor.IsBusy);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task CachedCurrentFrameDoesNotStayBusyWhileCancelledRenderUnwinds()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-RetiredPreview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var backend = new PixelBackend { DelayCancelledRenderExit = true };
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder), backend);
            var first = new ReferenceTargetItem(CreatePng(folder, "first", 30, 40, 50)) { AppliedLookSnapshot = Look("red") };
            var second = new ReferenceTargetItem(CreatePng(folder, "second", 70, 80, 90)) { AppliedLookSnapshot = Look("blue") };
            workspace.Targets.Add(first); workspace.Targets.Add(second);
            await workspace.ActivateTargetCommand.ExecuteAsync(first);
            var frame = workspace.Editor.MatchedImage;
            backend.BlockAfterFirstRender = true;
            var retired = workspace.ActivateTargetCommand.ExecuteAsync(second);
            await backend.SecondRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await workspace.ActivateTargetCommand.ExecuteAsync(first).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(frame, workspace.Editor.MatchedImage);
            Assert.IsFalse(workspace.Editor.IsBusy, "The displayed cached frame is ready even while retired work unwinds.");
            Assert.IsFalse(retired.IsCompleted);
            backend.ReleaseCancelledRender.TrySetResult();
            await retired.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(frame, workspace.Editor.MatchedImage);
            Assert.IsFalse(workspace.Editor.IsBusy);
        }
        finally { backend.ReleaseCancelledRender.TrySetResult(); Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task PreviewCacheInvalidatesWhenReferenceFileChanges()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-ReferenceCache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var backend = new PixelBackend();
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder), backend);
            var referencePath = CreatePng(folder, "reference", 80, 90, 100);
            var look = Look("red");
            look = look with { ReferenceSources = [look.ReferenceSources[0] with { SourcePath = referencePath }] };
            var target = new ReferenceTargetItem(CreatePng(folder, "target", 40, 50, 60)) { AppliedLookSnapshot = look };
            workspace.Targets.Add(target);
            await workspace.ActivateTargetCommand.ExecuteAsync(target);
            var before = backend.RenderedLooks.Count;
            await workspace.Editor.ApplyCommand.ExecuteAsync(null);
            Assert.HasCount(before, backend.RenderedLooks);
            File.SetLastWriteTimeUtc(referencePath, File.GetLastWriteTimeUtc(referencePath).AddMinutes(1));
            await workspace.Editor.ApplyCommand.ExecuteAsync(null);
            Assert.HasCount(before + 1, backend.RenderedLooks, "A changed reference must not reuse the cached frame.");
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ColorInspectionOnlyChangesPreviewAndClearsOnTargetSwitch()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PixelTart-Inspection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(folder));
            await workspace.LoadTargetAsync(CreatePng(folder, "red", 170, 30, 30));
            var stack = workspace.Editor.AdjustmentStack;
            await workspace.BuildColorSpaceModelAsync();
            workspace.HighlightImageSample(new VisualRgb24(170, 30, 30));
            Assert.IsNotEmpty(workspace.HighlightedPixels);
            Assert.AreSame(stack, workspace.Editor.AdjustmentStack);
            workspace.ClearColorSpaceHighlight(); Assert.IsEmpty(workspace.HighlightedPixels);
            workspace.HighlightCloudSelection(0); Assert.IsNotEmpty(workspace.HighlightedPixels);
            await workspace.LoadTargetAsync(CreatePng(folder, "blue", 30, 30, 170));
            Assert.IsNull(workspace.ColorSpaceModel); Assert.IsEmpty(workspace.HighlightedPixels);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task MissingReferenceImportKeepsValidFrameTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ReferenceFailure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var good = CreatePng(root, "good", 90, 100, 120);
            var dialogs = new FolderDialog(root) { Paths = [Path.Combine(root, "missing.png")] };
            using var workspace = new ReferenceColorWorkspaceViewModel(dialogs);
            await workspace.LoadTargetAsync(good); workspace.Editor.WorkspaceMode = "专业";
            await workspace.Editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            var frame = workspace.Editor.MatchedImage; Assert.IsNotNull(frame);
            await workspace.Editor.ImportExternalReferenceCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.AreSame(frame, workspace.Editor.MatchedImage); Assert.IsFalse(workspace.Editor.IsBusy);
            StringAssert.Contains(workspace.Editor.StatusText, "参考图无法读取");
            dialogs.Paths = [good];
            await workspace.Editor.ImportExternalReferenceCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.IsNotNull(workspace.Editor.SelectedLook);
            await workspace.Editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.IsFalse(workspace.Editor.HasError); Assert.IsFalse(workspace.Editor.IsBusy);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public async Task CorruptTargetAndFailedExportKeepFrameAndRecoverTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var good = CreatePng(root, "good", 80, 90, 100);
            var corrupt = Path.Combine(root, "corrupt.png"); await File.WriteAllTextAsync(corrupt, "not an image");
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(root));
            await workspace.LoadTargetAsync(good); workspace.Editor.WorkspaceMode = "专业";
            await workspace.Editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            var frame = workspace.Editor.MatchedImage; Assert.IsNotNull(frame);
            await workspace.LoadTargetAsync(corrupt).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.AreSame(frame, workspace.Editor.MatchedImage); Assert.AreEqual(good, workspace.ActiveTarget!.Path);
            Assert.IsFalse(workspace.IsLoading);
            Assert.AreSame(workspace.Targets.Single(t => t.Path == corrupt), workspace.FailedTarget);
            Assert.IsTrue(workspace.RetryFailedTargetCommand.CanExecute(null));
            await workspace.ExportAllCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.IsFalse(workspace.IsExporting); Assert.AreSame(frame, workspace.Editor.MatchedImage);
            Assert.AreEqual(ReferenceExportStatus.Failed, workspace.Targets.Single(t => t.Path == corrupt).ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, workspace.Targets.Single(t => t.Path == good).ExportStatus);
            StringAssert.Contains(workspace.ExportFailureSummary, "失败 1 张");
            Assert.IsTrue(workspace.RetryFailedExportCommand.CanExecute(null));
            Assert.IsFalse(Directory.EnumerateFiles(root, "*.tmp").Any());
            File.Copy(good, corrupt, true);
            await workspace.RetryFailedTargetCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.AreEqual(corrupt, workspace.ActiveTarget!.Path); Assert.IsFalse(workspace.IsLoading);
            await workspace.RetryFailedExportCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.AreEqual(ReferenceExportStatus.Succeeded, workspace.Targets.Single(t => t.Path == corrupt).ExportStatus);
            Assert.IsEmpty(workspace.ExportFailureSummary);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void BatchSyncSelectionToastAndIsolationTests()
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(Path.GetTempPath()));
        workspace.Editor.WorkspaceMode = "专业";
        var existing = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "保留", NumericParameters: new Dictionary<string, double> { ["hue"] = 19 });
        var a = new ReferenceTargetItem("a.jpg") { IsSelected = true, ColorAdjustmentStackSnapshot = new ColorAdjustmentStack([existing]) };
        var b = new ReferenceTargetItem("b.jpg") { IsSelected = true, ColorAdjustmentStackSnapshot = new ColorAdjustmentStack([existing]) };
        workspace.Targets.Add(a); workspace.Targets.Add(b);
        Assert.IsTrue(workspace.CanSyncSelectedNodes);
        workspace.OpenNodeSyncCommand.Execute(null);
        foreach (var choice in workspace.NodeSyncChoices.Skip(1)) choice.Selected = false;
        workspace.ConfirmNodeSyncCommand.Execute(null);
        Assert.IsFalse(workspace.NodeSyncOpen); StringAssert.Contains(workspace.SyncFeedback, "1 个调整到 2 张照片");
        Assert.AreEqual(19, a.ColorAdjustmentStackSnapshot!.Nodes[0].NumericParameters["hue"]);
        Assert.AreNotSame(a.ColorAdjustmentStackSnapshot.Nodes[1].NumericParameters, b.ColorAdjustmentStackSnapshot!.Nodes[1].NumericParameters);
        b.IsSelected = false; Assert.IsFalse(workspace.CanSyncSelectedNodes);
    }

    [TestMethod]
    public void CopyApplyAdjustmentsUsesSelectedCategoriesAndProtectsAssetFields()
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(Path.GetTempPath()));
        workspace.Editor.WorkspaceMode = "专业";
        var first = new ReferenceTargetItem("first.jpg") { IsSelected = true, Rating = 5, ColorLabel = "红", ColorAdjustmentStackSnapshot = workspace.Editor.AdjustmentStack.DeepClone() };
        var second = new ReferenceTargetItem("second.jpg") { IsSelected = true, Rating = 2, ColorLabel = "蓝", ColorAdjustmentStackSnapshot = workspace.Editor.AdjustmentStack.DeepClone() };
        workspace.Targets.Add(first); workspace.Targets.Add(second);
        workspace.Editor.SelectedAdjustmentNode = workspace.Editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ColorRange);
        workspace.Editor.RangeHue = 77;
        workspace.OpenAdjustmentCopyCommand.Execute(null);
        foreach (var choice in workspace.AdjustmentSyncChoices) choice.Selected = choice.Type == ColorStudioNodeType.ColorRange;
        workspace.ConfirmAdjustmentCopyCommand.Execute(null);
        Assert.AreEqual(5, first.Rating); Assert.AreEqual("红", first.ColorLabel);
        Assert.AreEqual(2, second.Rating); Assert.AreEqual("蓝", second.ColorLabel);
        Assert.AreEqual(77, second.ColorAdjustmentStackSnapshot!.Nodes.Single(node => node.Type == ColorStudioNodeType.ColorRange).NumericParameters["hue"]);
    }
    [TestMethod]
    public async Task InactiveTargetsExportTheirFrozenProcessedPixelsNotActiveEditorPixels()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-BatchPixels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output");
            Directory.CreateDirectory(source); Directory.CreateDirectory(output);
            var red = new ReferenceTargetItem(CreatePng(source, "active", 40, 60, 80)) { IsSelected = true, IsActive = true, AppliedLookSnapshot = Look("red", 10), FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "red-film") };
            var green = new ReferenceTargetItem(CreatePng(source, "inactive-b", 40, 60, 80)) { IsSelected = true, AppliedLookSnapshot = Look("green", 20), FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "green-film") };
            var blue = new ReferenceTargetItem(CreatePng(source, "inactive-c", 40, 60, 80)) { IsSelected = true, AppliedLookSnapshot = Look("blue", 30), FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "blue-film") };
            var untouched = new ReferenceTargetItem(CreatePng(source, "unsynced", 40, 60, 80)) { IsSelected = true };
            var backend = new PixelBackend { FirstRender = () => { green.AppliedLookSnapshot = Look("yellow", 99); blue.FilmSettingsSnapshot = new PixelTartFilmSettings(Enabled: true, ProfileId: "wrong-film"); } };
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output), backend);
            workspace.Targets.Add(red); workspace.Targets.Add(green); workspace.Targets.Add(blue); workspace.Targets.Add(untouched);
            workspace.Editor.SelectedLook = Look("yellow", 99); // The active editor must not leak into other targets.

            await workspace.ExportAllCommand.ExecuteAsync(null);

            Assert.AreEqual(4, workspace.ExportCompleted);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, red.ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, green.ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, blue.ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, untouched.ExportStatus);
            AssertRgb(red.OutputPath!, 220, 20, 20);
            AssertRgb(green.OutputPath!, 20, 220, 20);
            AssertRgb(blue.OutputPath!, 20, 20, 220);
            AssertRgb(untouched.OutputPath!, 40, 60, 80);
            CollectionAssert.AreEqual(new[] { "red:10", "green:20", "blue:30" }, backend.RenderedLooks);
            CollectionAssert.AreEqual(new[] { "red-film", "green-film", "blue-film" }, backend.AppliedFilms);
            Assert.IsTrue(red.IsActive); Assert.IsFalse(green.IsActive); Assert.IsFalse(blue.IsActive);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RealCpuRendererProcessesActiveAndTwoNeverActivatedTargets()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RealCpuBatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output");
            Directory.CreateDirectory(source); Directory.CreateDirectory(output);
            var targets = new[]
            {
                new ReferenceTargetItem(CreatePng(source, "active", 100, 100, 100)) { IsSelected = true, IsActive = true, AppliedLookSnapshot = Look("warm", 100, 220, 85, 55) },
                new ReferenceTargetItem(CreatePng(source, "inactive-b", 100, 100, 100)) { IsSelected = true, AppliedLookSnapshot = Look("cool", 100, 55, 100, 220) },
                new ReferenceTargetItem(CreatePng(source, "inactive-c", 100, 100, 100)) { IsSelected = true, AppliedLookSnapshot = Look("green", 100, 55, 210, 75) }
            };
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output), new ReferenceLookPreviewService());
            foreach (var target in targets) workspace.Targets.Add(target);
            await workspace.ExportAllCommand.ExecuteAsync(null);
            foreach (var target in targets)
            {
                Assert.AreEqual(ReferenceExportStatus.Succeeded, target.ExportStatus, target.FileName);
                var sourcePixel = Pixels(Load(target.Path)); var outputPixel = Pixels(Load(target.OutputPath!));
                Assert.IsGreaterThan(5, new[] { 0, 1, 2 }.Max(channel => Math.Abs(sourcePixel[channel] - outputPixel[channel])), target.FileName);
            }
            Assert.IsTrue(targets[0].IsActive); Assert.IsFalse(targets[1].IsActive); Assert.IsFalse(targets[2].IsActive);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task ColorStudioInactiveTargetUsesFrozenStackAndSamePreviewRenderer()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ColorStack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = CreatePng(root, "source", 180, 90, 60);
            var output = Path.Combine(root, "output"); Directory.CreateDirectory(output);
            var nodeId = Guid.NewGuid();
            var stack = new ColorAdjustmentStack([new(nodeId, ColorStudioNodeType.ColorRange, "warm", true,
                new Dictionary<string, double> { ["hue"] = 70, ["saturation"] = 45, ["range"] = .1 }, [new(180, 90, 60)])]);
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output));
            var inactive = new ReferenceTargetItem(source) { IsSelected = true, ColorAdjustmentStackSnapshot = stack };
            workspace.Targets.Add(inactive);
            var preview = await workspace.Editor.PreviewColorStudioAsync(Load(source), stack, null);
            await workspace.ExportAllCommand.ExecuteAsync(null);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, inactive.ExportStatus);
            Assert.IsFalse(inactive.IsActive);
            var exported = Pixels(Load(inactive.OutputPath!)); var expected = Pixels(preview);
            Assert.IsGreaterThan(10, Math.Abs(exported[2] - 180) + Math.Abs(exported[1] - 90) + Math.Abs(exported[0] - 60));
            Assert.IsLessThanOrEqualTo(6, exported.Zip(expected, (a, b) => Math.Abs(a - b)).Max());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task ColorStudioThirtyHighResolutionTargetsRecordProcessedExportBaseline()
    {
        const int width = 2400, height = 1600, count = 30;
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ColorBaseline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "output"); Directory.CreateDirectory(output);
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4; pixels[offset] = (byte)(35 + x % 120); pixels[offset + 1] = (byte)(55 + y % 130); pixels[offset + 2] = (byte)(100 + (x + y) % 110); pixels[offset + 3] = 255;
            }
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            var path = Path.Combine(root, "fixture.png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(path)) encoder.Save(stream);
            var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.Film, "film", true,
                new Dictionary<string, double> { ["profile_amount"] = 40 })]);
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output));
            for (var index = 0; index < count; index++)
            {
                var targetPath = Path.Combine(root, $"fixture-{index:00}.png"); File.Copy(path, targetPath);
                workspace.Targets.Add(new ReferenceTargetItem(targetPath) { IsSelected = true, ColorAdjustmentStackSnapshot = stack });
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var before = Process.GetCurrentProcess().WorkingSet64; var watch = Stopwatch.StartNew();
            await workspace.ExportAllCommand.ExecuteAsync(null);
            watch.Stop();
            Assert.AreEqual(count, workspace.ExportCompleted);
            Assert.IsTrue(workspace.Targets.All(target => target.ExportStatus == ReferenceExportStatus.Succeeded));
            Assert.HasCount(count, Directory.GetFiles(output, "*_仿色.jpg"));
            TestContext?.WriteLine($"color_studio_batch_targets={count}; dimensions={width}x{height}; elapsed_ms={watch.Elapsed.TotalMilliseconds:F0}; working_set_before_mb={before / 1048576d:F1}; process_peak_mb={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d:F1}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void FilmstripSelectionDoesNotRequireActiveState()
    {
        var target = new ReferenceTargetItem("a.jpg") { IsSelected = true, IsActive = false };
        Assert.IsTrue(target.IsSelected);
        Assert.IsFalse(target.IsActive);
    }

    [TestMethod]
    public async Task ActiveTargetCurrentEditsFreezeWhenExportStarts()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ActiveFreeze-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = CreatePng(root, "active", 40, 60, 80);
            var output = Path.Combine(root, "output"); Directory.CreateDirectory(output);
            var backend = new PixelBackend();
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output), backend);
            await workspace.LoadTargetAsync(source);
            workspace.Editor.SelectedLook = Look("red", 10);
            workspace.Editor.MatchStrength = 73;
            workspace.Editor.FilmEnabled = true;
            workspace.Editor.FilmProfileId = "active-film";
            await workspace.ExportAllCommand.ExecuteAsync(null);
            AssertRgb(workspace.ActiveTarget!.OutputPath!, 220, 20, 20);
            CollectionAssert.Contains(backend.RenderedLooks, "red:73");
            CollectionAssert.Contains(backend.AppliedFilms, "active-film");
            Assert.AreEqual(73d, workspace.ActiveTarget.AppliedLookSnapshot!.Parameters.MatchStrength);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void FilmstripClickModifiersKeepAnchorSelectionAndActiveIndependent()
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(Path.GetTempPath()));
        foreach (var index in Enumerable.Range(0, 5)) workspace.Targets.Add(new ReferenceTargetItem($"{index}.jpg"));
        workspace.Targets[0].IsActive = true;
        var anchor = workspace.SelectFilmstripTarget(1, -1, shift: false, control: false);
        AssertSelected(1);
        anchor = workspace.SelectFilmstripTarget(3, anchor, shift: false, control: true);
        AssertSelected(1, 3);
        anchor = workspace.SelectFilmstripTarget(3, anchor, shift: false, control: true);
        AssertSelected(1);
        anchor = workspace.SelectFilmstripTarget(4, anchor, shift: true, control: false);
        AssertSelected(3, 4);
        anchor = workspace.SelectFilmstripTarget(1, anchor, shift: true, control: true);
        AssertSelected(1, 2, 3, 4);
        Assert.AreEqual(3, anchor);
        Assert.IsTrue(workspace.Targets[0].IsActive);
        Assert.IsFalse(workspace.Targets[0].IsSelected);
        CollectionAssert.AreEqual(new[] { "1.jpg", "2.jpg", "3.jpg", "4.jpg" }, workspace.SelectedTargets.Select(item => item.FileName).ToArray());

        void AssertSelected(params int[] expected) => CollectionAssert.AreEqual(expected, workspace.Targets.Select((item, index) => (item, index)).Where(pair => pair.item.IsSelected).Select(pair => pair.index).ToArray());
    }

    [TestMethod]
    public void ActivatingUnsyncedTargetClearsPreviousPreviewLookAndFilm()
    {
        using var editor = new TetherReferenceModeViewModel(renderBackend: new PixelBackend());
        editor.SelectedLook = Look("red");
        editor.FilmEnabled = true;
        editor.ApplyTargetSnapshot(null, null);
        Assert.IsNull(editor.SelectedLook);
        Assert.IsFalse(editor.FilmEnabled);
    }

    [TestMethod]
    public async Task FullResolutionPreviewAndExportProduceTheSamePixels()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = CreatePng(root, "source", 40, 60, 80);
            var image = Load(path);
            var look = Look("parity", 70);
            var film = new PixelTartFilmSettings(Enabled: true, ProfileId: "PT-W01", ProfileAmount: 35);
            var backend = new ReferenceLookPreviewService();
            var preview = (await backend.RenderWithResultAsync(image, look)).Image;
            preview = await backend.ApplyFilmAsync(preview, film);
            using var editor = new TetherReferenceModeViewModel(renderBackend: backend);
            var export = await editor.ProcessForExportAsync(path, look, film, CancellationToken.None);
            var previewBytes = Pixels(preview); var exportBytes = Pixels(export);
            Assert.HasCount(previewBytes.Length, exportBytes);
            var maximumDelta = previewBytes.Zip(exportBytes, (a, b) => Math.Abs(a - b)).Max();
            Assert.IsLessThanOrEqualTo(0, maximumDelta, $"Actual maximum channel delta: {maximumDelta}; allowed: 0");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task PreviewExportParityMatrixRecordsJpegTiff16HighPrecisionAndIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ParityMatrix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = CreateJpeg(root, "matrix", 40, 60, 80);
            var source = Load(sourcePath);
            var look = Look("matrix", 65, 150, 110, 90);
            var backend = new ReferenceLookPreviewService();
            var preview = (await backend.RenderWithResultAsync(source, look)).Image;
            var export = await new TetherReferenceModeViewModel(renderBackend: backend).ProcessForExportAsync(sourcePath, look, null, CancellationToken.None);
            var rgbDelta = CompareBytes(Pixels(preview), Pixels(export));
            Assert.AreEqual(0, rgbDelta.Max, "This fixture compares identical-resolution service outputs.");

            var identity = look with { Parameters = look.Parameters with { MatchStrength = 0 } };
            var identityPreview = (await backend.RenderWithResultAsync(source, identity)).Image;
            var identityExport = await new TetherReferenceModeViewModel(renderBackend: backend).ProcessForExportAsync(sourcePath, identity, null, CancellationToken.None);
            var identityDelta = CompareBytes(Pixels(source), Pixels(identityPreview));
            Assert.AreEqual(0, identityDelta.Max);
            Assert.AreEqual(0, CompareBytes(Pixels(identityPreview), Pixels(identityExport)).Max);

            var evidence = Path.Combine(TestContext!.ResultsDirectory!, "synthetic-jpeg-service-parity");
            Directory.CreateDirectory(evidence);
            var matrix = new
            {
                sourceHead = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_SOURCE_HEAD") ?? "UNRECORDED",
                scope = "Synthetic constant JPEG, equal-resolution service comparison only; not corpus/proxy parity",
                cases = new object[]
                {
                    new { id = "V3-JPEG-8BIT", inputType = "JPEG 8-bit", meanDelta = rgbDelta.Mean, maxDelta = rgbDelta.Max,  tolerance = "max RGB channel delta <= 0", status = "PASS" },
                    new { id = "V3-TIFF16", status = "NOT_RUN", reason = "Not exercised by this fixture" },
                    new { id = "V3-HIGH-PRECISION", status = "NOT_RUN", reason = "Not exercised by this fixture" },
                    new { id = "V3-IDENTITY-0", inputType = "8-bit identity", meanDelta = identityDelta.Mean, maxDelta = identityDelta.Max,  tolerance = "zero color math delta", status = "PASS" },
                    new { id = "V4-EXPERIMENTAL", status = "NOT_RUN", reason = "Not exercised by this fixture" },
                    new { id = "REAL-RAW-CORPUS", status = "NOT_RUN", reason = "CORPUS_NOT_AVAILABLE" }
                }
            };
            await File.WriteAllTextAsync(Path.Combine(evidence, "PARITY_MATRIX.json"), System.Text.Json.JsonSerializer.Serialize(matrix, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(evidence, "PARITY_SUMMARY.md"), $"Synthetic JPEG equal-resolution service comparison: max channel delta={rgbDelta.Max}; 0% identity max delta={identityDelta.Max}. This test does not measure tone/color delta, TIFF16, high precision, ICC, proxy/full resolution, V4, or real RAW. Those cases are NOT_RUN here.");
            TestContext?.WriteLine($"parity_mean_delta={rgbDelta.Mean:F6}; parity_max_delta={rgbDelta.Max}; identity_mean_delta={identityDelta.Mean:F6}; identity_max_delta={identityDelta.Max}");
        }
        finally { Directory.Delete(root, true); }
    }

    private static (double Mean, int Max) CompareBytes(byte[] left, byte[] right)
    {
        Assert.HasCount(left.Length, right);
        var values = left.Zip(right, (a, b) => Math.Abs(a - b)).ToArray();
        return (values.Average(), values.Max());
    }

    [TestMethod]
    public async Task ReferenceAnalysisCacheSharesSameFileAcrossTargetsAndInvalidatesChangedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ReferenceCache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = CreatePng(root, "reference", 80, 90, 100);
            var backend = new ReferenceLookPreviewService();
            var shared = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => backend.AnalyzeExternalReferenceAsync(path)));
            Assert.IsTrue(ReferenceEquals(shared[0], shared[1]));
            Assert.IsTrue(ReferenceEquals(shared[1], shared[2]));
            Assert.AreEqual(1L, backend.ReferenceCacheStats.Misses);
            Assert.AreEqual(2L, backend.ReferenceCacheStats.Hits);
            Assert.AreEqual(1L, backend.ReferenceCacheStats.Executions);
            Assert.IsGreaterThan(TimeSpan.Zero, backend.ReferenceCacheStats.AnalysisTime);
            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
            await backend.AnalyzeExternalReferenceAsync(path);
            Assert.AreEqual(2L, backend.ReferenceCacheStats.Misses);
            Assert.AreEqual(2L, backend.ReferenceCacheStats.Executions);
            TestContext?.WriteLine($"reference_cache_hits={backend.ReferenceCacheStats.Hits}; misses={backend.ReferenceCacheStats.Misses}; executions={backend.ReferenceCacheStats.Executions}; analysis_ms={backend.ReferenceCacheStats.AnalysisTime.TotalMilliseconds:F2}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task HundredTargetBatchExportMeasuresCompletionAndPeakManagedMemory()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Batch100-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output");
            Directory.CreateDirectory(source); Directory.CreateDirectory(output);
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output), new PixelBackend());
            for (var index = 0; index < 100; index++)
                workspace.Targets.Add(new ReferenceTargetItem(CreatePng(source, $"{index:000}", 40, 60, 80))
                { IsSelected = true, AppliedLookSnapshot = Look("red", 10 + index % 20) });
            var before = GC.GetTotalMemory(true); var clock = Stopwatch.StartNew();
            await workspace.ExportAllCommand.ExecuteAsync(null);
            clock.Stop();
            var managedPeak = GC.GetTotalMemory(false);
            Assert.AreEqual(100, workspace.ExportCompleted);
            Assert.HasCount(100, Directory.GetFiles(output, "*_仿色.jpg"));
            Assert.IsTrue(workspace.Targets.All(item => item.ExportStatus == ReferenceExportStatus.Succeeded));
            TestContext?.WriteLine($"batch100_ms={clock.Elapsed.TotalMilliseconds:F1}; managed_before_mb={before / 1048576d:F2}; managed_after_mb={managedPeak / 1048576d:F2}; process_peak_mb={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d:F2}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task ImportHundredThenFiveHundredTargetsMeasuresFreshSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Import500-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = Enumerable.Range(0, 500).Select(index => CreatePng(root, $"{index:000}", 40, 60, 80)).ToArray();
            var dialog = new FolderDialog(root) { Paths = paths.Take(100).ToArray() };
            using var workspace = new ReferenceColorWorkspaceViewModel(dialog, new PixelBackend());
            var hundred = Stopwatch.StartNew(); await workspace.ChooseTargetCommand.ExecuteAsync(null); hundred.Stop();
            Assert.HasCount(100, workspace.Targets);
            dialog.Paths = paths;
            var fiveHundred = Stopwatch.StartNew(); await workspace.ChooseTargetCommand.ExecuteAsync(null); fiveHundred.Stop();
            Assert.HasCount(500, workspace.Targets);
            Assert.IsTrue(workspace.Targets.All(item => item.Thumbnail is not null));
            TestContext?.WriteLine($"import100_ms={hundred.Elapsed.TotalMilliseconds:F1}; import500_total_ms={fiveHundred.Elapsed.TotalMilliseconds:F1}; process_peak_mb={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d:F2}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RapidThirtyTargetActivationsKeepLastPreviewAndActiveState()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RapidTargets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(root), new PixelBackend());
            var paths = Enumerable.Range(0, 30).Select(index => CreatePng(root, $"{index:00}", (byte)(40 + index), 60, 80)).ToArray();
            var clock = Stopwatch.StartNew();
            await Task.WhenAll(paths.Select(path => workspace.LoadTargetAsync(path)));
            clock.Stop();
            Assert.AreEqual(Path.GetFileName(paths[^1]), workspace.TargetName);
            Assert.AreEqual(paths[^1], workspace.ActiveTarget?.Path);
            Assert.AreEqual(1, workspace.Targets.Count(item => item.IsActive));
            Assert.IsFalse(workspace.IsLoading);
            Assert.AreEqual((byte)69, Pixels(workspace.TargetImage!)[2]);
            Assert.AreEqual((byte)69, Pixels(workspace.Editor.SourceImage!)[2]);
            TestContext?.WriteLine($"rapid30_target_switch_ms={clock.Elapsed.TotalMilliseconds:F1}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task CancelBatchKeepsCompletedFileAndCleansUnfinishedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-CancelBatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output");
            Directory.CreateDirectory(source); Directory.CreateDirectory(output);
            var backend = new PixelBackend { BlockAfterFirstRender = true };
            using var workspace = new ReferenceColorWorkspaceViewModel(new FolderDialog(output), backend);
            foreach (var index in Enumerable.Range(0, 2)) workspace.Targets.Add(new ReferenceTargetItem(CreatePng(source, $"{index}", 40, 60, 80))
            { IsSelected = true, AppliedLookSnapshot = Look("red") });
            var export = workspace.ExportAllCommand.ExecuteAsync(null);
            await backend.SecondRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var clock = Stopwatch.StartNew(); workspace.StopExportCommand.Execute(null);
            await export.WaitAsync(TimeSpan.FromSeconds(10)); clock.Stop();
            Assert.AreEqual(ReferenceExportStatus.Succeeded, workspace.Targets[0].ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Cancelled, workspace.Targets[1].ExportStatus);
            Assert.IsTrue(File.Exists(workspace.Targets[0].OutputPath));
            Assert.IsFalse(File.Exists(Path.Combine(output, "1_仿色.jpg")));
            Assert.IsFalse(File.Exists(Path.Combine(output, "1_仿色.jpg.tmp")));
            TestContext?.WriteLine($"cancel_latency_ms={clock.Elapsed.TotalMilliseconds:F2}");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    public TestContext? TestContext { get; set; }

    private static byte[] Pixels(BitmapSource image)
    {
        var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(bytes, bgra.PixelWidth * 4, 0);
        return bytes;
    }

    private static ReferenceLook Look(string name, double strength = 50, byte r = 100, byte g = 100, byte b = 100)
    {
        var pixels = new VisualPixelBuffer(16, 16, Enumerable.Repeat(new[] { r, g, b }, 256).SelectMany(pixel => pixel).ToArray());
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic", pixels));
        var reference = new ReferenceLookSource(Guid.NewGuid(), analysis.AssetId, "Synthetic", "synthetic.png", "synthetic", 1, analysis);
        return new(Guid.NewGuid(), name, null, [reference], new(MatchStrength: strength), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static string CreatePng(string folder, string name, byte r, byte g, byte b)
    {
        var path = Path.Combine(folder, name + ".png");
        var pixels = Enumerable.Range(0, 16 * 16).SelectMany(_ => new byte[] { b, g, r, 255 }).ToArray();
        var image = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream); return path;
    }

    private static string CreateJpeg(string folder, string name, byte r, byte g, byte b)
    {
        var path = Path.Combine(folder, name + ".jpg");
        var pixels = Enumerable.Range(0, 16 * 16).SelectMany(_ => new byte[] { b, g, r, 255 }).ToArray();
        var image = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        var encoder = new JpegBitmapEncoder { QualityLevel = 100 }; encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream); return path;
    }

    private static void AssertRgb(string path, int r, int g, int b)
    {
        var image = Load(path);
        var pixels = new byte[16 * 16 * 4]; new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 16 * 4, 0);
        Assert.IsLessThanOrEqualTo(5, Math.Abs(pixels[2] - r), path + " red");
        Assert.IsLessThanOrEqualTo(5, Math.Abs(pixels[1] - g), path + " green");
        Assert.IsLessThanOrEqualTo(5, Math.Abs(pixels[0] - b), path + " blue");
    }

    private static BitmapImage Load(string path)
    { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image; }

    private sealed class PixelBackend : IReferenceRenderBackend
    {
        public Action? FirstRender { get; set; }
        public bool BlockAfterFirstRender { get; set; }
        public bool DelayCancelledRenderExit { get; set; }
        public TaskCompletionSource ReleaseCancelledRender { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRenderStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> RenderedLooks { get; } = [];
        public List<string> AppliedFilms { get; } = [];
        public async Task<ReferenceLookPreviewRenderResult> RenderWithResultAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default)
        {
            RenderedLooks.Add($"{look.Name}:{look.Parameters.MatchStrength:0}");
            if (RenderedLooks.Count == 1) FirstRender?.Invoke();
            if (BlockAfterFirstRender && RenderedLooks.Count == 2)
            {
                SecondRenderStarted.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) when (DelayCancelledRenderExit)
                { await ReleaseCancelledRender.Task; throw; }
            }
            var color = look.Name switch { "red" => (R: (byte)220, G: (byte)20, B: (byte)20), "green" => (R: (byte)20, G: (byte)220, B: (byte)20), "blue" => (R: (byte)20, G: (byte)20, B: (byte)220), _ => (R: (byte)220, G: (byte)220, B: (byte)20) };
            var pixels = Enumerable.Range(0, 16 * 16).SelectMany(_ => new byte[] { color.B, color.G, color.R, 255 }).ToArray();
            var result = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4); result.Freeze();
            return new ReferenceLookPreviewRenderResult(result, null);
        }
        public Task<BitmapSource> ApplyFilmAsync(BitmapSource source, PixelTartFilmSettings settings, CancellationToken token = default)
        { AppliedFilms.Add(settings.ProfileId); return Task.FromResult(source); }
        public Task<ReferenceCubeLut> BuildExportLutAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ReferenceLookSource> AnalyzeExternalReferenceAsync(string path, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class FolderDialog(string folder) : IDialogService
    {
        public IReadOnlyList<string> Paths { get; set; } = [];
        public string? ChooseFolder(string title, string? initialDirectory = null) => folder;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => Paths;
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { }
        public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
