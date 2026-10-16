using System.Text.Json;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass]
[DoNotParallelize]
public sealed class CandidateTests
{
    private static HighBitDepthImageBuffer Input(int w=37,int h=23)=>new(w,h,Enumerable.Range(0,w*h*3).Select(i=>(i%149)/148f).ToArray(),"16","sRGB",1);
    [TestMethod]
    public void ModernAliasesShareResponseBlueFamiliesDifferAndNoCalibratedProfilesExist()
    {
        var a=CandidateRecipe.Create("modern-itype-v3");var b=CandidateRecipe.Create("modern-600-v3");
        Assert.AreEqual(a.Response,b.Response);Assert.AreEqual(a.Profile.ChemicalFamily,b.Profile.ChemicalFamily);
        var c=new ColorVector3(.5,.5,.5);
        Assert.AreEqual(CandidateTone.ApplyLinear(c,a),CandidateTone.ApplyLinear(c,b));
        var reclaimed=CandidateRecipe.Create("reclaimed-blue-v3");var duo=CandidateRecipe.Create("blue-duochrome-v3");
        Assert.AreNotEqual(reclaimed.Profile.ChemicalFamily,duo.Profile.ChemicalFamily);
        Assert.AreNotEqual(CandidateTone.ApplyLinear(c,reclaimed),CandidateTone.ApplyLinear(c,duo));
        Assert.IsTrue(CandidateProfiles.Available.All(p=>!p.Provenance.CalibrationEligible&&p.Provenance.EvidenceStatus=="CreativeSimulation"));
        Assert.Throws<InvalidDataException>(()=>(a with{Provenance=a.Provenance with{CalibrationEligible=true}}).Validate());
    }
    [TestMethod]
    public void LinearLuminanceBwIsNeutralAndNonfiniteInputsAreRefused()
    {
        var r=CandidateRecipe.Create("modern-bw-v3");
        foreach(var v in new[]{new ColorVector3(1,0,0),new(0,1,0),new(0,0,1),new(-.2,2,.5)})
        {
            var x=CandidateTone.ApplyLinear(v,r);Assert.AreEqual(x.X,x.Y);Assert.AreEqual(x.Y,x.Z);
            Assert.IsTrue(double.IsFinite(x.X)&&x.X>=0&&x.X<=1);
        }
        var a=CandidateTone.ApplyLinear(new(1,0,0),r);var b=CandidateTone.ApplyLinear(new(0,1,0),r);Assert.IsTrue(b.X>a.X);
        Assert.Throws<InvalidDataException>(()=>CandidateTone.ApplyLinear(new(double.NaN,0,0),r));
        Assert.Throws<InvalidDataException>(()=>CandidateTone.Decode(double.PositiveInfinity));
        var image=CandidateSpatial.RenderRegion(Input(),r,0,0,37,23);
        for(var i=0;i<image.Rgb32.Length;i+=3){Assert.AreEqual(image.Rgb32.Span[i],image.Rgb32.Span[i+1]);Assert.AreEqual(image.Rgb32.Span[i],image.Rgb32.Span[i+2]);}
    }
    [TestMethod]
    public void FullTilesRegionsAndReopenAreBitExactAcrossHaloEdges()
    {
        foreach(var p in CandidateProfiles.Available)
        {
            var r=CandidateRecipe.Create(p.Id);var input=Input();
            var full=CandidateSpatial.RenderRegion(input,r,0,0,37,23,tileWidth:7,tileHeight:5);
            var other=CandidateSpatial.RenderRegion(input,r,0,0,37,23,tileWidth:19,tileHeight:13);
            CollectionAssert.AreEqual(full.Rgb32.ToArray(),other.Rgb32.ToArray());
            var restored=JsonSerializer.Deserialize<CandidateRecipe>(JsonSerializer.Serialize(r))!;restored.Validate();
            var reopened=CandidateSpatial.RenderRegion(input,restored,0,0,37,23);CollectionAssert.AreEqual(full.Rgb32.ToArray(),reopened.Rgb32.ToArray());
            for(var y=0;y<23;y+=6)for(var x=0;x<37;x+=9)
            {
                var w=Math.Min(9,37-x);var h=Math.Min(6,23-y);var tile=CandidateSpatial.RenderRegion(input,r,x,y,w,h,tileWidth:3,tileHeight:4);
                for(var dy=0;dy<h;dy++)for(var dx=0;dx<w;dx++)for(var c=0;c<3;c++)Assert.AreEqual(full.Rgb32.Span[((y+dy)*37+x+dx)*3+c],tile.Rgb32.Span[(dy*w+dx)*3+c]);
            }
            CollectionAssert.AreEqual(CandidateRenderer.Render(input,r).Rgb32.ToArray(),CandidateRenderer.Render(input,restored).Rgb32.ToArray());
        }
    }
    [TestMethod]
    public void StrengthIsSmoothZeroIsExactAndSeedChangesOnlyMaterial()
    {
        var input=Input();var r=CandidateRecipe.Create("modern-itype-v3");
        var zero=CandidateSpatial.RenderRegion(input,r with{Amount=0},0,0,37,23);CollectionAssert.AreEqual(input.Rgb32.ToArray(),zero.Rgb32.ToArray());
        var half=CandidateSpatial.RenderRegion(input,r with{Amount=.5},0,0,37,23);var full=CandidateSpatial.RenderRegion(input,r,0,0,37,23);
        for(var i=0;i<half.Rgb32.Length;i++)Assert.AreEqual((input.Rgb32.Span[i]+full.Rgb32.Span[i])/2,half.Rgb32.Span[i],1e-7);
        var a=CandidateTone.ApplyLinear(new(.2,.4,.6),r);var other=CandidateRecipe.Create(r.ProfileId,42);Assert.AreEqual(a,CandidateTone.ApplyLinear(new(.2,.4,.6),other));
        Assert.AreNotEqual(CandidateMaterial.Noise(r.Random.RootSeed,.2,.3,5,1),CandidateMaterial.Noise(other.Random.RootSeed,.2,.3,5,1));
        Assert.AreEqual(CandidateMaterial.Noise(42,.2,.3,5,1),CandidateMaterial.Noise(42,200d/1000,300d/1000,5,1));
    }
    [TestMethod]
    public void CancelFailureAndBudgetRefusalReleaseAllScratch()
    {
        var before=StudioGlobalResources.AccountedBytes;var r=CandidateRecipe.Create("modern-itype-v3");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>CandidateRenderer.Render(Input(),r,cancel.Token));
        Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
        var bad=Input();var values=bad.Rgb32.ToArray();values[20]=float.NaN;
        Assert.Throws<InvalidDataException>(()=>CandidateSpatial.RenderRegion(new(37,23,values,"16","sRGB",1),r,0,0,37,23));
        Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
        using(var lease=StudioGlobalResources.ReserveWork(StudioGlobalResources.TotalLimit-before))
            Assert.Throws<StudioBudgetException>(()=>CandidateSpatial.RenderRegion(Input(),r,0,0,37,23));
        Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
        Assert.AreEqual(2,CandidateSpatial.Plan().Halo);Assert.IsTrue(CandidateSpatial.Plan().ScratchBytes<1024*1024);
    }
}
