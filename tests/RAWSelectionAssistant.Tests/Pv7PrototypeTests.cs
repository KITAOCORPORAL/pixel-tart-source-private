using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorScience;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class Pv7PrototypeTests
{
 [TestMethod] public void ExposureNeutralMonotonicSingleTransferAndQuantizationAreExplicit()
 {
  double last=-1;
  for(int i=0;i<=40;i++){var v=i/20f;var result=Pv7ColorPrototype.Render([v,v,v],new());Assert.AreEqual(result.DisplayEncoded[0],result.DisplayEncoded[1]);Assert.AreEqual(result.DisplayEncoded[1],result.DisplayEncoded[2]);Assert.IsTrue(result.DisplayEncoded[0]>=last);last=result.DisplayEncoded[0];var encoded=Pv7ColorPrototype.Export16(result);Assert.AreEqual((int)Math.Round(result.DisplayEncoded[0]*65535,MidpointRounding.ToEven),(int)encoded[0]);}
  var gray=Pv7ColorPrototype.Render([.18f,.18f,.18f],new());Assert.AreEqual(Pv7ColorPrototype.EncodeSrgb(.18f),gray.DisplayEncoded[0],1e-7);
  var ev=Pv7ColorPrototype.Render([.09f,.09f,.09f],new(ExposureEv:1));CollectionAssert.AreEqual(gray.DisplayEncoded,ev.DisplayEncoded);
  var encodedInput=Pv7ColorPrototype.Render(gray.DisplayEncoded,new(InputDomain:Pv7InputDomain.EncodedSrgbD65));for(int i=0;i<3;i++)Assert.AreEqual(gray.DisplayEncoded[i],encodedInput.DisplayEncoded[i],1e-6);
 }
 [TestMethod] public void ExtendedPrimariesSkinAndBakedWhiteBalanceFailClosed()
 {
  var data=new float[]{-.1f,.2f,1.5f,1,0,0,0,1,0,0,0,1,.4f,.25f,.18f};var r=Pv7ColorPrototype.Render(data,new());Assert.IsTrue(r.DisplayEncoded.All(x=>float.IsFinite(x)&&x>=0&&x<=1));Assert.IsTrue(r.WorkingLinear.Minimum<0);Assert.IsTrue(r.WorkingLinear.Maximum>1);
  Assert.Throws<InvalidDataException>(()=>Pv7ColorPrototype.Render(data,new(RedGain:2)));var wb=Pv7ColorPrototype.Render([.1f,.2f,.3f],new(WhiteBalanceAlreadyApplied:false,RedGain:2));Assert.AreEqual(.2f,wb.WorkingLinear.Minimum,1e-6);
  Assert.Throws<NotSupportedException>(()=>Pv7ColorPrototype.Render(data,new(MatrixId:"LibRawProPhotoD65")));Assert.Throws<ArgumentException>(()=>Pv7ColorPrototype.Render([float.NaN,1,0],new()));Assert.AreNotEqual(new Pv7Recipe().CacheKey,new Pv7Recipe(ExposureEv:1).CacheKey);
 }
 [TestMethod] public void SyntheticD50D65AdaptationAndUnknownProfileRefusal()
 {
  var profile=new SyntheticMatrixProfile("SyntheticLinearProPhotoD50V1",.3457,.3585,1,"AnalystSyntheticFixtureV1");var white=SyntheticProfileBoundary.ToLinearSrgb(new(1,1,1),profile);Assert.AreEqual(1,white.X,.003);Assert.AreEqual(1,white.Y,.003);Assert.AreEqual(1,white.Z,.003);
  var back=LinearRgbColorScience.Convert(white,WorkingColorSpace.LinearSRgb,WorkingColorSpace.ProPhotoRgb);Assert.AreEqual(1,back.X,.003);Assert.Throws<NotSupportedException>(()=>SyntheticProfileBoundary.ToLinearSrgb(new(1,1,1),profile with{Provenance="Manufacturer"}));
 }
}
