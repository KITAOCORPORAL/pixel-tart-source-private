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
            await workspace.ExportAllCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.IsFalse(workspace.IsExporting); Assert.AreSame(frame, workspace.Editor.MatchedImage);
            Assert.AreEqual(ReferenceExportStatus.Failed, workspace.Targets.Single(t => t.Path == corrupt).ExportStatus);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, workspace.Targets.Single(t => t.Path == good).ExportStatus);
            Assert.IsFalse(Directory.EnumerateFiles(root, "*.tmp").Any());
            File.Copy(good, corrupt, true);
            await workspace.LoadTargetAsync(corrupt).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.AreEqual(corrupt, workspace.ActiveTarget!.Path); Assert.IsFalse(workspace.IsLoading);
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
        public TaskCompletionSource SecondRenderStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> RenderedLooks { get; } = [];
        public List<string> AppliedFilms { get; } = [];
        public async Task<ReferenceLookPreviewRenderResult> RenderWithResultAsync(BitmapSource source, ReferenceLook look, CancellationToken token = default)
        {
            RenderedLooks.Add($"{look.Name}:{look.Parameters.MatchStrength:0}");
            if (RenderedLooks.Count == 1) FirstRender?.Invoke();
            if (BlockAfterFirstRender && RenderedLooks.Count == 2)
            { SecondRenderStarted.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }
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
