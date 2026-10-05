using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class ColorStudioEffectiveStateTests
{
    [TestMethod]
    public void StandaloneFilmAndExistingStackResolveWithoutDroppingOrDuplicatingFilm()
    {
        var film=new PixelTartFilmSettings(true,"PT-W01",ProfileAmount:100,SpatialVersion:2);
        var state=ColorStudioEffectiveState.Resolve(null,null,film)!;Assert.HasCount(1,state.Nodes);Assert.AreEqual(ColorStudioNodeType.Film,state.Nodes[0].Type);
        var input=new HighBitDepthImageBuffer(1,1,new float[]{.4f,.5f,.6f},"16");var raw=new RawMatchTiff16ProductPipeline(new LibRawDecoder());
        CollectionAssert.AreEqual(PixelTartFilmPipeline.Apply(input,film).Rgb32.ToArray(),raw.Render(input,null,null,film:film).ProcessingPixels!.Rgb32.ToArray());
        var tool=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"WB");
        var merged=ColorStudioEffectiveState.Resolve(null,new([tool],ProcessingVersion:2),film)!;
        Assert.HasCount(2,merged.Nodes);Assert.AreEqual(tool.Id,merged.Nodes[0].Id);Assert.AreEqual(2,merged.ProcessingVersion);
        Assert.HasCount(2,ColorStudioEffectiveState.Resolve(null,merged,film)!.Nodes);
    }
}
