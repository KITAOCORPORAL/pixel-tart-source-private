using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53LocalCompositeTests
{
    [TestMethod]public void BlendOpacityMaskBoundariesAndLegacyIdentity()
    {
        using var input=new HighBitDepthImageBuffer(2,1,new float[]{.2f,.4f,.8f,.5f,.1f,.3f});using var effect=new HighBitDepthImageBuffer(2,1,new float[]{.6f,.2f,.1f,.2f,.9f,.6f});var n=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"layer"){CompositeVersion=1,Opacity=.25};
        foreach(var mode in Enum.GetValues<StudioLayerBlend>()){var actual=StudioLayerComposite.Apply(input,effect,n with{Blend=mode});for(int i=0;i<6;i++){var a=input.Rgb32.Span[i];var b=effect.Rgb32.Span[i];var expected=mode switch{StudioLayerBlend.Multiply=>a*b,StudioLayerBlend.Screen=>1-(1-a)*(1-b),_=>b};Assert.AreEqual(a+(expected-a)*.25,actual.Rgb32.Span[i],1e-7);}}
        CollectionAssert.AreEqual(input.Rgb32.ToArray(),StudioLayerComposite.Apply(input,effect,n with{Opacity=0}).Rgb32.ToArray());CollectionAssert.AreEqual(input.Rgb32.ToArray(),StudioLayerComposite.Apply(input,effect,n with{LocalMask=new StudioLocalMask([])}).Rgb32.ToArray());Assert.AreSame(effect,StudioLayerComposite.Apply(input,effect,n with{Opacity=1}));
    }
    [TestMethod]public void BrushEraseFeatherDensityInverseAndFrozenVariantIsolation()
    {
        var points=new List<StudioMaskPoint>{new(.5,.5)};var stroke=new StudioMaskStroke(points,.25,.5,.5,.8);var mask=new StudioLocalMask([stroke]);Assert.AreEqual(.4,mask.Weight(.5,.5,1),1e-8);Assert.IsTrue(mask.Weight(.7,.5,1)<.4);Assert.AreEqual(0,mask.Weight(.1,.1,1));var erase=mask with{Strokes=[stroke,stroke with{Erase=true,Flow=1}]};Assert.AreEqual(0,erase.Weight(.5,.5,1));Assert.AreEqual(.6,(mask with{Inverted=true}).Weight(.5,.5,1),1e-8);
        var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"A"){CompositeVersion=1,LocalMask=mask}]);var frozen=stack.DeepClone();points[0]=new(.1,.1);Assert.AreEqual(.5,frozen.Nodes[0].LocalMask!.Strokes[0].Points[0].X);Assert.AreNotSame(stack.Nodes[0].LocalMask,frozen.Nodes[0].LocalMask);var json=JsonSerializer.Serialize(frozen);Assert.AreEqual(json,JsonSerializer.Serialize(JsonSerializer.Deserialize<ColorAdjustmentStack>(json)!.Normalize()));
    }
    [TestMethod]public void MaskViewportCropCoordinatesAndZoomPanAreSourceNormalized()
    {
        var p=StudioMaskCoordinates.FromViewport(250,150,50,50,400,200);Assert.AreEqual(.5,p!.X,1e-9);Assert.AreEqual(.5,p.Y,1e-9);var zoomed=StudioMaskCoordinates.FromViewport(450,250,50,50,800,400);Assert.AreEqual(p,zoomed);
        var layout=InstantPaperLayout.Create(400,200,new InstantFilmDefinition(true){PaperGeometry=new(WindowAspect:1,CropX:1,Side:0,Top:0,Bottom:0),PaperFrame=false});var crop=StudioMaskCoordinates.FromViewport(100,100,0,0,200,200,layout);Assert.AreEqual(.75,crop!.X,1e-6);Assert.AreEqual(.5,crop.Y,1e-6);Assert.IsNull(StudioMaskCoordinates.FromViewport(-1,0,0,0,200,200));
    }
    [TestMethod]public void EmptyMaskHiddenNodeVersionGuardsAndInvalidCoordinatesAreExplicit()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>StudioMaskCoordinates.FromViewport(1,1,0,0,10,10,zoom:0));
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"v1"){CompositeVersion=1,LocalMask=new([])};
        Assert.ThrowsExactly<InvalidDataException>(()=>(node with{CompositeVersion=12}).Normalize());
        Assert.ThrowsExactly<InvalidDataException>(()=>(node with{CompositeVersion=0}).Normalize());
        var frozen=new ColorAdjustmentStack([node with{Enabled=false}]).DeepClone();Assert.IsNotNull(frozen.Nodes[0].LocalMask);Assert.IsFalse(frozen.Nodes[0].Enabled);
        Assert.ThrowsExactly<InvalidDataException>(()=>new StudioMaskStroke([new(double.NaN,.5)],.1,.5,.5,1).Freeze());
        var crop=InstantPaperLayout.Create(400,200,new InstantFilmDefinition(true){PaperGeometry=new(WindowAspect:1,CropX:1,Side:0,Top:0,Bottom:0),PaperFrame=false});
        var p=StudioMaskCoordinates.FromViewport(100,100,0,0,200,200,crop,zoom:2,centerX:.5,centerY:.5);Assert.AreEqual(.625,p!.X,1e-6);
    }
    [TestMethod]public void PointwiseFullFrameAndRowBandsUseIdenticalCompositeCoordinates()
    {
        var rgb=Enumerable.Range(0,48*32*3).Select(i=>(float)((i%37)/50d)).ToArray();using var source=new HighBitDepthImageBuffer(48,32,rgb,"16");var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",.3}}){CompositeVersion=1,Opacity=.7,Blend=StudioLayerBlend.Screen,LocalMask=new([new([new(.2,.2),new(.8,.7)],.2,.5,.7,1)])};var stack=new ColorAdjustmentStack([node],ProcessingVersion:2);var analysis=ColorStudioProcessingAnalysis.Create(new VisualPixelBuffer(1,1,new byte[3]),stack,null);var full=new ColorStudioRenderPipeline().Render(source,analysis,null,stack,captureNodeDiagnostics:false).ProcessingPixels!;var bands=BoundedLegacyRawRenderer.Render(source,null,stack,null);for(int i=0;i<rgb.Length;i++)Assert.AreEqual(full.Rgb32.Span[i],bands.Rgb32.Span[i],1e-6);var spatial=SpatialLegacyRawRenderer.RenderNode(source,node,tileWidth:13,tileHeight:9);for(int i=0;i<rgb.Length;i++)Assert.AreEqual(full.Rgb32.Span[i],spatial.Rgb32.Span[i],1e-6);
    }
}
