using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Tests;
[TestClass] public sealed class PhotoWideV1Tests
{
 [TestMethod] public void ThresholdsNegativeDomainAndExplicitNitsDoNotClampSilently()
 {
  foreach(var x in new[]{-.2,0,.00262409-1e-10,.00262409+1e-10,.0078125-1e-10,.0078125+1e-10,.18,1,10,100})
  {Assert.AreEqual(x,PhotoColorMathV1.DecodeDi(PhotoColorMathV1.EncodeDi(x)),3e-8);Assert.AreEqual(x,PhotoColorMathV1.DecodeAcesCct(PhotoColorMathV1.EncodeAcesCct(x)),1e-10);}
  Assert.IsTrue(PhotoColorMathV1.EncodeDi(-.1)<0);Assert.AreEqual(.5080784215,PhotoColorMathV1.EncodePqNits(100),1e-9);
  foreach(var x in new[]{0d,.1,100,1000,10000})Assert.AreEqual(x,PhotoColorMathV1.DecodePqNits(PhotoColorMathV1.EncodePqNits(x)),1e-7);
  Assert.Throws<ArgumentOutOfRangeException>(()=>PhotoColorMathV1.EncodePqNits(-1));Assert.Throws<ArgumentOutOfRangeException>(()=>PhotoColorMathV1.EncodeDi(double.NaN));
 }
 [TestMethod]public void PrimariesAndTransferAreSeparateAndCdlModesVersioned()
 {
  new PhotoColorDomainV1(PhotoPrimariesV1.AcesAp1D60,PhotoTransferV1.AcesCct).Validate();Assert.Throws<InvalidDataException>(()=>new PhotoColorDomainV1(PhotoPrimariesV1.SrgbBt709D65,PhotoTransferV1.AcesCct).Validate());
  Assert.AreNotEqual(PhotoColorMathV1.EncodeSrgb(.18),PhotoColorMathV1.Encode709(.18));var input=new ColorVector3(-.2,.18,1.5);var identity=new ColorVector3(1,1,1);var zero=new ColorVector3(0,0,0);
  Assert.AreEqual(input,PhotoColorMathV1.Cdl(input,identity,zero,identity,1,CdlClampV1.NoClampNonNegativePower));var clamped=PhotoColorMathV1.Cdl(input,identity,zero,identity,1,CdlClampV1.ClampSopAndOutput);Assert.AreEqual(0,clamped.X);Assert.AreEqual(1,clamped.Z);
 }
}
