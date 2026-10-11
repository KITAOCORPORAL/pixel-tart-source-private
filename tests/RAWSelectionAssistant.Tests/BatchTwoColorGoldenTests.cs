using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchTwoColorGoldenTests
{
    [TestMethod]
    public void LegacyAndCurrentRenderUsePinnedSyntheticOutput()
    {
        var bytes=new byte[]{0,0,0,255,255,255,71,113,147,220,32,14,15,180,55};
        var pixels=new VisualPixelBuffer(5,1,bytes);
        var analysis=VisualAnalysisEngine.Analyze(new(Guid.Empty,"public-synthetic",pixels));
        var node=new ColorAdjustmentStackNode(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),ColorStudioNodeType.Develop,"Exposure",true,new Dictionary<string,double>{{"exposure",.5}});
        foreach(var version in new[]{1,2})
        {
            var actual=new ColorStudioRenderPipeline().Render(pixels,analysis,null,new([node],ProcessingVersion:version)).Pixels.Rgb24.ToArray();
            CollectionAssert.AreEqual(Convert.FromHexString("000000FFFFFF5485ACFF271214D242"), actual, "ProcessingVersion " + version + " byte golden must stay exact.");
            Assert.HasCount(15,actual);
        }
    }
    [TestMethod]
    public void CandidateThreePinsLinearExposureAndRejectsImplicitMigration()
    {
        var samples=new[]{new RAWSelectionAssistant.Core.Services.Color.ColorVector3(0,0,0),new(.18,.18,.18),new(1,1,1),new(-.05,1.2,.4)};
        var expected=new[]{new RAWSelectionAssistant.Core.Services.Color.ColorVector3(0,0,0),new(.36,.36,.36),new(2,2,2),new(-.1,2.4,.8)};
        for(var i=0;i<samples.Length;i++)
        {
            var actual=ColorStudioLinearCandidate.Transform(samples[i],RAWSelectionAssistant.Core.Services.Color.WorkingColorSpace.AcesCg,1);
            Assert.AreEqual(expected[i].X,actual.X,1e-12); Assert.AreEqual(expected[i].Y,actual.Y,1e-12); Assert.AreEqual(expected[i].Z,actual.Z,1e-12);
        }
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"old");
        Assert.Throws<ArgumentException>(()=>new ColorAdjustmentStack([node],ProcessingVersion:3).Normalize());
    }}
