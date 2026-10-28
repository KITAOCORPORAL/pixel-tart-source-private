using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public class R54FrameAndNearBlackTests
{
    [TestMethod]public void AllTwelveOriginalResourcesHaveFrozenHashAndGeometry()
    {
        Assert.AreEqual(12,OriginalFrameLibrary.Frames.Count);
        foreach(var f in OriginalFrameLibrary.Frames){var art=OriginalFrameLibrary.Find(f.Id);var bytes=OriginalFrameLibrary.Load(art);Assert.AreEqual(art.Width*art.Height*4,bytes.Length);var recipe=CandidateRecipe.Create("modern-itype-v3") with{FrameArtwork=art,Geometry=new(WindowAspect:(art.X1-art.X0)*art.Width/((art.Y1-art.Y0)*art.Height))};var layout=OriginalFrameLibrary.Layout(100,100,recipe);Assert.IsTrue(layout.Left>0&&layout.Top>0&&layout.Bottom>0);Assert.ThrowsExactly<InvalidDataException>(()=>(art with{Sha256=new string('0',64)}).Validate());}
    }
    [TestMethod]public void FrameIndependentOfPhotoAmountAndRegionMatchesFullOutput()
    {
        var recipe=CandidateRecipe.Create("modern-itype-v3") with{FrameArtwork=OriginalFrameLibrary.Find("orig.01"),Amount=0};using var pixels=new HighBitDepthImageBuffer(16,16,Enumerable.Repeat(.3f,16*16*3).ToArray());using var image=CandidateImageLease.Adopt(pixels);using var session=new CandidateRenderSession(image,recipe);using var all=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height);using var borrowed=all.Borrow();using var one=session.RenderRegion(0,0,1,1);using var small=one.Borrow();CollectionAssert.AreEqual(borrowed.Image.Rgb32.Span[..3].ToArray(),small.Image.Rgb32.ToArray());Assert.IsTrue(borrowed.Image.Width>16);var serialized=System.Text.Json.JsonSerializer.Serialize(recipe);var restored=System.Text.Json.JsonSerializer.Deserialize<CandidateRecipe>(serialized)!.Freeze();Assert.AreEqual(recipe.FrameArtwork,restored.FrameArtwork);
    }
    [TestMethod]public void NearBlackProductionAnalyticDiscontinuityIsSeparateFromCubeApproximation()
    {
        var r=CandidateRecipe.Create("modern-itype-v3");var zero=CandidateTone.Encode(CandidateTone.ApplyLinear(new(0,0,0),r));var tiny=CandidateTone.Encode(CandidateTone.ApplyLinear(new(1e-7,0,0),r));Assert.IsTrue(Math.Abs(zero.X-tiny.X)>.01,"This executes analytic production tone, no LUT involved");
        var bw=CandidateRecipe.Create("modern-bw-v3");var a=CandidateTone.Encode(CandidateTone.ApplyLinear(new(0,0,0),bw));var b=CandidateTone.Encode(CandidateTone.ApplyLinear(new(1e-7,0,0),bw));Assert.IsTrue(Math.Abs(a.X-b.X)<1e-5);
    }
}
