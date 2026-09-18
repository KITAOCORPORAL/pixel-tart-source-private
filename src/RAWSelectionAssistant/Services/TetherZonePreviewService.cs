using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Services;

public sealed class TetherZonePreviewService
{
    public Task<IReadOnlyList<BitmapSource>> BuildAsync(BitmapSource source, CancellationToken token = default) => Task.Run<IReadOnlyList<BitmapSource>>(() =>
    {
        var input=HistogramService.EnsureBgra32(source);var stride=input.PixelWidth*4;var bgra=new byte[stride*input.PixelHeight];input.CopyPixels(bgra,stride,0);
        var rgb=new byte[input.PixelWidth*input.PixelHeight*3];for(var pixel=0;pixel<input.PixelWidth*input.PixelHeight;pixel++){rgb[pixel*3]=bgra[pixel*4+2];rgb[pixel*3+1]=bgra[pixel*4+1];rgb[pixel*3+2]=bgra[pixel*4];}
        var buffer=new VisualPixelBuffer(input.PixelWidth,input.PixelHeight,rgb);var map=VisualAnalysisEngine.CreateZoneMap(buffer,null,token);var hover=new VisualZoneHoverPreview(buffer,map,token);
        return Enumerable.Range(0,11).Select(zone=>ToBitmap(hover.Select(zone),input.DpiX,input.DpiY)).ToArray();
    },token);
    private static BitmapSource ToBitmap(VisualPixelBuffer value,double dpiX,double dpiY){var stride=value.Width*4;var bgra=new byte[stride*value.Height];for(var pixel=0;pixel<value.PixelCount;pixel++){bgra[pixel*4]=value.Rgb24.Span[pixel*3+2];bgra[pixel*4+1]=value.Rgb24.Span[pixel*3+1];bgra[pixel*4+2]=value.Rgb24.Span[pixel*3];bgra[pixel*4+3]=255;}var image=BitmapSource.Create(value.Width,value.Height,dpiX,dpiY,PixelFormats.Bgra32,null,bgra,stride);image.Freeze();return image;}
}
