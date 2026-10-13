using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Tests;
[TestClass]
[DoNotParallelize]
public sealed class BatchElevenFoundationTests
{
    [TestMethod]
    public void BandedPointwiseStackAndBothFilmSpatialVersionsAreBitExact()
    {
        var source=new HighBitDepthImageBuffer(73,139,Enumerable.Range(0,73*139*3).Select(i=>(i%997)/997f).ToArray(),"16","sRGB");
        var pipeline=new RawMatchTiff16ProductPipeline(new Fake());
        foreach(var spatial in new[]{1,2})
        {
            var film=new PixelTartFilmSettings(true,"PT-W01",GrainAmount:17,VignetteAmount:25,SurfaceAmount:20,TextureId:"Paper",TextureAmount:35,Seed:61,SpatialVersion:spatial);
            var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Tone",NumericParameters:new Dictionary<string,double>{{"exposure",.75},{"saturation",12}}),new(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"WB",NumericParameters:new Dictionary<string,double>{{"temperature",17},{"tint",-8}})],ProcessingVersion:2);
            var expected=pipeline.Render(source,null,stack,film:film).ProcessingPixels!;
            var actual=BoundedLegacyRawRenderer.Render(source,null,stack,film);
            CollectionAssert.AreEqual(expected.Rgb32.ToArray(),actual.Rgb32.ToArray());
        }
    }
    [TestMethod]
    public void FullImageNodesRemainExplicitAndFujiPointwiseEstimateFitsWithoutChangingLimit()
    {
        var point=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"EV",NumericParameters:new Dictionary<string,double>{{"exposure",1}})]);
        var band=BoundedLegacyRawRenderer.Plan(7752,5184,null,point,new(true,GrainAmount:5));Assert.IsTrue(band.Bounded);Assert.IsLessThan(StudioGlobalResources.TotalLimit,band.RequiredBytes);
        var spatial=BoundedLegacyRawRenderer.Plan(7752,5184,null,point,new(true,BloomAmount:5));Assert.IsFalse(spatial.Bounded);Assert.IsGreaterThan(StudioGlobalResources.TotalLimit,spatial.RequiredBytes);
        var details=BoundedLegacyRawRenderer.Plan(7752,5184,null,new([new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details")]),null);Assert.IsFalse(details.Bounded);
        var develop=BoundedLegacyRawRenderer.Plan(7752,5184,null,new([new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",20}})]),null);Assert.IsFalse(develop.Bounded);
    }
    [TestMethod]
    public void CacheReplacementAndReleaseRetainPinnedOldGenerationUntilTaskEnds()
    {
        var owner=Guid.NewGuid();var baseline=StudioGlobalResources.AccountedBytes;
        StudioGlobalResources.Store(owner,"Master",100,()=>{});var pin=StudioGlobalResources.Pin(owner,"Master");
        StudioGlobalResources.Store(owner,"Master",200,()=>{});Assert.AreEqual(baseline+300,StudioGlobalResources.AccountedBytes);
        StudioGlobalResources.Release(owner,"Master");Assert.AreEqual(baseline+100,StudioGlobalResources.AccountedBytes);pin.Dispose();pin.Dispose();Assert.AreEqual(baseline,StudioGlobalResources.AccountedBytes);
    }
    [TestMethod]
    public void BoundedNodesIncludingLegacyDevelopAndNumericFilmMatchFullImage()
    {
        var source=new HighBitDepthImageBuffer(67,133,Enumerable.Range(0,67*133*3).Select(i=>(i%251)/251f).ToArray(),"16","sRGB");
        var nodes=new[]{new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"EV",NumericParameters:new Dictionary<string,double>{{"exposure",.4}}),new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Preset",NumericParameters:new Dictionary<string,double>{{"exposure",.3},{"saturation",5}}),new(Guid.NewGuid(),ColorStudioNodeType.TransitionBlend,"Blend",NumericParameters:new Dictionary<string,double>{{"amount",.2}}),new(Guid.NewGuid(),ColorStudioNodeType.Film,"Numeric film",NumericParameters:new Dictionary<string,double>{{"grain_amount",20},{"vignette_amount",10}})};
        foreach(var node in nodes)
        {
            var stack=new ColorAdjustmentStack([node]);var expected=new RawMatchTiff16ProductPipeline(new Fake()).Render(source,null,stack).ProcessingPixels!;
            CollectionAssert.AreEqual(expected.Rgb32.ToArray(),BoundedLegacyRawRenderer.Render(source,null,stack,null).Rgb32.ToArray());
        }
    }
    [TestMethod]
    public void ErrorsAreStableAndDoNotExposePathsOrCredentials()
    {
        var rejected=StudioProcessingFailure.From(new StudioBudgetException(300,200),"Rebuild");Assert.AreEqual("STUDIO_BUDGET_REJECTED",rejected.Code);Assert.AreEqual(300L,rejected.RequiredBytes);
        var raw=StudioProcessingFailure.From(new RawDecodeException("DecodeFailed","private path"){Stage="OpenFile",NativeCode=-2,NativeName="FileUnsupported"},"Decode");Assert.AreEqual("OpenFile",raw.Stage);Assert.AreEqual(-2,raw.NativeCode);Assert.IsFalse(raw.UserMessage.Contains("private"));
    }
    [TestMethod]
    public void RenderedSrgbGreyAndBoundaryValuesNeverDoubleEncode()
    {
        var d=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
        var data=new float[]{0,0,0,.04045f,.04045f,.04045f,.18f,.18f,.18f,1,0,1,1,1,1};
        var m=new ProfessionalRawMaster(new(5,1,data,"16","sRGB"),new("test","test","test","hash",d.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));
        CollectionAssert.AreEqual(data,ProfessionalRawPipeline.RenderOutput(m,d).Rgb32.ToArray());CollectionAssert.AreEqual(data,ProfessionalRawPipeline.RenderRegion(m,d,0,0,5,1).Rgb32.ToArray());
        Assert.Throws<InvalidDataException>(()=>(d with{ExposureEv=1}).Validate());
        var before=ProfessionalRawPipeline.Display(new(.18,.18,.18));Assert.IsTrue(double.IsFinite(before.X)&&before.X>=0&&before.X<=1);
    }
    private sealed class Fake:IRawDecoder{public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();}
}
