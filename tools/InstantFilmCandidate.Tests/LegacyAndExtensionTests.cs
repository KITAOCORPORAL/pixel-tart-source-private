using System.Text.Json;using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize]
public sealed class LegacyAndExtensionTests
{
 [TestMethod] public void SixLegacyFormulaIdentitiesMatchProvidedGrayTableWithoutInventedStockNames()
 {
  var file=Path.Combine(TestRoot(),"docs/research/instant-v3-live-20261017/01_intake/instant_stocks_summary.json");using var json=JsonDocument.Parse(File.ReadAllText(file));double worst=0;int count=0;
  foreach(var entry in json.RootElement.EnumerateObject())
  {var id=entry.Name[5]-'0';foreach(var point in entry.Value.GetProperty("neutral_ramp").EnumerateObject()){double x=double.Parse(point.Name,System.Globalization.CultureInfo.InvariantCulture);var got=Womb01351Reference.Apply(new(x,x,x),"Womb01351.Stock"+id);var expected=point.Value.EnumerateArray().Select(v=>v.GetDouble()).ToArray();var values=new[]{got.X,got.Y,got.Z};for(int c=0;c<3;c++)worst=Math.Max(worst,Math.Abs(values[c]-expected[c]));count++;}}
  Assert.IsTrue(worst<=.000051,$"Provided values rounded four decimals, max={worst}; formula reproduction only, not app runtime");Assert.AreEqual(42,count);
  Assert.IsTrue(Womb01351Reference.Apply(new(0,0,0),"Womb01351.Stock2").X<0);Assert.IsTrue(Womb01351Reference.Apply(new(1,1,1),"Womb01351.Stock2").X>1);
 }
 [TestMethod] public void ExtensionsAreDeterministicFiniteAndBlackWhiteDoesNotPickUpColorTemperature()
 {
  var input=new HighBitDepthImageBuffer(31,29,Enumerable.Range(0,31*29*3).Select(i=>(i%173)/172f).ToArray(),"16","sRGB",1);using var source=CandidateImageLease.Adopt(input);
  foreach(var kind in new[]{"None","Unspread","Bands","Fog","Islands","Crescent","BlueFailure"})
  {
   var r=CandidateRecipe.Create("modern-bw-v3",999) with{Creative=new(TemperatureAppearance:1,ShadowCast:1,HighlightWarmth:1,Fog:1,FrameDrift:1,Fade:1,LensVignette:1,DevelopmentEdge:1),Defects=new(Kind:kind,Amount:1,Crystal:1,Leak:1),Paper=new(Pattern:"Foil",Window:"Circle",Reflection:1,Emboss:1,EdgeLine:1)};
   using var session=new CandidateRenderSession(source,r);using var a=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height);using var b=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height);using var aa=a.Borrow();using var bb=b.Borrow();CollectionAssert.AreEqual(aa.Image.Rgb32.ToArray(),bb.Image.Rgb32.ToArray());Assert.IsTrue(aa.Image.Rgb32.Span.ToArray().All(x=>float.IsFinite(x)&&x>=0&&x<=1));
   var bw=CandidateExtensions.Creative(new(.2,.2,.2),.2,.3,r,"NeutralBW");Assert.AreEqual(bw.X,bw.Y);Assert.AreEqual(bw.Y,bw.Z);
  }
 }
 [TestMethod] public void UnknownVersionsAndUnreviewedParametersRejectWithoutOldRecipeMigration()
 {
  var r=CandidateRecipe.Create("modern-itype-v3");Assert.Throws<InvalidDataException>(()=>(r with{Creative=new(Version:99)}).Validate());Assert.Throws<InvalidDataException>(()=>(r with{Paper=new(Reflection:double.NaN)}).Validate());Assert.Throws<InvalidDataException>(()=>(r with{Defects=new(Streams:"random-global")} ).Validate());
  var raw=new ProfessionalRawDefinition();Assert.AreEqual(4,raw.ProcessingVersion);Assert.AreEqual(1,new InstantFilmDefinition().AlgorithmVersion);
 }
 private static string TestRoot(){for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"RAWSelectionAssistant.sln")))return p.FullName;throw new DirectoryNotFoundException();}
}
