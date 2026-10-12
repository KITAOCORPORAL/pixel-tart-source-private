using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Services.Publishing;

namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchEightPublishingOracleTests
{
    [TestMethod]
    public async Task Tiff16ResizeMatchesIndependentBilinearOracleWithoutEightBitQuantization()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-resize16-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            const int width=40,height=24;var samples=Enumerable.Range(0,width*height*3).Select(i=>(ushort)((i*73)%65536)).ToArray();var source=Path.Combine(root,"source.tif");
            using(var file=File.Create(source))TiffExport.WriteRgb48(file,new HighBitDepthImageBuffer(width,height,samples),new(IccProfile:StudioQuickExport.SrgbProfileBytes()));
            var output=Path.Combine(root,"output.tif");await new WpfPublishingRenderer().RenderAsync(source,output,new(new(true,PublishingSizeMode.Exact,Width:20,Height:12,Dpi:300),false,[],PublishingOutputFormat.Tiff,OutputBitDepth:PublishingOutputBitDepth.Sixteen));
            using var input=File.OpenRead(output);var result=TiffReadBack.Read(input);Assert.AreEqual(20,result.Width);Assert.AreEqual(12,result.Height);Assert.AreEqual(300,result.Dpi);
            for(var y=0;y<12;y++)for(var x=0;x<20;x++)for(var c=0;c<3;c++)
            {
                var expected=(samples[((2*y)*width+2*x)*3+c]+samples[((2*y)*width+2*x+1)*3+c]+samples[((2*y+1)*width+2*x)*3+c]+samples[((2*y+1)*width+2*x+1)*3+c])/4d;
                Assert.AreEqual(expected,result.Rgb48Samples.Span[(y*20+x)*3+c],1);
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task ResizeOrientationDpiAndImageAlphaHaveIndependentPixelOracleAtNinePositions()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-publishing-oracle-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source.tif");var samples=new ushort[80*48*3];
            for(var i=0;i<samples.Length;i+=3){samples[i]=80*257;samples[i+1]=120*257;samples[i+2]=160*257;}
            using(var file=File.Create(source))TiffExport.WriteRgb48(file,new HighBitDepthImageBuffer(80,48,samples),new(Orientation:6,IccProfile:StudioQuickExport.SrgbProfileBytes()));
            var logo=Path.Combine(root,"logo.png");var logoPixels=new byte[8*8*4];for(var i=0;i<logoPixels.Length;i+=4){logoPixels[i]=40;logoPixels[i+1]=200;logoPixels[i+2]=240;logoPixels[i+3]=128;}
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,logoPixels,32)));using(var file=File.Create(logo))encoder.Save(file);
            foreach(var dpi in new[]{96,144,240})foreach(var opacity in new[]{0d,.5,1})foreach(var position in Enum.GetValues<WatermarkPosition>())
            {
                // EXIF6: 48x80, exact-fit 24x40. Watermark is 6x6, margin=0; interior pixels avoid raster edge coverage.
                var output=Path.Combine(root,$"{dpi}-{opacity}-{position}.png");
                var layer=new WatermarkLayer(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:logo,Opacity:opacity,WidthPercent:25,Position:position,MarginPercent:0);
                var options=new PublishingOptions(new(true,PublishingSizeMode.Exact,Width:24,Height:40,Dpi:dpi),true,[layer],PublishingOutputFormat.Png);
                await new WpfPublishingRenderer().RenderAsync(source,output,options);
                using var file=File.OpenRead(output);var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];Assert.AreEqual(24,frame.PixelWidth);Assert.AreEqual(40,frame.PixelHeight);Assert.AreEqual(dpi,Math.Round(frame.DpiX));
                var rgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb24,null,0);var pixels=new byte[24*40*3];rgb.CopyPixels(pixels,72,0);
                var col=(int)position%3;var row=(int)position/3;var left=col==0?0:col==1?9:18;var top=row==0?0:row==1?17:34;
                var alpha=128/255d*opacity;var expected=new[]{80*(1-alpha)+240*alpha,120*(1-alpha)+200*alpha,160*(1-alpha)+40*alpha};
                for(var y=top+1;y<top+5;y++)for(var x=left+1;x<left+5;x++)for(var c=0;c<3;c++)Assert.AreEqual(expected[c],pixels[(y*24+x)*3+c],2,$"{dpi}/{opacity}/{position}/{c}");
                var checkY=top<20?38:1;for(var c=0;c<3;c++)Assert.AreEqual(new[]{80,120,160}[c],pixels[(checkY*24+12)*3+c],2);
            }
            var bad=Path.Combine(root,"bad.png");await Assert.ThrowsAsync<Exception>(()=>new WpfPublishingRenderer().RenderAsync(source,bad,new(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:Path.Combine(root,"missing.png"))],PublishingOutputFormat.Png)));Assert.IsFalse(File.Exists(bad));
        }
        finally{Directory.Delete(root,true);}
    }
}
