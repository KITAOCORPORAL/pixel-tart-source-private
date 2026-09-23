using System.IO;
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
