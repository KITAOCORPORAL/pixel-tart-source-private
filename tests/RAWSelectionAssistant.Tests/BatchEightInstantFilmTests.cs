using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchEightInstantFilmTests
{
    private static ProfessionalRawMaster Master(int w=33,int h=19,ushort orientation=1)
    {
        var d=new ProfessionalRawDefinition();var data=Enumerable.Range(0,w*h*3).Select(i=>(i%131)/170f).ToArray();
        return new(new(w,h,data,"16",ProfessionalRawPipeline.WorkingSpace,orientation),new("synthetic","Test","Synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));
    }
    [TestMethod]
    public void DisabledEffectPreservesPv4ExactlyAndNoLegacyFilmIsMigrated()
    {
        Assert.IsFalse(JsonSerializer.Serialize(new ProfessionalRawDefinition()).Contains("InstantFilm"));
        foreach(ushort o in Enumerable.Range(1,8).Select(i=>(ushort)i))
        {
            var m=Master(orientation:o);var d=new ProfessionalRawDefinition(ExposureEv:.7);var disabled=d with{InstantFilm=new(false)};
            CollectionAssert.AreEqual(ProfessionalRawPipeline.Render(m,d).Rgb32.ToArray(),ProfessionalRawPipeline.Render(m,disabled).Rgb32.ToArray());
            CollectionAssert.AreEqual(ProfessionalRawPipeline.RenderPreview(m,d,11).Rgb32.ToArray(),ProfessionalRawPipeline.RenderPreview(m,disabled,11).Rgb32.ToArray());
            Assert.AreEqual(4,disabled.ProcessingVersion);Assert.AreEqual(o,ProfessionalRawPipeline.RenderPreview(m,d,11).Orientation);
        }
    }
    [TestMethod]
    public void AllEightProfilesRoundTripFrozenSnapshotsAndTileWithoutSeamsForExif1To8()
    {
        foreach(var profile in InstantFilmProfiles.Available)foreach(ushort o in Enumerable.Range(1,8).Select(i=>(ushort)i))
        {
            var film=new InstantFilmDefinition(true,profile.Id,profile.ChemicalGeneration,Tier:"实验胶片");film=film with{DiceEnabled=true,Snapshot=InstantFilmRandom.Roll(film,0x1234567890abcdef)};
            var definition=new ProfessionalRawDefinition{InstantFilm=InstantFilmPipeline.Freeze(film)};
            var restored=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(definition))!;restored.Validate();
            var master=Master(orientation:o);var full=ProfessionalRawPipeline.RenderOutput(master,definition);var reopened=ProfessionalRawPipeline.RenderOutput(master,restored);
            CollectionAssert.AreEqual(full.Rgb32.ToArray(),reopened.Rgb32.ToArray());
            var size=ProfessionalRawPipeline.OutputSize(master,definition);
            for(var y=0;y<size.Height;y+=7)for(var x=0;x<size.Width;x+=11)
            {
                var w=Math.Min(11,size.Width-x);var h=Math.Min(7,size.Height-y);var tile=ProfessionalRawPipeline.RenderRegion(master,restored,x,y,w,h);
                for(var ty=0;ty<h;ty++)for(var tx=0;tx<w;tx++)for(var c=0;c<3;c++)Assert.AreEqual(full.Rgb32.Span[((y+ty)*size.Width+x+tx)*3+c],tile.Rgb32.Span[(ty*w+tx)*3+c]);
            }
        }
    }
    [TestMethod]
    public void RandomSubstreamsAndNormalizedGeometryAreDeterministicAndIndependentOfOutputSize()
    {
        var d=new InstantFilmDefinition(true,Tier:"轻微偶然");var first=InstantFilmRandom.Roll(d,42);var second=InstantFilmRandom.Roll(d,42);
        Assert.AreEqual(JsonSerializer.Serialize(first),JsonSerializer.Serialize(second));Assert.AreNotEqual(JsonSerializer.Serialize(first),JsonSerializer.Serialize(InstantFilmRandom.Roll(d,43)));
        var film=d with{DiceEnabled=true,Snapshot=first};var input=new ColorVector3(.2,.4,.7);
        Assert.AreEqual(InstantFilmPipeline.Apply(input,25d/100,60d/100,film),InstantFilmPipeline.Apply(input,250d/1000,600d/1000,film));
        Assert.AreEqual(InstantFilmPipeline.Apply(input,.5,.5,d),InstantFilmPipeline.Apply(input,.5,.5,film with{DiceEnabled=false}));
        Assert.AreEqual(8,InstantFilmProfiles.Available.Count);Assert.AreEqual(8,InstantFilmProfiles.ResearchOnly.Count);
    }
    [TestMethod]
    public void InvalidVersionsMalformedGeometryAndDuplicateWhiteBalanceAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>new InstantFilmDefinition(true,AlgorithmVersion:2).Validate());
        Assert.Throws<InvalidDataException>(()=>new InstantFilmDefinition(true,FilmId:"go").Validate());
        var d=new InstantFilmDefinition(true);var s=InstantFilmRandom.Roll(d,1) with{Geometry=[new("Roller",.5,.5,.1,.2,0)]};
        Assert.Throws<InvalidDataException>(()=>(d with{DiceEnabled=true,Snapshot=s}).Validate());
        Assert.Throws<InvalidDataException>(()=>new ProfessionalRawDefinition(5,"Rendered",ExposureEv:1,InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1").Validate());
        var jpeg=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
        var master=new ProfessionalRawMaster(new(1,1,new float[]{.2f,.4f,.6f},"8","sRGB"),Master().Contract with{DecodeKey=jpeg.DecodeKey});
        CollectionAssert.AreEqual(new float[]{.2f,.4f,.6f},ProfessionalRawPipeline.Render(master,jpeg).Rgb32.ToArray());
        Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.Render(master,new ProfessionalRawDefinition()));
    }
    [TestMethod]
    public void ColorDirectionsAreNotConflatedAndItype600SharedBaseIsExplicit()
    {
        var black=new ColorVector3(0,0,0);var white=new ColorVector3(1,1,1);
        InstantFilmDefinition D(string id,string generation)=>new(true,id,generation,Uneven:0,Unspread:0,Roller:0);
        var reclaimed=D("reclaimed-blue600","ReclaimedCreative");var duo=D("blue-duochrome600","DuochromeCreative");
        Assert.IsGreaterThan(.9,InstantFilmPipeline.Apply(white,0,0,reclaimed).X);
        Assert.IsLessThan(.03,InstantFilmPipeline.Apply(black,0,0,duo).X);
        Assert.IsGreaterThan(.7,InstantFilmPipeline.Apply(white,0,0,duo).Z);
        Assert.AreEqual(InstantFilmPipeline.Apply(white,.2,.3,D("600-color","ModernIntegralCreative")),InstantFilmPipeline.Apply(white,.2,.3,D("itype-color","ModernIntegralCreative")));
    }
    [TestMethod]
    public void TimelineBothPathsRequireFullDevelopmentAfterCompleteEjectionAndNeverExposeSkip()
    {
        foreach(var camera in new[]{false,true})foreach(var duration in new[]{3d,4d,5d})
        {
            var start=camera?InstantDevelopmentTimeline.EjectionSeconds:0;
            var first=InstantDevelopmentTimeline.Sample(start,camera,duration);Assert.AreEqual(InstantDevelopmentStage.Developing,first.Stage);Assert.IsFalse(first.CameraVisible);Assert.IsFalse(first.EditingAllowed);
            Assert.IsFalse(InstantDevelopmentTimeline.Sample(start+duration-.001,camera,duration).EditingAllowed);
            Assert.IsTrue(InstantDevelopmentTimeline.Sample(start+duration,camera,duration).EditingAllowed);
        }
        Assert.AreEqual(InstantDevelopmentStage.Ejecting,InstantDevelopmentTimeline.Sample(1.2,true).Stage);Assert.AreEqual(1d,InstantDevelopmentTimeline.Sample(1.25,true).Progress);
        Assert.Throws<ArgumentOutOfRangeException>(()=>InstantDevelopmentTimeline.Sample(0,false,2.9));
        Assert.Throws<ArgumentOutOfRangeException>(()=>InstantDevelopmentTimeline.Sample(0,false,5.1));
    }
    [TestMethod]
    [DoNotParallelize]
    public void GlobalBudgetCombinesOwnersAndWorkAndReleasesReservations()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var evicted=false;var baseline=StudioGlobalResources.AccountedBytes;
        try
        {
            StudioGlobalResources.Store(a,"Master",700L*1024*1024,()=>evicted=true);
            StudioGlobalResources.Store(b,"Master",700L*1024*1024,()=>{});Assert.IsTrue(evicted);
            using(var work=StudioGlobalResources.ReserveWork(1400L*1024*1024))Assert.IsLessThanOrEqualTo(StudioGlobalResources.TotalLimit,StudioGlobalResources.AccountedBytes);
            Assert.Throws<InvalidOperationException>(()=>StudioGlobalResources.ReserveWork(StudioGlobalResources.TotalLimit+1));
        }
        finally{StudioGlobalResources.Release(a,"Master");StudioGlobalResources.Release(b,"Master");}
        Assert.IsLessThanOrEqualTo(baseline,StudioGlobalResources.AccountedBytes);
    }
}
