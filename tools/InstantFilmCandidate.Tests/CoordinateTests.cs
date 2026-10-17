using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize]
public sealed class CoordinateTests
{
 [TestMethod] public void StripsRegionsAndFullOutputMatchWithoutLocalCoordinateReset()
 {
  var input=new HighBitDepthImageBuffer(43,31,Enumerable.Range(0,43*31*3).Select(i=>(i%251)/250f).ToArray(),"16","sRGB",1);
  using var source=CandidateImageLease.Adopt(input);var recipe=CandidateRecipe.Create("reclaimed-blue-v3");using var session=new CandidateRenderSession(source,recipe);
  using var fullOwner=CandidateRenderer.Render(source,recipe);using var full=fullOwner.Borrow();
  session.StreamStrips((top,strip)=>{for(int y=0;y<strip.Height;y++)for(int x=0;x<strip.Width*3;x++)Assert.AreEqual(full.Image.Rgb32.Span[(top+y)*strip.Width*3+x],strip.Rgb32.Span[y*strip.Width*3+x]);},7);
  using var regionOwner=session.RenderRegion(2,3,9,11);using var region=regionOwner.Borrow();
  for(int y=0;y<11;y++)for(int x=0;x<9;x++)for(int c=0;c<3;c++)Assert.AreEqual(full.Image.Rgb32.Span[((3+y)*full.Image.Width+2+x)*3+c],region.Image.Rgb32.Span[(y*9+x)*3+c]);
 }
 [TestMethod] public void PreviewIsLinearLightAreaReductionOfFullSourceEffect()
 {
  using var source=CandidateImageLease.Adopt(new(29,23,Enumerable.Range(0,29*23*3).Select(i=>(i%119)/118f).ToArray(),"16","sRGB",1));
  using var session=new CandidateRenderSession(source,CandidateRecipe.Create("modern-itype-v3"));using var fullOwner=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height);using var full=fullOwner.Borrow();using var previewOwner=session.RenderPreview(13);using var preview=previewOwner.Borrow();
  var fw=full.Image.Width;var fh=full.Image.Height;var pw=preview.Image.Width;var ph=preview.Image.Height;
  for(int y=0;y<ph;y++)for(int x=0;x<pw;x++)for(int c=0;c<3;c++)
  {double sum=0;double ax=x*fw/(double)pw,bx=(x+1)*fw/(double)pw,ay=y*fh/(double)ph,by=(y+1)*fh/(double)ph;
   for(int sy=(int)ay;sy<Math.Min(fh,(int)Math.Ceiling(by));sy++)for(int sx=(int)ax;sx<Math.Min(fw,(int)Math.Ceiling(bx));sx++)sum+=CandidateTone.Decode(full.Image.Rgb32.Span[(sy*fw+sx)*3+c])*(Math.Min(sx+1,bx)-Math.Max(sx,ax))*(Math.Min(sy+1,by)-Math.Max(sy,ay));
   Assert.AreEqual(CandidateTone.Encode(sum/((bx-ax)*(by-ay))),preview.Image.Rgb32.Span[(y*pw+x)*3+c],1e-6);
  }
 }
}
