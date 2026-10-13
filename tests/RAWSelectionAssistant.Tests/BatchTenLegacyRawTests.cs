using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using System.Text.Json;
namespace RAWSelectionAssistant.Tests;
[TestClass]
[DoNotParallelize]
public sealed class BatchTenLegacyRawTests
{
    internal static InstantInputRecipe Recipe()=>new(2,"CurrentEdits",Guid.NewGuid(),Stack:new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",1.25}})],ProcessingVersion:2),Film:new(true,GrainAmount:12,Seed:42))
    {LegacyRaw=new(1,2,new string('a',64))};
    [TestMethod]
    public void RenderedLegacyRawMatchesExistingFullPrecisionExportAndRejectsRepeatedGains()
    {
        var recipe=Recipe();var d=LegacyRawInstantInput.Create(recipe);var pipeline=new RawMatchTiff16ProductPipeline(new Fake());
        for(ushort orientation=1;orientation<=8;orientation++)
        {
            var data=Enumerable.Range(0,48*32*3).Select(i=>.1f+(i%255)/510f).ToArray();
            var source=new FrozenRawMaster("synthetic.arw",new string('a',64),Guid.NewGuid(),new(48,32,data,"16","sRGB",orientation));
            var upright=RawImageOrientation.NormalizePixels(source.Image);
            var reference=pipeline.Render(upright,recipe.Look,recipe.Stack,film:recipe.Film).ProcessingPixels!;
            var master=LegacyRawInstantInput.Render(source,d,pipeline);
            Assert.IsFalse(upright.Rgb32.Span.SequenceEqual(reference.Rgb32.Span),"Non-default editing must visibly change pixels.");
            CollectionAssert.AreEqual(reference.Rgb32.ToArray(),ProfessionalRawPipeline.RenderOutput(master,d).Rgb32.ToArray());
            var reopened=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(d))!;
            Assert.AreEqual(d.DecodeKey,reopened.DecodeKey);CollectionAssert.AreEqual(reference.Rgb32.ToArray(),LegacyRawInstantInput.Render(source,reopened,pipeline).Image.Rgb32.ToArray());
        }
        Assert.Throws<InvalidDataException>(()=>(d with{ExposureEv=1}).Validate());
        Assert.Throws<InvalidDataException>(()=>(d with{WhiteBalance="Camera"}).Validate());
        Assert.Throws<InvalidDataException>(()=>(d with{DisplayTransform="LuminanceShoulderNeutralGamutV1"}).Validate());
        Assert.Throws<InvalidDataException>(()=>(d with{ProcessingVersion=5}).Validate());
    }
    [TestMethod]
    public void UnsupportedReferencesEnginesAndVersionsFailExplicitly()
    {
        var r=Recipe();
        Assert.Throws<NotSupportedException>(()=>LegacyRawInstantInput.Create(r with{LegacyRaw=r.LegacyRaw! with{Engine=ColorStudioMatchEngine.MatchV4Beta}}));
        Assert.Throws<NotSupportedException>(()=>LegacyRawInstantInput.Create(r with{Stack=new([new(Guid.NewGuid(),ColorStudioNodeType.ReferenceMatch,"Missing look")],ProcessingVersion:2)}));
        Assert.Throws<InvalidDataException>(()=>LegacyRawInstantInput.Create(r with{Version=3}));
        Assert.Throws<InvalidDataException>(()=>LegacyRawInstantInput.Create(r with{LegacyRaw=r.LegacyRaw! with{ProcessingVersion=1}}));
    }
    [TestMethod]
    public void CancelDuringEditsReleasesWorkAndNeverReturnsMaster()
    {
        var r=Recipe();var d=LegacyRawInstantInput.Create(r);var source=new FrozenRawMaster("synthetic.arw",new string('a',64),Guid.NewGuid(),new(500,400,Enumerable.Repeat(.2f,500*400*3).ToArray(),"16","sRGB"));
        using var cts=new CancellationTokenSource();var baseline=StudioGlobalResources.AccountedBytes;
        Assert.Throws<OperationCanceledException>(()=>LegacyRawInstantInput.Render(source,d,new(new Fake()),cts.Token,stage=>{if(stage=="CurrentEditsStart")cts.Cancel();}));
        Assert.AreEqual(baseline,StudioGlobalResources.AccountedBytes);
    }
    [TestMethod]
    public async Task CancellationInsideLargeEditLoopIsPromptAndReleasesAdmission()
    {
        var definition=LegacyRawInstantInput.Create(Recipe());var source=new FrozenRawMaster("synthetic.arw",new string('a',64),Guid.NewGuid(),new(1800,1200,Enumerable.Repeat(.2f,1800*1200*3).ToArray(),"16","sRGB"));
        using var cts=new CancellationTokenSource();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var baseline=StudioGlobalResources.AccountedBytes;var task=Task.Run(()=>LegacyRawInstantInput.Render(source,definition,new(new Fake()),cts.Token,s=>{if(s=="CurrentEditsStart")started.TrySetResult();}));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));await Task.Delay(15);var watch=System.Diagnostics.Stopwatch.StartNew();cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async()=>await task);Assert.IsLessThan(2000L,watch.ElapsedMilliseconds);Assert.AreEqual(baseline,StudioGlobalResources.AccountedBytes);
    }
    private sealed class Fake:IRawDecoder
    {
        public RawDecoderCapability GetCapability()=>throw new NotSupportedException();
        public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();
    }
}
