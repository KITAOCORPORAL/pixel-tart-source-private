using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class EveningFeedbackCoreTests
{
    [TestMethod]
    public void ColorRangeFloatUsesActualSelectionAndFilmSharesDisplayMath()
    {
        var pixels = new VisualPixelBuffer(2, 1, new byte[] {220,30,30,30,30,220});
        var master = HighBitDepthImageBuffer.FromVisualRgb24(pixels);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(pixels), pixels));
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "红色", true,
            new Dictionary<string,double> {{"range",.02},{"softness",.01},{"lightness",20}}, new VisualRgb24[] {new(220,30,30)});
        var pipeline = new ColorStudioRenderPipeline();
        var stack = new ColorAdjustmentStack([node]);
        var result = pipeline.Render(master, analysis, null, stack).ProcessingPixels!;
        CollectionAssert.AreEqual(master.Rgb32.ToArray()[3..], result.Rgb32.ToArray()[3..], "Unselected blue must not change");
        Assert.IsFalse(master.Rgb32.ToArray()[..3].SequenceEqual(result.Rgb32.ToArray()[..3]));
        var display = pipeline.Render(pixels, analysis, null, stack).Pixels;
        CollectionAssert.AreEqual(display.Rgb24.ToArray(), result.ToVisualRgb24().Rgb24.ToArray());
        var film = new PixelTartFilmSettings(true, "PT-W01", 60, 20, 35, 20, 30, 40, 30, "Paper", 25);
        CollectionAssert.AreEqual(PixelTartFilmPipeline.Apply(pixels,film).Rgb24.ToArray(), PixelTartFilmPipeline.Apply(master,film).ToVisualRgb24().Rgb24.ToArray());
    }

    [TestMethod]
    public void AllQuantizedToneBoundariesAndMembersMatchStatistics()
    {
        var bytes=Enumerable.Range(0,256).SelectMany(i=>new[]{(byte)i,(byte)i,(byte)i}).ToArray();var pixels=new VisualPixelBuffer(256,1,bytes);
        var histogram=VisualAnalysisEngine.AnalyzeHistogram(pixels);var seen=new List<int>();
        for(var zone=0;zone<11;zone++) {var members=VisualAnalysisEngine.ToneZoneMembers(pixels,zone);seen.AddRange(members);Assert.AreEqual(histogram.Zones[zone],members.Count/256d);foreach(var p in members)Assert.AreEqual(zone,VisualAnalysisEngine.ToneZoneIndex((byte)p,(byte)p,(byte)p));}
        CollectionAssert.AreEquivalent(Enumerable.Range(0,256).ToArray(),seen.ToArray());Assert.AreEqual(0,VisualAnalysisEngine.ToneZoneIndex(0,0,0));Assert.AreEqual(10,VisualAnalysisEngine.ToneZoneIndex(255,255,255));
        var cancelled=new CancellationToken(true);Assert.ThrowsExactly<OperationCanceledException>(()=>VisualAnalysisEngine.ToneZoneMembers(pixels,2,cancelled));
    }
    [TestMethod]
    public void DevelopNeutralIsExactAndEveryControlChangesPixelsWithSharedPrecisionMath()
    {
        var bytes=Enumerable.Range(0,1024).SelectMany(i=>new[]{(byte)(20+i*47%220),(byte)(15+i*31%235),(byte)(10+i*61%240)}).ToArray();var pixels=new VisualPixelBuffer(32,32,bytes);var master=HighBitDepthImageBuffer.FromVisualRgb24(pixels);
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"影调");CollectionAssert.AreEqual(master.Rgb32.ToArray(),ColorStudioDevelop.Apply(master,node).Rgb32.ToArray());
        var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),VisualAnalysisFingerprint.Compute(pixels),pixels));
        foreach(var key in new[]{"exposure","brightness","highlights","midtones","shadows","contrast","whites","blacks","structure","detail"})
        {
            var adjusted=node with {NumericParameters=new Dictionary<string,double>{[key]=key=="exposure"?.7:40}};var stack=new ColorAdjustmentStack([adjusted]);var pipeline=new ColorStudioRenderPipeline();
            var display=pipeline.Render(pixels,analysis,null,stack).Pixels;var full=pipeline.Render(master,analysis,null,stack).Pixels;
            CollectionAssert.AreEqual(display.Rgb24.ToArray(),full.Rgb24.ToArray(),key);Assert.IsFalse(bytes.SequenceEqual(display.Rgb24.ToArray()),key+" must change actual pixels");
            Assert.IsTrue(pipeline.Render(master,analysis,null,stack).ProcessingPixels!.Rgb32.Span.ToArray().All(x=>float.IsFinite(x)&&x>=0&&x<=1));
        }
        CollectionAssert.AreEqual(bytes,pixels.Rgb24.ToArray());
    }
    [TestMethod]
    public async Task DevelopPersistsAndSyncPreservesUnselectedNodes()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-Develop-"+Guid.NewGuid());
        try {var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"影调",true,new Dictionary<string,double>{{"shadows",27},{"exposure",.4}});var scheme=new ColorStudioSchemeV2(Guid.NewGuid(),"晚间",new([node]),DateTimeOffset.UtcNow);await new ColorStudioSchemeStore(root).SaveAsync(scheme);var restored=(await new ColorStudioSchemeStore(root).LoadAsync()).Single();Assert.AreEqual(ColorStudioSchemeSerializer.ComputeHash(scheme),ColorStudioSchemeSerializer.ComputeHash(restored));var film=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Film,"胶片",false);var sync=new ColorAdjustmentStack([film]).SyncSelectedByTypeFrom(restored.Stack,new HashSet<ColorStudioNodeType>{ColorStudioNodeType.Develop});Assert.AreEqual(film.Id,sync.Nodes[0].Id);Assert.AreEqual(film.Enabled,sync.Nodes[0].Enabled);Assert.AreEqual(film.Name,sync.Nodes[0].Name);CollectionAssert.AreEqual(film.NumericParameters.ToArray(),sync.Nodes[0].NumericParameters.ToArray());Assert.AreEqual(27d,sync.Nodes[1].NumericParameters["shadows"]);}
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
