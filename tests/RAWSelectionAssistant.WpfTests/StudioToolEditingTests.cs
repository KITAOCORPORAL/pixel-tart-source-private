using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioToolEditingTests
{
    [TestMethod]
    public Task FirstToolPreservesLegacyLookAndFilmAndUndoRestoresEffectiveStack() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        var pixels = new VisualPixelBuffer(2, 2, new byte[] { 40, 60, 80, 100, 120, 140, 170, 150, 130, 220, 210, 200 });
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "test", pixels));
        var source = new ReferenceLookSource(Guid.NewGuid(), analysis.AssetId, "测试参考", "", "test", 1, analysis);
        var film = new PixelTartFilmSettings { Enabled = true, VignetteAmount = .25 };
        var look = new ReferenceLook(Guid.NewGuid(), "旧方案", null, [source], new(MatchStrength: 37), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Film: film);
        editor.ApplyTargetSnapshot(look, film, null, false, Guid.NewGuid());
        var exposure = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p => p.Key == "exposure");
        editor.BeginEditTransaction(); editor.SetToolParameter(exposure, .6); editor.CommitEditTransaction();
        Assert.AreEqual(37, editor.AdjustmentStack.Nodes.Single(n => n.Type == ColorStudioNodeType.ReferenceMatch).NumericParameters["match_strength"]);
        Assert.AreEqual(.25, editor.AdjustmentStack.Nodes.Single(n => n.Type == ColorStudioNodeType.Film).FilmSettings!.VignetteAmount);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.IsNull(editor.ToolNode(ColorStudioNodeType.BasicTone)); Assert.IsTrue(editor.FilmEnabled);
        Assert.AreEqual(2, editor.AdjustmentStack.Nodes.Count);
        // The other legacy path has a look without a standalone film node.
        editor.ApplyTargetSnapshot(look with { Film = null }, null, null, false, Guid.NewGuid());
        editor.BeginEditTransaction(); editor.SetToolParameter(exposure, .4); editor.CommitEditTransaction();
        Assert.HasCount(1, editor.AdjustmentStack.Nodes.Where(n => n.Type == ColorStudioNodeType.ReferenceMatch).ToArray());
        editor.UndoAdjustmentCommand.Execute(null); Assert.IsNotNull(editor.ToolNode(ColorStudioNodeType.ReferenceMatch));
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task FilmWithoutReferenceHasRealNodeAndUndoClearsFilmUi() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, null, false, Guid.NewGuid());
        editor.FilmEnabled = true;
        Assert.IsTrue(editor.Enabled); Assert.IsTrue(editor.AdjustmentStack.Nodes.Single().FilmSettings!.Enabled);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(0, editor.AdjustmentStack.Nodes.Count); Assert.IsFalse(editor.FilmEnabled);
        editor.RedoAdjustmentCommand.Execute(null); Assert.IsTrue(editor.FilmEnabled);
        editor.AddAdjustmentNodeCommand.Execute("ColorRange"); editor.RangeSaturation = 20;
        editor.ResetAdjustmentNodeCommand.Execute(null);
        Assert.AreEqual(2, editor.SelectedAdjustmentNode!.NumericParameters["range_version"]); Assert.AreEqual(0, editor.RangeSaturation);
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task FirstToolDragIsOneUndoAndDisabledNeutralToolStaysDisabled() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, new ColorAdjustmentStack([]), false, Guid.NewGuid());
        var exposure = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p => p.Key == "exposure");
        editor.BeginEditTransaction();
        editor.SetToolParameter(exposure, .2); editor.SetToolParameter(exposure, .4); editor.SetToolParameter(exposure, .8);
        editor.CommitEditTransaction();
        Assert.AreEqual(.8, editor.ToolValue(exposure)); Assert.AreEqual(2, editor.AdjustmentStack.ProcessingVersion);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(0, editor.ToolValue(exposure)); Assert.AreEqual(0, editor.AdjustmentStack.Nodes.Count);
        Assert.IsFalse(editor.UndoAdjustmentCommand.CanExecute(null));
        editor.RedoAdjustmentCommand.Execute(null); Assert.AreEqual(.8, editor.ToolValue(exposure));
        editor.SetToolEnabled(ColorStudioNodeType.WhiteBalance, false);
        Assert.IsFalse(editor.ToolNode(ColorStudioNodeType.WhiteBalance)!.Enabled);
        var temperature = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.WhiteBalance).Single(p => p.Key == "temperature");
        editor.SetToolParameter(temperature, 30);
        Assert.IsFalse(editor.ToolNode(ColorStudioNodeType.WhiteBalance)!.Enabled);
        editor.SetToolEnabled(ColorStudioNodeType.WhiteBalance, true);
        Assert.AreEqual(30, editor.ToolValue(temperature));
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task InvalidLevelsDoNotMutateStackAndGroupResetKeepsOtherGroup() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, new ColorAdjustmentStack([]), false, Guid.NewGuid());
        var parameters = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.Levels).ToArray();
        var black = parameters.Single(p => p.Key == "rgb_black"); var white = parameters.Single(p => p.Key == "rgb_white");
        editor.SetToolParameter(black, .8); var before = editor.AdjustmentStack;
        Assert.ThrowsExactly<ArgumentException>(() => editor.SetToolParameter(white, .3));
        Assert.AreSame(before, editor.AdjustmentStack);
        Assert.ThrowsExactly<ArgumentException>(() => editor.SetToolParameter(black, double.NaN));
        var redGamma = parameters.Single(p => p.Key == "r_gamma"); editor.SetToolParameter(redGamma, 1.4);
        editor.ResetToolGroup(ColorStudioNodeType.Levels, black.Group);
        Assert.AreEqual(0, editor.ToolValue(black)); Assert.AreEqual(1.4, editor.ToolValue(redGamma));
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task CurvePointEditUndoRestoresAllPointsAndRejectsDuplicateInputs() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, new ColorAdjustmentStack([]), false, Guid.NewGuid());
        editor.BeginEditTransaction();
        editor.SetCurvePoints("r", [new(0, 0), new(.4, .65), new(1, 1)]);
        editor.SetCurvePoints("r", [new(0, 0), new(.45, .7), new(1, 1)]);
        editor.CommitEditTransaction();
        Assert.AreEqual(.7, ColorStudioToolProcessor.ReadCurve(editor.ToolNode(ColorStudioNodeType.Curve)!, "r")[1].Y);
        var before = editor.AdjustmentStack;
        Assert.ThrowsExactly<ArgumentException>(() => editor.SetCurvePoints("r", [new(0, 0), new(0, .5), new(1, 1)]));
        Assert.AreSame(before, editor.AdjustmentStack);
        editor.UndoAdjustmentCommand.Execute(null); Assert.IsNull(editor.ToolNode(ColorStudioNodeType.Curve));
        editor.RedoAdjustmentCommand.Execute(null); Assert.AreEqual(3, ColorStudioToolProcessor.ReadCurve(editor.ToolNode(ColorStudioNodeType.Curve)!, "r").Count);
        await Task.CompletedTask;
    });
}
