using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Services;

/// <summary>Quick export keeps supported container types. The shared loader normalizes the ICC source to sRGB once.</summary>
public static class StudioQuickExport
{
    public static string Extension(string source) => Path.GetExtension(source).ToLowerInvariant() switch { ".png"=>".png", ".tif" or ".tiff"=>".tif", _=>".jpg" };
    public static BitmapSource Load(string path)
    {
        using var file=File.OpenRead(path);
        var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        BitmapSource result=frame;
        if(frame.ColorContexts is {Count:>0})
        {
            var srgb = new ColorContext(PixelFormats.Bgra32);
            using var sourceProfile = frame.ColorContexts[0].OpenProfileStream();
            using var targetProfile = srgb.OpenProfileStream();
            using var sourceBytes = new MemoryStream(); using var targetBytes = new MemoryStream();
            sourceProfile.CopyTo(sourceBytes); targetProfile.CopyTo(targetBytes);
            if (!sourceBytes.ToArray().SequenceEqual(targetBytes.ToArray()))
            {
                // WIC color conversion does not accept RGBA64 on all codecs. Convert RGB48
                // and restore the independent alpha channel without reducing precision.
                if (frame.Format.BitsPerPixel > 32)
                {
                    var rgba = new FormatConvertedBitmap(frame, PixelFormats.Rgba64, null, 0);
                    var original = new ushort[frame.PixelWidth * frame.PixelHeight * 4]; rgba.CopyPixels(original, frame.PixelWidth*8, 0);
                    var rgbInput = new FormatConvertedBitmap(frame, PixelFormats.Rgb48, null, 0);
                    var converted = new ColorConvertedBitmap(rgbInput, frame.ColorContexts[0], srgb, PixelFormats.Rgb48);
                    var rgb = new ushort[frame.PixelWidth * frame.PixelHeight * 3]; converted.CopyPixels(rgb, frame.PixelWidth*6, 0);
                    for(var pixel=0;pixel<rgb.Length/3;pixel++) for(var c=0;c<3;c++) original[pixel*4+c]=rgb[pixel*3+c];
                    result=BitmapSource.Create(frame.PixelWidth,frame.PixelHeight,frame.DpiX,frame.DpiY,PixelFormats.Rgba64,null,original,frame.PixelWidth*8);
                }
                else result = new ColorConvertedBitmap(frame,frame.ColorContexts[0],srgb,PixelFormats.Bgra32);
            }
        }
        // A frozen BitmapFrame can still retain a decoder owned by the decoding
        // thread. Later FormatConvertedBitmap.Freeze walks that decoder and fails
        // on the preview worker. Publish detached pixels, including palette/alpha.
        var stride = checked((result.PixelWidth * result.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[checked(stride * result.PixelHeight)];
        result.CopyPixels(pixels, stride, 0);
        var detached = BitmapSource.Create(result.PixelWidth, result.PixelHeight,
            result.DpiX, result.DpiY, result.Format, result.Palette, pixels, stride);
        detached.Freeze(); return detached;
    }
    public static void Encode(BitmapSource image,string sourcePath,string destination,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();var extension=Extension(sourcePath);
        BitmapEncoder encoder=extension switch { ".png"=>new PngBitmapEncoder(), ".tif"=>new TiffBitmapEncoder {Compression=TiffCompressOption.Zip}, _=>new JpegBitmapEncoder {QualityLevel=95} };
        // JPEG has no alpha. PNG/TIFF retain the processed straight-alpha surface and its real bit depth.
        if(extension==".jpg") image=new FormatConvertedBitmap(image,PixelFormats.Bgr24,null,0);
        encoder.Frames.Add(BitmapFrame.Create(image,null,null,new ReadOnlyCollection<ColorContext>([new ColorContext(PixelFormats.Bgra32)])));
        using var file=File.Create(destination);encoder.Save(file);token.ThrowIfCancellationRequested();
    }
}
