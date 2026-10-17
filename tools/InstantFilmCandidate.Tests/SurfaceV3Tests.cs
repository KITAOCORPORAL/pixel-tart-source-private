using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize]public sealed class SurfaceV3Tests
{
 [TestMethod]public void CircleHasPartialCoverageAndSeparateWindowDistance()
 {
  var l=InstantPaperLayout.Create(64,48,new InstantFilmDefinition(true,AlgorithmVersion:2){PaperGeometry=new()});
  Assert.IsTrue(CandidateExtensions.WindowDistance(l.Left+l.WindowWidth*.5,l.Top+l.WindowHeight*.5,l,"Circle")>0);
  Assert.IsTrue(CandidateExtensions.WindowDistance(l.Left,l.Top,l,"Circle")<0);
  var d=CandidateExtensions.WindowDistance(l.Left+l.WindowWidth*.5,l.Top+.25,l,"Rectangle");Assert.AreEqual(.25,d,1e-12);Assert.IsTrue(Math.Clamp(d+.5,0,1)<1);
 }
 [TestMethod]public void BilinearZoom3DiffersFromNearest2ButRegionsStayIdentical()
 {
  using var source=CandidateImageLease.Adopt(new(41,37,Enumerable.Range(0,41*37*3).Select(i=>(i%7)/6f).ToArray(),"16","sRGB",1));
  var r=CandidateRecipe.Create("modern-itype-v3") with{Paper=new(Version:3,Zoom:1.37,Window:"Circle",WindowPressed:.8,WindowHighlight:.7)};
  using var session=new CandidateRenderSession(source,r);using var full=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height);using var f=full.Borrow();
  session.StreamStrips((top,strip)=>{for(int y=0;y<strip.Height;y++)for(int x=0;x<strip.Width*3;x++)Assert.AreEqual(f.Image.Rgb32.Span[(top+y)*strip.Width*3+x],strip.Rgb32.Span[y*strip.Width*3+x]);},7);
  using var oldSession=new CandidateRenderSession(source,r with{Paper=r.Paper! with{Version=2}});using var old=oldSession.RenderRegion(0,0,oldSession.Layout.Width,oldSession.Layout.Height);using var b=old.Borrow();Assert.IsFalse(f.Image.Rgb32.Span.SequenceEqual(b.Image.Rgb32.Span));
 }
}
