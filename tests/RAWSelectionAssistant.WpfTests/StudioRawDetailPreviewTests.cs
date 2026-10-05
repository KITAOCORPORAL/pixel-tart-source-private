using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class StudioRawDetailPreviewTests
{
    [TestMethod]
    public Task RawActualSizeUsesFullPixelGeometryAndRenderedSamplesWithReusableCache() => RunSta(async()=>
    {
        const int width=2400,height=1200;var values=new float[width*height*3];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++){var p=(y*width+x)*3;values[p]=(x%7)/8f;values[p+1]=(y%7)/8f;values[p+2]=.4f;}
        var full=new HighBitDepthImageBuffer(width,height,values,"16");
        var master=new FrozenRawMaster(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"synthetic.raw"),"fixture",Guid.NewGuid(),full);
        var pipeline=new RawMatchTiff16ProductPipeline(new RAWSelectionAssistant.Core.Services.RawToJpeg.LibRawDecoder());var proxy=pipeline.PreviewMaster(full,1600);
        using var editor=new TetherReferenceModeViewModel(allowReferenceManagement:true);editor.Enabled=true;
        var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"WB",NumericParameters:new Dictionary<string,double>{{"temperature",10}})],ProcessingVersion:2);
        editor.ApplyTargetSnapshot(null,null,stack,false);await editor.SetSourceAsync(Guid.NewGuid(),RawDisplayBitmapAdapter.ToBitmap(proxy.ToVisualRgb24()),rawPreviewMaster:proxy,frozenRawMaster:master);
        Assert.AreEqual(1600,editor.MatchedImage!.PixelWidth);Assert.AreEqual(new Size(width,height),editor.SourcePixelSize);
        var viewport=new ColorStudioImageViewport { Original=editor.SourceImage,Matched=editor.MatchedImage,SourcePixelSize=editor.SourcePixelSize };
        viewport.Measure(new Size(800,600));viewport.Arrange(new Rect(0,0,800,600));viewport.State.SetZoom(1);
        Assert.AreEqual(width,viewport.State.ImageRect(new Rect(0,0,800,600)).Width,"100% geometry must represent original pixels, never proxy dimensions.");
        await editor.SetPreviewZoomAsync(1);
        Assert.AreEqual(width,editor.SourceImage!.PixelWidth);Assert.AreEqual(width,editor.MatchedImage!.PixelWidth);
        var detail=editor.MatchedImage;var expected=pipeline.Render(full,null,stack).Pixels;
        var actual=new FormatConvertedBitmap(detail,System.Windows.Media.PixelFormats.Rgb24,null,0);var bytes=new byte[width*height*3];actual.CopyPixels(bytes,width*3,0);
        CollectionAssert.AreEqual(expected.Rgb24.ToArray(),bytes,"100% must actually render full samples.");
        await editor.SetPreviewZoomAsync(1);Assert.AreSame(detail,editor.MatchedImage);
        await editor.SetPreviewZoomAsync(.2);Assert.AreEqual(1600,editor.MatchedImage!.PixelWidth);
        await editor.SetPreviewZoomAsync(1);Assert.AreSame(detail,editor.MatchedImage,"Returning to 100% reuses the complete-state cached full frame.");
        var pending=editor.SetPreviewZoomAsync(.2);await editor.SetSourceAsync(null,null);await pending;
        Assert.IsNull(editor.SourceImage);Assert.IsNull(editor.MatchedImage);
    });
    [TestMethod]
    public Task StopCancelsPendingActualSizeBeforeItCanStartAnotherRender() => RunSta(async()=>
    {
        var full=new HighBitDepthImageBuffer(1800,2,Enumerable.Repeat(.4f,1800*2*3).ToArray(),"16");
        var master=new FrozenRawMaster(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"synthetic.raw"),"fixture",Guid.NewGuid(),full);
        var pipeline=new RawMatchTiff16ProductPipeline(new RAWSelectionAssistant.Core.Services.RawToJpeg.LibRawDecoder());var proxy=pipeline.PreviewMaster(full,1600);
        using var editor=new TetherReferenceModeViewModel(allowReferenceManagement:true);editor.Enabled=true;
        editor.ApplyTargetSnapshot(null,null,new([new(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"WB")],ProcessingVersion:2),false);
        await editor.SetSourceAsync(Guid.NewGuid(),RawDisplayBitmapAdapter.ToBitmap(proxy.ToVisualRgb24()),rawPreviewMaster:proxy,frozenRawMaster:master);
        var before=editor.MatchedImage;var pending=editor.SetPreviewZoomAsync(1);editor.StopProcessing();await pending;
        Assert.AreSame(before,editor.MatchedImage);Assert.AreEqual(1600,editor.SourceImage!.PixelWidth);Assert.IsFalse(editor.IsDetailPreviewRequested);
        await editor.SetPreviewZoomAsync(1);Assert.AreEqual(1800,editor.MatchedImage!.PixelWidth,"A later explicit 100% can retry.");
    });
}
