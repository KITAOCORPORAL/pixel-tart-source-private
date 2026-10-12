using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services.Publishing;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchNinePublishingTests
{
    [TestMethod]
    public async Task EncodedTextWatermarkMatchesIndependentFixedFontRasterAndRejectsTampering()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-batch09-text-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source.png");var output=Path.Combine(root,"output.png");
            const int w=320,h=160;var pixels=Enumerable.Repeat((byte)100,w*h*3).ToArray();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(w,h,96,96,PixelFormats.Rgb24,null,pixels,w*3)));using(var stream=File.Create(source))encoder.Save(stream);
            var layer=new WatermarkLayer(Guid.NewGuid(),WatermarkLayerType.Text,Text:"TEST",FontFamily:"Arial",FontWeight:"Normal",FontSize:144,Color:"#FFFFFF",Opacity:.5,Position:WatermarkPosition.TopLeft,MarginPercent:0);
            var options=new PublishingOptions(new(false,Dpi:96),true,[layer],PublishingOutputFormat.Png);
            var renderer=new WpfPublishingRenderer();await renderer.RenderAsync(source,output,options);
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(100,100,100)),null,new Rect(0,0,w,h));
                dc.PushOpacity(.5);dc.DrawText(new FormattedText("TEST",CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface("Arial"),24,Brushes.White,1),new Point(0,0));dc.Pop();
            }
            var expected=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);expected.Render(visual);
            using var encoded=File.OpenRead(output);var actual=BitmapDecoder.Create(encoded,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
            var a=Rgb(actual);var e=Rgb(expected);var changed=0;
            for(var i=0;i<a.Length;i++){Assert.AreEqual(e[i],a[i],2);if(a[i]>110)changed++;}
            Assert.IsGreaterThan(100,changed);
            var proof=await renderer.VerifyPixelsAsync(source,output,options,default);StringAssert.Contains(proof,"EncodedRecipePixelComparison");
            encoded.Dispose();var replacement=Path.Combine(root,"replacement.png");await renderer.RenderAsync(source,replacement,options with{WatermarksEnabled=false});File.Copy(replacement,output,true);
            await Assert.ThrowsAsync<InvalidDataException>(()=>renderer.VerifyPixelsAsync(source,output,options,default));
        }
        finally{Directory.Delete(root,true);}
    }
    private static byte[] Rgb(BitmapSource bitmap){var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb24,null,0);var bytes=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(bytes,rgb.PixelWidth*3,0);return bytes;}
}

