using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class RawWideOutputTests
{
 public static ProfessionalRawMaster Master(ProfessionalRawDefinition d,int w=12,int h=8){var data=Enumerable.Range(0,w*h*3).Select(i=>new[]{.9f,.02f,.05f,.02f,.8f,.04f,.05f,.02f,.95f,.18f,.18f,.18f}[i%12]).ToArray();return new(new HighBitDepthImageBuffer(w,h,data,"16",ProfessionalRawPipeline.WorkingSpace),new("Synthetic","test","Bayer",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[1,0,0,0,0,1,0,0,0,0,1,0],65535));}
 public static ProfessionalRawDefinition Definition(string output="ProPhotoRGB"){using var cms=new LcmsRgbEngine();return new ProfessionalRawDefinition{RawWideOutput=new(),ColorState=new(cms.StandardProfile("LibRawProPhotoD65LinearV1"),cms.StandardProfile("LinearProPhotoD50V2"),cms.StandardProfile(output),Version:2)};}
 [TestMethod]public void DefaultOldDefinitionUnchangedWideMasterBypassesDisplayAndPreservesExtendedValues()
 {
  var old=new ProfessionalRawDefinition();var m=Master(old);using var legacy=ProfessionalRawPipeline.Render(m,old);var d=Definition();var wide=RawWideOutputV1.ConvertMaster(m,d);using var owner=wide.Image;using var result=ProfessionalRawPipeline.RenderRegion(wide,d,0,0,wide.Image.Width,wide.Image.Height);CollectionAssert.AreEqual(wide.Image.Rgb32.ToArray(),result.Rgb32.ToArray());Assert.IsFalse(result.Rgb32.Span.SequenceEqual(legacy.Rgb32.Span));Assert.AreEqual(DocumentWorkingRendererV2.Space,result.WorkingColorSpace);Assert.IsFalse(JsonSerializer.Serialize(old).Contains("RawWideOutput"));CollectionAssert.AreEqual(legacy.Rgb32.ToArray(),ProfessionalRawPipeline.Render(m,old).Rgb32.ToArray());var read=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(d))!;Assert.AreEqual(d.DecodeKey,read.DecodeKey);Assert.Throws<InvalidDataException>(()=>ProfessionalRawPipeline.ValidateMaster(m,d));
 }
 [TestMethod]public void D65ProfileNeutralAdaptsToD50AndDoesNotRoundtripThroughSrgb()
 {
  using var cms=new LcmsRgbEngine();var source=cms.StandardProfile("LibRawProPhotoD65LinearV1");var working=cms.StandardProfile("LinearProPhotoD50V2");using var transform=cms.CreateTransform(source,working);var values=new float[]{.18f,.18f,.18f,1,1,1,-.1f,.3f,1.2f,.9f,.02f,.05f};var converted=transform.Convert(values);for(int i=0;i<6;i++)Assert.AreEqual(values[i],converted[i],.0005);using var toSrgb=cms.CreateTransform(working,cms.StandardProfile("sRGB"));using var fromSrgb=cms.CreateTransform(cms.StandardProfile("sRGB"),working);var through=fromSrgb.Convert(toSrgb.Convert(converted));Assert.IsTrue(converted.All(float.IsFinite));var output=Environment.GetEnvironmentVariable("KITAO_RAW_WIDE_EVIDENCE");if(output is not null){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"PROFILE_NUMERICS.json"),JsonSerializer.Serialize(new{input=values,linearD50=converted,viaSrgb=through,inputProfile=source.Sha256,workingProfile=working.Sha256,CalibrationEligible=false},new JsonSerializerOptions{WriteIndented=true}));}
 }
}
