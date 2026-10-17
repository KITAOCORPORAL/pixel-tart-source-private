using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize] public sealed class CompositionVersionTests
{
 [TestMethod] public void WombV1RemainsLegacyOverrideAndNewComponentPreservesUpstreamEffects()
 {
  var image=new HighBitDepthImageBuffer(23,19,Enumerable.Range(0,23*19*3).Select(i=>(i%137)/136f).ToArray(),"32f","sRGB",1);
  float[] Render(CandidateRecipe r){using var owner=CandidateSpatial.RenderRegion(image,r,0,0,23,19);using var b=owner.Borrow();return b.Image.Rgb32.ToArray();}
  foreach(var stock in Enumerable.Range(0,6).Select(i=>"Womb01351.Stock"+i))
  {
   var old=CandidateRecipe.Create("modern-itype-v3") with{Creative=new(Stock:stock)};CollectionAssert.AreEqual(Render(old),Render(old with{Response=old.Response with{ExposureEv=1},Optics=old.Optics with{DetailMix=1},Emulsion=old.Emulsion with{ExpiredAging=1}}));
   var current=old with{Creative=old.Creative! with{Version=2}};Assert.IsFalse(Render(current).SequenceEqual(Render(current with{Response=current.Response with{ExposureEv=1}})));Assert.IsFalse(Render(current).SequenceEqual(Render(current with{Optics=current.Optics with{DetailMix=1}})));
  }
 }
 [TestMethod] public void NoneShapeIgnoresSelectedShapeStrengthAndEmulsionRemainsIndependent()
 {
  var r=CandidateRecipe.Create("modern-itype-v3") with{Defects=new(Kind:"None",Amount:1)};var c=new ColorVector3(.2,.3,.4);Assert.AreEqual(c,CandidateExtensions.Defects(c,.4,.3,r,"Color"));Assert.AreEqual(0,r.Emulsion.DevelopmentDefects);
 }
}
