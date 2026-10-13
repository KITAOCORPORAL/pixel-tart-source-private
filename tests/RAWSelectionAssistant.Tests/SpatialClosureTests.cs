using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Tests;
[TestClass]
[DoNotParallelize]
public sealed class SpatialClosureTests
{
    private static HighBitDepthImageBuffer Source(int w=83,int h=151)=>new(w,h,Enumerable.Range(0,w*h*3).Select(i=>(i%991)/991f).ToArray(),"16","sRGB");
    [TestMethod]
    public void EverySpatialNodeMatchesFullFrameAtTileEdgesAndGlobalBoundary()
    {
        var source=Source(513,257);var nodes=new[]{new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Details,"Details",NumericParameters:new Dictionary<string,double>{{"clarity",15},{"structure",20},{"sharpen",30},{"luma_noise",12},{"chroma_noise",8}}),new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",25},{"detail",15},{"exposure",.4}})};
        foreach(var node in nodes)Compare(source,node,2e-6);
        foreach(var version in new[]{1,2})foreach(var bloom in new[]{false,true})
        {
            var film=new PixelTartFilmSettings(true,"PT-W01",GrainAmount:17,VignetteAmount:15,HalationAmount:bloom?0:35,BloomAmount:bloom?30:0,Seed:23,SpatialVersion:version);
            Compare(source,new(Guid.NewGuid(),ColorStudioNodeType.Film,bloom?"Bloom":"Halation",FilmSettings:film),0);
        }
    }
    private static void Compare(HighBitDepthImageBuffer source,ColorAdjustmentStackNode node,double tolerance)
    {
        var expected=new RawMatchTiff16ProductPipeline(new Fake()).Render(source,null,new([node])).ProcessingPixels!;
        foreach(var (w,h) in new[]{(31,37),(67,64)})
        {
            var result=SpatialLegacyRawRenderer.RenderNode(source,node,tileWidth:w,tileHeight:h);double maximum=0;
            for(var i=0;i<result.Rgb32.Length;i++)maximum=Math.Max(maximum,Math.Abs(expected.Rgb32.Span[i]-result.Rgb32.Span[i]));Assert.IsLessThanOrEqualTo(tolerance,maximum,$"{node.Name} tile {w}/{h}: {maximum}");
        }
    }
    [TestMethod]
    public void ReferenceStatisticsAreFrozenGloballyNotRecomputedPerTile()
    {
        var source=Source();var pixels=source.ToVisualRgb24();var analysis=RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis.VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"synthetic",pixels));
        var look=new ReferenceLook(Guid.NewGuid(),"Test",null,[new(Guid.NewGuid(),null,"Test","synthetic","hash",1,analysis)],new(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.ReferenceMatch,"Match");var expected=new RawMatchTiff16ProductPipeline(new Fake()).Render(source,look,new([node])).ProcessingPixels!;
        CollectionAssert.AreEqual(expected.Rgb32.ToArray(),SpatialLegacyRawRenderer.RenderNode(source,node,look).Rgb32.ToArray());
        var corrected=look with{AlgorithmVersion=2,ReferenceSources=look.ReferenceSources.Select(r=>r with{PixelStatistics=ReferencePixelStatistics.FromPixels(source)}).ToArray()};
        var expected2=new RawMatchTiff16ProductPipeline(new Fake()).Render(source,corrected,new([node])).ProcessingPixels!;CollectionAssert.AreEqual(expected2.Rgb32.ToArray(),SpatialLegacyRawRenderer.RenderNode(source,node,corrected).Rgb32.ToArray());
    }
    [TestMethod]
    public void NewSpatialContractIsExplicitAndBudgetRejectsBeforeAllocation()
    {
        var recipe=new InstantInputRecipe(2,"CurrentEdits",Guid.NewGuid(),Stack:new([new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details")])){LegacyRaw=new(1,1,new string('a',64))};
        var old=LegacyRawInstantInput.Create(recipe);var next=LegacyRawInstantInput.CreateSpatial(recipe);Assert.AreNotEqual(old.DecodeKey,next.DecodeKey);Assert.AreEqual("LegacyRawFullResolutionV1",old.InstantInput!.LegacyRaw!.RenderContract);
        var baseline=StudioGlobalResources.AccountedBytes;using(var reserved=StudioGlobalResources.ReserveWork(StudioGlobalResources.TotalLimit-1024))Assert.Throws<StudioBudgetException>(()=>SpatialLegacyRawRenderer.RenderNode(Source(),recipe.Stack!.Nodes[0]));Assert.AreEqual(baseline,StudioGlobalResources.AccountedBytes);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.Throws<OperationCanceledException>(()=>SpatialLegacyRawRenderer.RenderNode(Source(),recipe.Stack!.Nodes[0],token:cancelled.Token));Assert.AreEqual(baseline,StudioGlobalResources.AccountedBytes);
    }
    private sealed class Fake:IRawDecoder{public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();}
}
