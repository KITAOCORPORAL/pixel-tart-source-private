using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorStudioStateClosureTests
{
    [TestMethod]
    public void SimpleProfessionalRoundTripPreservesReferenceStateTests() => Sta(() =>
    {
        using var editor = Editor();
        editor.SelectedLook = Look();
        editor.WorkspaceMode = "专业";
        editor.MatchStrength = 37;
        editor.SkinProtection = 23;
        var node = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.ReferenceMatch);
        Assert.AreEqual(37, node.NumericParameters["match_strength"]);
        Assert.AreEqual(23, node.NumericParameters["skin_protection"]);
        editor.WorkspaceMode = "简洁";
        Assert.AreEqual(37, editor.MatchStrength);
        Assert.AreEqual(23, editor.SkinProtection);
    });

    [TestMethod]
    public void SimpleProfessionalRoundTripPreservesFilmStateTests() => Sta(() =>
    {
        using var editor = Editor();
        editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        editor.FilmEnabled = true; editor.FilmGrainAmount = 42;
        var node = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.Film);
        Assert.AreEqual(42, node.FilmSettings!.GrainAmount);
        editor.WorkspaceMode = "简洁";
        Assert.AreEqual(42, editor.FilmGrainAmount);
    });

    [TestMethod]
    public void NegativeOnlySamplesCanBeClearedTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.ColorRange);
        editor.SampleMode = "减少取样"; editor.AddDisplayedSample(new VisualRgb24(20, 40, 60));
        Assert.IsTrue(editor.ClearSamplesCommand.CanExecute(null));
        editor.ClearSamplesCommand.Execute(null);
        Assert.HasCount(0, editor.SelectedAdjustmentNode!.NegativeSamples);
    });

    [TestMethod]
    public void NodeResetClearsNegativeSamplesTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.ColorRange);
        editor.SampleMode = "减少取样"; editor.AddDisplayedSample(new VisualRgb24(20, 40, 60));
        editor.ResetAdjustmentNodeCommand.Execute(null);
        Assert.HasCount(0, editor.SelectedAdjustmentNode!.NegativeSamples);
    });

    [TestMethod]
    public void BatchSyncTargetIsolationTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        var first = new ReferenceTargetItem("first.jpg"); var second = new ReferenceTargetItem("second.jpg");
        editor.CopyCurrentLookTo([first, second]);
        var firstNode = first.ColorAdjustmentStackSnapshot!.Nodes.First();
        var secondNode = second.ColorAdjustmentStackSnapshot!.Nodes.First();
        Assert.AreNotSame(firstNode.NumericParameters, secondNode.NumericParameters);
        Assert.AreNotSame(first.ColorAdjustmentStackSnapshot.Nodes, second.ColorAdjustmentStackSnapshot.Nodes);
    });

    [TestMethod]
    public void TargetWithNoStackDoesNotInheritPreviousStackTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        Assert.IsNotEmpty(editor.AdjustmentStack.Nodes);
        editor.ApplyTargetSnapshot(null, null, null);
        Assert.HasCount(0, editor.AdjustmentStack.Nodes);
    });

    [TestMethod]
    public void StackOnlyTargetPersistsWithoutReferenceLookTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        var target = new ReferenceTargetItem("stack-only.jpg");
        editor.CopyCurrentLookTo([target]);
        Assert.IsNull(target.AppliedLookSnapshot);
        Assert.IsNotNull(target.ColorAdjustmentStackSnapshot);
        editor.ApplyTargetSnapshot(null, null, target.ColorAdjustmentStackSnapshot);
        Assert.IsNotEmpty(editor.AdjustmentStack.Nodes);
    });

    [TestMethod]
    public void TargetSwitchRestoresOwnStackTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        var first = new ReferenceTargetItem("first.jpg"); editor.CopyCurrentLookTo([first]);
        editor.AddAdjustmentNodeCommand.Execute("ColorRange");
        var second = new ReferenceTargetItem("second.jpg"); editor.CopyCurrentLookTo([second]);
        editor.ApplyTargetSnapshot(first.AppliedLookSnapshot, first.FilmSettingsSnapshot, first.ColorAdjustmentStackSnapshot);
        Assert.HasCount(first.ColorAdjustmentStackSnapshot!.Nodes.Count, editor.AdjustmentStack.Nodes);
        editor.ApplyTargetSnapshot(second.AppliedLookSnapshot, second.FilmSettingsSnapshot, second.ColorAdjustmentStackSnapshot);
        Assert.HasCount(second.ColorAdjustmentStackSnapshot!.Nodes.Count, editor.AdjustmentStack.Nodes);
    });

    [TestMethod]
    public void NodeBoundaryCommandStateTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.First();
        Assert.IsFalse(editor.MoveAdjustmentNodeUpCommand.CanExecute(null));
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Last();
        Assert.IsFalse(editor.MoveAdjustmentNodeDownCommand.CanExecute(null));
    });

    [TestMethod]
    public void NodeRenameUiTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.StartNodeRenameCommand.Execute(null);
        editor.PendingNodeName = "暖橙"; editor.CommitNodeRenameCommand.Execute(null);
        Assert.AreEqual("暖橙", editor.SelectedAdjustmentNode!.Name);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreNotEqual("暖橙", editor.SelectedAdjustmentNode!.Name);
    });

    [TestMethod]
    public void UndoRestoresSelectedNodeTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        var selected = editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ColorRange);
        editor.SelectedAdjustmentNode = selected;
        editor.DeleteAdjustmentNodeCommand.Execute(null);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(selected.Id, editor.SelectedAdjustmentNode?.Id);
    });

    [TestMethod]
    public void NodeDragReorderTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        var first = editor.AdjustmentNodes[0].Id; var last = editor.AdjustmentNodes[^1].Id;
        editor.MoveAdjustmentNode(first, last);
        Assert.AreEqual(first, editor.AdjustmentNodes[^1].Id);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(first, editor.AdjustmentNodes[0].Id);
    });

    [TestMethod]
    public void DuplicateNodeKeepsSingleReferenceAndFilmAndIsolatesParametersTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ReferenceMatch);
        Assert.IsFalse(editor.DuplicateAdjustmentNodeCommand.CanExecute(null));
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.Film);
        Assert.IsFalse(editor.DuplicateAdjustmentNodeCommand.CanExecute(null));
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ColorRange);
        editor.RangeHue = 14;
        editor.DuplicateAdjustmentNodeCommand.Execute(null);
        var ranges = editor.AdjustmentNodes.Where(node => node.Type == ColorStudioNodeType.ColorRange).ToArray();
        Assert.HasCount(2, ranges);
        Assert.AreNotSame(ranges[0].NumericParameters, ranges[1].NumericParameters);
        editor.RangeHue = 36;
        Assert.AreEqual(14, ranges[0].NumericParameters["hue"]);
    });

    [TestMethod]
    public void SliderDragSingleUndoTransactionTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.ColorRange);
        editor.BeginEditTransaction(); editor.RangeHue = 10; editor.RangeHue = 20; editor.RangeHue = 30; editor.CommitEditTransaction();
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(0, editor.RangeHue);
        editor.RedoAdjustmentCommand.Execute(null);
        Assert.AreEqual(30, editor.RangeHue);
    });

    [TestMethod]
    public void ContinuousNegativeSamplingTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(item => item.Type == ColorStudioNodeType.ColorRange);
        editor.StartSamplingCommand.Execute("减少取样");
        editor.CompleteDisplayedSample(new VisualRgb24(10, 20, 30));
        Assert.IsTrue(editor.IsSampling);
        editor.CompleteDisplayedSample(new VisualRgb24(40, 50, 60));
        Assert.HasCount(2, editor.SelectedAdjustmentNode!.NegativeSamples);
        editor.CancelSamplingCommand.Execute(null);
        Assert.IsFalse(editor.IsSampling);
    });

    [TestMethod]
    public void SchemeV2RestartRoundTripTests() => Sta(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "pixel-tart-ui-scheme-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ColorStudioSchemeStore(folder);
            using (var editor = new TetherReferenceModeViewModel(new ReferenceLookStore(folder), schemeStore: store))
            {
                editor.WorkspaceMode = "专业"; editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ColorRange);
                editor.RangeHue = 28; editor.SampleMode = "减少取样"; editor.AddDisplayedSample(new VisualRgb24(22, 33, 44));
                editor.ColorSchemeName = "重启方案"; editor.SaveColorSchemeCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            }
            using var restarted = new TetherReferenceModeViewModel(new ReferenceLookStore(folder), schemeStore: store);
            restarted.LoadAsync().GetAwaiter().GetResult();
            restarted.SelectedColorScheme = restarted.ColorSchemes.Single(); restarted.ApplyColorSchemeCommand.Execute(null);
            var range = restarted.AdjustmentNodes.Single(node => node.Type == ColorStudioNodeType.ColorRange);
            Assert.AreEqual(28, range.NumericParameters["hue"]);
            Assert.HasCount(1, range.NegativeSamples);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    });

    [TestMethod]
    public void StopProcessingEndsAfterJobExitsAndKeepsLastValidFrameTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look();
        var source = SolidBitmap(110);
        editor.SetSourceAsync(Guid.NewGuid(), source).GetAwaiter().GetResult();
        editor.Enabled = true;
        Assert.IsTrue(SpinWait.SpinUntil(() => editor.MatchedImage is not null, TimeSpan.FromSeconds(10)));
        var validFrame = editor.MatchedImage;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.PostProcessor = async (image, token) =>
        {
            started.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            finally { await release.Task; }
            token.ThrowIfCancellationRequested();
            return image;
        };
        var render = editor.ApplyCommand.ExecuteAsync(null);
        try
        {
            started.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            editor.StopProcessing();
            Assert.AreEqual(TetherReferenceModeViewModel.ProcessingState.Cancelling, editor.State);
            Assert.AreEqual("正在停止…", editor.StatusText);
            Assert.AreSame(validFrame, editor.MatchedImage);
        }
        finally { release.TrySetResult(); }
        render.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.AreEqual(TetherReferenceModeViewModel.ProcessingState.Cancelled, editor.State);
        Assert.AreEqual("已停止处理。", editor.StatusText);
        Assert.AreSame(validFrame, editor.MatchedImage);
    });

    [TestMethod]
    public void CancelledJobCannotOverwriteNewTargetTests() => Sta(() =>
    {
        using var editor = Editor(); editor.SelectedLook = Look();
        editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(40)).GetAwaiter().GetResult();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldFrame = SolidBitmap(22); var newFrame = SolidBitmap(77);
        var calls = 0;
        editor.PostProcessor = async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.TrySetResult();
                await release.Task;
                return oldFrame;
            }
            return newFrame;
        };
        var oldRender = editor.ApplyCommand.ExecuteAsync(null);
        try
        {
            started.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(80)).GetAwaiter().GetResult();
            editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.AreSame(newFrame, editor.MatchedImage);
        }
        finally { release.TrySetResult(); }
        oldRender.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.AreSame(newFrame, editor.MatchedImage);
    });

    private static BitmapSource SolidBitmap(byte value)
    {
        var pixels = Enumerable.Repeat(new byte[] { value, value, value, 255 }, 16 * 16).SelectMany(x => x).ToArray();
        var image = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        image.Freeze(); return image;
    }

    [TestMethod]
    public void RapidSchemeSwitchLastWinsTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(55)).GetAwaiter().GetResult(); editor.Enabled = true;
        Assert.IsTrue(SpinWait.SpinUntil(() => !editor.IsBusy, TimeSpan.FromSeconds(5)));
        var started = new System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource>();
        var frames = new System.Collections.Concurrent.ConcurrentQueue<BitmapSource>();
        editor.PostProcessor = async (image, _) =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            frames.Enqueue(image); started.Enqueue(gate); await gate.Task; return image;
        };
        var schemes = Enumerable.Range(0, 4).Select(i => new ColorStudioSchemeV2(Guid.NewGuid(), $"方案-{i}",
            new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, $"范围-{i}", NumericParameters: new Dictionary<string, double> { ["hue"] = i * 10 })]),
            DateTimeOffset.UtcNow)).ToArray();
        try
        {
            foreach (var scheme in schemes)
            {
                editor.SelectedColorScheme = scheme; editor.ApplyColorSchemeCommand.Execute(null);
                var expected = Array.IndexOf(schemes, scheme) + 1;
                Assert.IsTrue(SpinWait.SpinUntil(() => started.Count >= expected, TimeSpan.FromSeconds(5)));
            }
            started.Last().TrySetResult();
            Assert.IsTrue(SpinWait.SpinUntil(() => ReferenceEquals(editor.MatchedImage, frames.Last()), TimeSpan.FromSeconds(5)));
        }
        finally { foreach (var gate in started) gate.TrySetResult(); }
        Assert.IsTrue(SpinWait.SpinUntil(() => !editor.IsBusy, TimeSpan.FromSeconds(5)));
        Assert.AreEqual("范围-3", editor.AdjustmentNodes.Single().Name);
        Assert.AreSame(frames.Last(), editor.MatchedImage);
    });

    [TestMethod]
    public void SelectedNodeSurvivesItemsSourceReplacementTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes[0];
        var id = editor.SelectedAdjustmentNode.Id;
        editor.RangeHue = 9; editor.SelectedAdjustmentNode = null;
        Assert.AreEqual(id, editor.SelectedAdjustmentNode?.Id);
        editor.RangeStrength = 72;
        Assert.AreEqual(72, editor.RangeStrength);
    });

    [TestMethod]
    public void ProcessingErrorKeepsFrameAndRetryRecoversTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(60)).GetAwaiter().GetResult();
        editor.Enabled = true;
        editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
        var previous = editor.MatchedImage; Assert.IsNotNull(previous);
        editor.PostProcessor = (_, _) => throw new IOException("fixture failure");
        editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
        Assert.IsTrue(editor.HasError); Assert.IsFalse(editor.IsBusy); Assert.AreSame(previous, editor.MatchedImage);
        editor.PostProcessor = null;
        editor.ApplyCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
        Assert.IsFalse(editor.HasError); Assert.IsFalse(editor.IsBusy); Assert.IsNotNull(editor.MatchedImage);
    });

    [TestMethod]
    public void FailedJobCannotOverwriteLaterSuccessTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(70)).GetAwaiter().GetResult(); editor.Enabled = true;
        Assert.IsTrue(SpinWait.SpinUntil(() => !editor.IsBusy, TimeSpan.FromSeconds(5)));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.PostProcessor = async (_, _) => { started.TrySetResult(); await release.Task; throw new IOException("stale failure"); };
        var old = editor.ApplyCommand.ExecuteAsync(null);
        try
        {
            started.Task.WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
            editor.PostProcessor = null;
            editor.SelectedLook = Look();
            Assert.IsTrue(SpinWait.SpinUntil(() => editor.StatusText.Contains("已更新"), TimeSpan.FromSeconds(5)));
        }
        finally { release.TrySetResult(); }
        old.WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
        Assert.IsFalse(editor.HasError); Assert.IsNotNull(editor.MatchedImage);
    });

    [TestMethod]
    public void RapidReferenceSwitchLastWinsTests() => Sta(() =>
    {
        using var editor = Editor();
        editor.SetSourceAsync(Guid.NewGuid(), SolidBitmap(70)).GetAwaiter().GetResult(); editor.Enabled = true;
        var releases = new List<TaskCompletionSource>(); var frames = new List<BitmapSource>();
        editor.PostProcessor = async (_, _) =>
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var frame = SolidBitmap((byte)(30 + frames.Count)); frames.Add(frame); releases.Add(release);
            await release.Task; return frame;
        };
        for (var i = 0; i < 5; i++)
        {
            editor.SelectedLook = Look() with { Name = $"fixture-{i}" };
            Assert.IsTrue(SpinWait.SpinUntil(() => releases.Count > i, TimeSpan.FromSeconds(5)));
        }
        try
        {
            releases[^1].TrySetResult();
            Assert.IsTrue(SpinWait.SpinUntil(() => ReferenceEquals(editor.MatchedImage, frames[^1]), TimeSpan.FromSeconds(5)));
        }
        finally { foreach (var release in releases) release.TrySetResult(); }
        Assert.IsTrue(SpinWait.SpinUntil(() => !editor.IsBusy, TimeSpan.FromSeconds(5)));
        Assert.AreSame(frames[^1], editor.MatchedImage); Assert.AreEqual("fixture-4", editor.SelectedLook!.Name);
    });

    [TestMethod]
    public void SchemeUnsavedApplyCancelDeleteAndRestartTests() => Sta(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "pixel-tart-scheme-flow-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ColorStudioSchemeStore(folder);
            using var editor = new TetherReferenceModeViewModel(new ReferenceLookStore(folder), schemeStore: store);
            editor.WorkspaceMode = "专业"; editor.ColorSchemeName = "A";
            editor.SaveColorSchemeAsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            var first = editor.SelectedColorScheme!;
            editor.ColorSchemeName = "B"; editor.SaveColorSchemeAsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            var second = editor.SelectedColorScheme!;
            editor.SelectedAdjustmentNode = editor.AdjustmentNodes.First(); editor.RangeHue = 25;
            editor.SelectedColorScheme = first; editor.RequestApplySchemeCommand.Execute(null);
            Assert.IsTrue(editor.SchemeSwitchOpen); Assert.AreEqual(25, editor.RangeHue);
            editor.CancelSchemeSwitchCommand.Execute(null); Assert.AreEqual(25, editor.RangeHue);
            editor.RequestApplySchemeCommand.Execute(null); editor.DiscardAndApplySchemeCommand.Execute(null);
            Assert.AreEqual("A", editor.CurrentColorSchemeName); Assert.AreEqual(0, editor.RangeHue);
            editor.SelectedColorScheme = second; editor.RequestDeleteSchemeCommand.Execute(null);
            Assert.HasCount(2, editor.ColorSchemes);
            editor.CancelDeleteSchemeCommand.Execute(null); Assert.HasCount(2, editor.ColorSchemes);
            editor.RequestDeleteSchemeCommand.Execute(null); editor.ConfirmDeleteSchemeCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.HasCount(1, editor.ColorSchemes);
            using var restarted = new TetherReferenceModeViewModel(new ReferenceLookStore(folder), schemeStore: store);
            restarted.LoadAsync().GetAwaiter().GetResult(); Assert.HasCount(1, restarted.ColorSchemes);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    });

    [TestMethod]
    public void NodeEnableOverflowAndInsertionTests() => Sta(() =>
    {
        using var editor = Editor(); editor.WorkspaceMode = "专业";
        var first = editor.AdjustmentNodes[0]; var last = editor.AdjustmentNodes[^1];
        editor.SelectedAdjustmentNode = first; editor.ToggleAdjustmentNodeCommand.Execute(null);
        Assert.IsFalse(editor.SelectedAdjustmentNode!.Enabled);
        editor.UndoAdjustmentCommand.Execute(null); Assert.IsTrue(editor.SelectedAdjustmentNode!.Enabled);
        editor.InsertAdjustmentNode(first.Id, last.Id, true); Assert.AreEqual(first.Id, editor.AdjustmentNodes[^1].Id);
        Assert.IsFalse(editor.MoveAdjustmentNodeDownCommand.CanExecute(null));
        editor.InsertAdjustmentNode(first.Id, last.Id, false);
        Assert.AreEqual(first.Id, editor.AdjustmentNodes[^2].Id);
    });

    private static TetherReferenceModeViewModel Editor() => new(new ReferenceLookStore(Path.Combine(Path.GetTempPath(), "pixel-tart-state-test-" + Guid.NewGuid().ToString("N"))));
    private static ReferenceLook Look()
    {
        var pixels = new VisualPixelBuffer(16, 16, Enumerable.Repeat(new byte[] { 100, 110, 120 }, 256).SelectMany(pixel => pixel).ToArray());
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic", pixels));
        var reference = new ReferenceLookSource(Guid.NewGuid(), analysis.AssetId, "合成参考", "synthetic.png", "synthetic", 1, analysis);
        return new(Guid.NewGuid(), "方案", null, [reference], new(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "STA test exceeded bounded timeout.");
        if (failure is not null) throw failure;
    }
}
