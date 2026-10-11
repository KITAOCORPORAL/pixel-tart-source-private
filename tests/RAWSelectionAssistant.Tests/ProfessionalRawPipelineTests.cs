using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class ProfessionalRawPipelineTests
{
    [TestMethod]
    public void ExposureIsLinearAndDisplayIsBoundedMonotonicWithSoftShoulder()
    {
        Assert.AreEqual(new ColorVector3(-.2,.36,4),ProfessionalRawPipeline.ApplyExposure(new(-.1,.18,2),1));
        var previous=-1d;foreach(var value in new[]{0d,.01,.18,.5,.75,1,2,8}){var result=ProfessionalRawPipeline.Display(new(value,value,value));Assert.IsTrue(result.X>=previous);Assert.IsTrue(result.X is >=0 and <=1);Assert.AreEqual(result.X,result.Y,.00002);previous=result.X;}
        Assert.IsTrue(ProfessionalRawPipeline.Display(new(1,1,1)).X<1);Assert.Throws<ArgumentOutOfRangeException>(()=>ProfessionalRawPipeline.ApplyExposure(new(),double.NaN));
    }
    [TestMethod]
    public void PreviewSamplesExactlySameRenderedPixelsAndDefinitionMustMatchDecode()
    {
        var definition=new ProfessionalRawDefinition(ExposureEv:1);var samples=Enumerable.Range(0,12*8*3).Select(i=>(i%31)/25f).ToArray();
        var contract=new ProfessionalRawContract("test","synthetic","synthetic",new string('a',64),definition.DecodeKey,[1,1,1,1],[1,1,1,1],[1,0,0,0,0,1,0,0,0,0,1,0],65535);
        var master=new ProfessionalRawMaster(new HighBitDepthImageBuffer(12,8,samples,"16","LibRaw Linear ProPhoto D65"),contract);
        var full=ProfessionalRawPipeline.Render(master,definition);var preview=ProfessionalRawPipeline.Render(master,definition,6);
        for(var y=0;y<preview.Height;y++)for(var x=0;x<preview.Width;x++)for(var c=0;c<3;c++){var sx=(int)((x+.5)*12/preview.Width);var sy=(int)((y+.5)*8/preview.Height);Assert.AreEqual(full.Rgb32.Span[(sy*12+sx)*3+c],preview.Rgb32.Span[(y*preview.Width+x)*3+c]);}
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.Render(master,definition with{WhiteBalance="CameraGains",RedGain=2}));
        Assert.Throws<InvalidDataException>(()=>(definition with{ProcessingVersion=3}).Validate());
    }
    [TestMethod]
    public async Task DefinitionAndInputPersistInVersionAndV1SessionWithoutOldStackMigration()
    {
        using var temp=new TempDirectory();var definition=new ProfessionalRawDefinition(ExposureEv:.75,WhiteBalance:"CameraGains",RedGain:1.2);var id=Guid.NewGuid();var version=new StudioEditVersion(id,"PV4",DateTimeOffset.UtcNow,null,null,new string('a',64),null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto){RawDefinition=definition};
        var target=new ColorStudioSessionTarget(Guid.NewGuid(),temp.Combine("photo.arw"),null,true,null,null,null,SourceHash:new string('a',64),EditVersions:[version],ActiveEditVersionId:id,RawDefinition:definition);
        var path=temp.Combine("session.ptstudio.json");await ColorStudioSessionStore.SaveAsync(path,new([target],target.Id));var read=await ColorStudioSessionStore.LoadAsync(path);Assert.AreEqual(definition,read.Targets[0].RawDefinition);Assert.AreEqual(4,read.Targets[0].EditVersions![0].ProcessingVersion);Assert.AreEqual(1,read.Version);
    }
    [TestMethod]
    public void WhiteBalanceSelectsCameraOnceAndRejectsRenderedInputsAndMissingConfiguration()
    {
        double[] camera=[2,1,1.5,0];
        CollectionAssert.AreEqual(new double[]{2,1,1.5,1},ProfessionalRawDecoder.ResolveWhiteBalance(camera,false,new()));
        CollectionAssert.AreEqual(new double[]{4,1,1.5,1},ProfessionalRawDecoder.ResolveWhiteBalance(camera,false,new(WhiteBalance:"CameraGains",RedGain:2)));
        CollectionAssert.AreEqual(new double[]{2,1,1.5,0},camera,"Must not mutate metadata");
        Assert.Throws<NotSupportedException>(()=>ProfessionalRawDecoder.ResolveWhiteBalance(camera,true,new()));
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawDecoder.ResolveWhiteBalance([0,1,1,1],false,new()));
        Assert.Throws<InvalidDataException>(()=>(new ProfessionalRawDefinition(RedGain:double.NaN)).Validate());
        Assert.Throws<InvalidDataException>(()=>(new ProfessionalRawDefinition(ExposureEv:11)).Validate());
    }
    [TestMethod]
    public void SignedExtendedIntermediatesRemainAndWrongDomainsAreRejected()
    {
        var definition=new ProfessionalRawDefinition();
        var contract=new ProfessionalRawContract("test","synthetic","synthetic",new string('a',64),definition.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535);
        var master=new ProfessionalRawMaster(new HighBitDepthImageBuffer(2,1,new float[]{-.1f,0,2,4,.01f,-.2f},workingColorSpace:ProfessionalRawPipeline.WorkingSpace),contract);
        var rendered=ProfessionalRawPipeline.Render(master,definition);
        Assert.AreEqual(-.1f,master.Image.Rgb32.Span[0]);Assert.AreEqual(4f,master.Image.Rgb32.Span[3]);
        Assert.IsTrue(rendered.Rgb32.ToArray().All(v=>float.IsFinite(v)&&v is >=0 and <=1));
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.Render(master with{Contract=contract with{WhiteBalanceApplied=false}},definition));
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.Render(master with{Image=new(2,1,new float[6],workingColorSpace:"sRGB")},definition));
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.Render(master with{Image=new(1,1,new float[]{float.NaN,0,1},workingColorSpace:ProfessionalRawPipeline.WorkingSpace)},definition));
    }
    [TestMethod]
    public async Task UnsupportedFileAndRenderedJpegFailRatherThanLegacyFallback()
    {
        using var temp=new TempDirectory();var raw=temp.Combine("bad.arw");await File.WriteAllBytesAsync(raw,[1,2,3]);
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ProfessionalRawDecoder().DecodeAsync(raw,new()));
        await Assert.ThrowsAsync<NotSupportedException>(()=>new ProfessionalRawDecoder().DecodeAsync(temp.Combine("rendered.jpg"),new()));
    }
    [TestMethod]
    public void SyntheticNeutralExposureStaircaseAndBlackBoundaryAreMathematical()
    {
        foreach(var v in new[]{-.0001,0,1d/65535,.001,.18,.5,.99,1,2})
            foreach(var ev in new[]{-2d,-1,0,1,2})
            {var actual=ProfessionalRawPipeline.ApplyExposure(new(v,v,v),ev);Assert.AreEqual(v*Math.Pow(2,ev),actual.X,1e-12);Assert.IsTrue(ProfessionalRawPipeline.Display(actual).X is >=0 and <=1);}
    }
    [TestMethod]
    public void SessionRejectsMixedLegacyStackAndMismatchedSelectedDefinition()
    {
        var target=new ColorStudioSessionTarget(Guid.NewGuid(),Path.GetFullPath("fake.arw"),null,true,null,null,null,RawDefinition:new());
        var id=Guid.NewGuid();var version=new StudioEditVersion(id,"old",DateTimeOffset.UtcNow,null,null,null,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);
        Assert.Throws<InvalidDataException>(()=>new ColorStudioSessionDocument([target with{EditVersions=[version],ActiveEditVersionId=id}],target.Id).Normalize());
    }
}
