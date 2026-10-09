using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorStudioSessionStateTests
{
    [TestMethod]
    public async Task LayoutRoundTripPreservesWidthsModesAndOldFilesRemainCompatible()
    {
        var root = Path.Combine(Path.GetTempPath(), "Studio-layout-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "layout.ptstudio.json");
            var layout = new ColorStudioLayout(330, 410, false, false, 2, 4, "Right", "Slideshow", false);
            await ColorStudioSessionStore.SaveAsync(path, new([], null, Layout: layout));
            Assert.AreEqual(layout, (await ColorStudioSessionStore.LoadAsync(path)).Layout);
            await File.WriteAllTextAsync(path, "{\"targets\":[],\"activeTargetId\":null,\"version\":1}");
            Assert.IsNull((await ColorStudioSessionStore.LoadAsync(path)).Layout);
            var invalid = new ColorStudioLayout(double.NaN, double.PositiveInfinity, AuxiliaryMode: 99, ToolMode: -2).Normalize();
            Assert.AreEqual(290, invalid.AuxiliaryWidth); Assert.AreEqual(340, invalid.EditingWidth);
            Assert.AreEqual(3, invalid.AuxiliaryMode); Assert.AreEqual(0, invalid.ToolMode);
            Assert.AreEqual("Bottom", (new ColorStudioLayout(FilmstripDock: "Unknown")).Normalize().FilmstripDock);
            Assert.AreEqual("Grid", (new ColorStudioLayout(FilmstripView: "Unknown")).Normalize().FilmstripView);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void CategorySyncPreservesMultipleRangesAndUnselectedNodesWithoutDuplicateIds()
    {
        ColorAdjustmentStackNode Node(ColorStudioNodeType type, string name) => new(Guid.NewGuid(), type, name);
        var a = Node(ColorStudioNodeType.ColorRange, "肤色"); var b = Node(ColorStudioNodeType.ColorRange, "天空");
        var detail = Node(ColorStudioNodeType.Develop, "保留细节"); var film = Node(ColorStudioNodeType.Film, "保留胶片");
        var target = new ColorAdjustmentStack([detail, Node(ColorStudioNodeType.ColorRange, "旧红色"), film, Node(ColorStudioNodeType.ColorRange, "旧绿色")]);
        var result = target.SyncSelectedByTypeFrom(new([a, b]), new HashSet<ColorStudioNodeType> { ColorStudioNodeType.ColorRange });
        CollectionAssert.AreEqual(new[] { detail.Id, a.Id, b.Id, film.Id }, result.Nodes.Select(x => x.Id).ToArray());
        Assert.AreEqual(4, result.Nodes.Select(x => x.Id).Distinct().Count());
        Assert.AreNotSame(a.NumericParameters, result.Nodes[1].NumericParameters);
    }
    [TestMethod]
    public async Task SessionRoundTripPreservesTargetsSelectionFiltersEngineAndProtectsAssetMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart-Session-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var a = new ColorStudioSessionTarget(Guid.NewGuid(), Path.Combine(directory, "中文 a.jpg"), Guid.NewGuid(), true, null,
                new([new(Guid.NewGuid(), ColorStudioNodeType.Develop, "曝光", NumericParameters: new Dictionary<string, double> { ["exposure"] = .75 })]), null,
                ColorStudioMatchEngine.MatchV4Beta, MatchV4ExecutionMode.Cpu, 5, "红");
            var b = new ColorStudioSessionTarget(Guid.NewGuid(), Path.Combine(directory, "b.png"), null, false, null, null, null, SessionRating: 3, SessionColorLabel: "蓝");
            var path = Path.Combine(directory, "state.ptstudio.json");
            await ColorStudioSessionStore.SaveAsync(path, new([a, b], a.Id, "Jpeg", 3, "蓝"));
            var result = await ColorStudioSessionStore.LoadAsync(path);
            Assert.AreEqual(a.Id, result.ActiveTargetId); Assert.AreEqual("Jpeg", result.FilterScope); Assert.AreEqual(3, result.MinimumRating); Assert.AreEqual("蓝", result.ColorLabelFilter);
            Assert.IsTrue(result.Targets[0].Selected); Assert.IsFalse(result.Targets[1].Selected);
            Assert.IsNull(result.Targets[0].SessionRating); Assert.IsNull(result.Targets[0].SessionColorLabel);
            Assert.AreEqual(3, result.Targets[1].SessionRating); Assert.AreEqual("蓝", result.Targets[1].SessionColorLabel);
            Assert.AreEqual(.75, result.Targets[0].Stack!.Nodes[0].NumericParameters["exposure"]);
            Assert.AreEqual(ColorStudioMatchEngine.MatchV4Beta, result.Targets[0].Engine); Assert.AreEqual(MatchV4ExecutionMode.Cpu, result.Targets[0].ExecutionMode);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }
    [TestMethod]
    public void SessionRejectsDuplicateTargetIdentityBeforeReplacingCurrentDocument()
    {
        var item = new ColorStudioSessionTarget(Guid.NewGuid(), "a.jpg", null, true, null, null, null);
        Assert.Throws<InvalidDataException>(() => new ColorStudioSessionDocument([item, item], item.Id).Normalize());
        Assert.Throws<InvalidDataException>(() => new ColorStudioSessionDocument([item], Guid.NewGuid()).Normalize());
        Assert.Throws<InvalidDataException>(() => new ColorStudioSessionDocument([null!], null).Normalize());
    }
}
