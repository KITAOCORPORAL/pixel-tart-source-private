using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass][DoNotParallelize]
public class InstantColorComponentTests
{
    [TestMethod]public void FrozenContentAndVersionRejectTamperAndLegacyDoesNotChange()
    {
        var model=InstantColorComponents.Freeze("polaroid-modern-color-style-smooth-1");Assert.ThrowsExactly<InvalidDataException>(()=>(model with{ModelJson=model.ModelJson+" "}).Validate());Assert.ThrowsExactly<InvalidDataException>(()=>(model with{Version=2}).Validate());var old=CandidateRecipe.Create("modern-itype-v3");Assert.IsNull(old.ColorComponent);var c=new ColorVector3(.1,.2,.3);Assert.AreEqual(CandidateTone.ApplyLinear(c,old),CandidateTone.ApplyLinear(c,System.Text.Json.JsonSerializer.Deserialize<CandidateRecipe>(System.Text.Json.JsonSerializer.Serialize(old))!));
        var newRecipe=old with{AlgorithmVersion=4,ColorComponent=model};Assert.AreEqual(model,newRecipe.Freeze().ColorComponent);Assert.ThrowsExactly<InvalidDataException>(()=>(newRecipe with{Creative=new(Version:2,HighlightWarmth:.5)}).Validate());
    }
    [TestMethod]public void AnalyticModelIsContinuousNeutralAndStrengthZeroExact()
    {
        foreach(var id in new[]{"polaroid-modern-color-style-smooth-1","polaroid-modern-bw-style-smooth-1"})
        {
            var m=InstantColorComponents.Freeze(id);var a=InstantColorComponents.ApplyLinear(new(0,0,0),m,0);var b=InstantColorComponents.ApplyLinear(new(1e-7,0,0),m,0);Assert.IsTrue(Math.Abs(a.X-b.X)<1e-8);double prior=-1;for(int i=0;i<=256;i++){var c=InstantColorComponents.ApplyLinear(new(i/256d,i/256d,i/256d),m,0);Assert.IsTrue(c.X>=prior);Assert.AreEqual(c.X,c.Y,1e-12);Assert.AreEqual(c.X,c.Z,1e-12);prior=c.X;}
        }
    }
    [TestMethod]public void SpatialToneReplacementDoesNotDoubleOldBiasAndAmountZero()
    {
        var r=CandidateRecipe.Create("modern-itype-v3") with{AlgorithmVersion=4,ColorComponent=InstantColorComponents.Freeze("polaroid-modern-color-style-smooth-1"),Optics=new(DetailMix:0,HighlightDiffusion:0),Emulsion=new(Microstructure:0,DevelopmentUnevenness:0),Surface=new(Frame:false,PaperTexture:0)};using var source=new HighBitDepthImageBuffer(1,1,new float[]{.2f,.4f,.6f});using var pixels=CandidateSpatial.RenderRegion(source,r,0,0,1,1);using var result=pixels.Borrow();var expected=CandidateTone.Encode(InstantColorComponents.ApplyLinear(new(.2f,.4f,.6f),r.ColorComponent!,0));Assert.AreEqual(expected.X,result.Image.Rgb32.Span[0],1e-7);Assert.AreEqual(expected.Y,result.Image.Rgb32.Span[1],1e-7);using var zero=CandidateSpatial.RenderRegion(source,r with{Amount=0},0,0,1,1);using var z=zero.Borrow();CollectionAssert.AreEqual(source.Rgb32.ToArray(),z.Image.Rgb32.ToArray());
    }
    [TestMethod]public void EmitProductCandidateSamplesForFrozenOfflineParity()
    {
        var path=Environment.GetEnvironmentVariable("KITAO_INSTANT_COLOR_PARITY");if(path is null){Assert.Inconclusive("Explicit parity output required");return;}
        var rng=new Random(29);var rows=new List<object>();foreach(var id in new[]{"polaroid-modern-color-style-smooth-1","polaroid-modern-bw-style-smooth-1"}){var m=InstantColorComponents.Freeze(id);for(int i=0;i<4096;i++){var input=i<256?new ColorVector3(i/255d,i/255d,i/255d):new ColorVector3(rng.NextDouble(),rng.NextDouble(),rng.NextDouble());var output=CandidateTone.Encode(InstantColorComponents.ApplyLinear(input,m,0));rows.Add(new{id,input=new[]{input.X,input.Y,input.Z},output=new[]{output.X,output.Y,output.Z},m.ModelSha256});}}File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(rows));
    }

}
