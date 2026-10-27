using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53CmsClosureCoreTests
{
 [TestMethod]public void NewBlendsFiniteBoundedAndOpacityZeroPreservesHdrLegacyModes()
 {
  using var source=new HighBitDepthImageBuffer(4,1,new float[]{-.5f,1.3f,.2f,.3f,.5f,.7f,0,0,0,1,1,1});using var effect=new HighBitDepthImageBuffer(4,1,new float[]{1.5f,-.2f,.8f,.7f,.2f,.1f,.3f,.6f,.9f,.8f,.6f,.4f});
  foreach(var mode in new[]{StudioLayerBlend.Overlay,StudioLayerBlend.SoftLight,StudioLayerBlend.Color,StudioLayerBlend.Luminosity})
  {var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"admitted"){CompositeVersion=2,Blend=mode,Opacity=0};using(var identity=StudioLayerComposite.Apply(source,effect,node))CollectionAssert.AreEqual(source.Rgb32.ToArray(),identity.Rgb32.ToArray());using var result=StudioLayerComposite.Apply(source,effect,node with{Opacity=.5,LocalMask=new([new([new(.5,.5)],.8,.8,.7,1)])});Assert.IsTrue(result.Rgb32.ToArray().All(float.IsFinite));var mix=PhotographicBlendV2.Blend(new(-.5,1.3,.2),new(1.5,-.2,.8),mode);Assert.IsTrue(new[]{mix.X,mix.Y,mix.Z}.All(x=>x>=-1e-12&&x<=1+1e-12));}
  foreach(var mode in new[]{StudioLayerBlend.Normal,StudioLayerBlend.Multiply,StudioLayerBlend.Screen}){var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"legacy"){CompositeVersion=1,Blend=mode,Opacity=.3};using var actual=StudioLayerComposite.Apply(source,effect,node);for(int i=0;i<source.Rgb32.Length;i++){float a=source.Rgb32.Span[i],b=effect.Rgb32.Span[i];double mixed=mode switch{StudioLayerBlend.Multiply=>a*b,StudioLayerBlend.Screen=>1-(1-a)*(1-b),_=>b};Assert.AreEqual((float)(a+(mixed-a)*.3),actual.Rgb32.Span[i],0);}}
 }
 [TestMethod]public void IndependentAlphaOrientationKeepsStraightValuesWithoutBackgroundFlatten()
 {
  using var cms=new LcmsRgbEngine();using var document=new DocumentRenderResultV1(new(2,1,new float[]{.8f,.1f,.2f,.2f,.7f,.3f},orientation:6),cms.StandardProfile("sRGB"),Guid.NewGuid(),"EXIF6",new float[]{.25f,0});using var upright=document.NormalizeOrientation();Assert.AreEqual(1,upright.Pixels.Width);Assert.AreEqual(2,upright.Pixels.Height);CollectionAssert.AreEqual(document.Pixels.Rgb32.ToArray(),upright.Pixels.Rgb32.ToArray());CollectionAssert.AreEqual(new float[]{.25f,0},upright.Alpha.ToArray());Assert.Throws<NotSupportedException>(()=>upright.RequireOpaque());
 }
}
