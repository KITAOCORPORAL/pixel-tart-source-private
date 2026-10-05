using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Services;

public enum StudioExportFormat { Source, Jpeg, Png, Tiff }

/// <summary>Quick export keeps supported container types. The shared loader normalizes source ICC and EXIF orientation once.</summary>
public static class StudioQuickExport
{
    public static byte[] SrgbProfileBytes()
    {
        using var stream=new ColorContext(PixelFormats.Bgra32).OpenProfileStream();using var memory=new MemoryStream();stream.CopyTo(memory);return memory.ToArray();
    }
    public static string Extension(string source) => Path.GetExtension(source).ToLowerInvariant() switch { ".png"=>".png", ".tif" or ".tiff"=>".tif", _=>".jpg" };
    public static string Extension(string source, StudioExportFormat format) => format switch
    {
        StudioExportFormat.Jpeg => ".jpg", StudioExportFormat.Png => ".png", StudioExportFormat.Tiff => ".tif",
        _ => RAWSelectionAssistant.Core.Services.Projects.RawMatchTiff16ProductPipeline.IsRaw(source) ? ".tif" : Extension(source)
    };
    public static string FormatDescription(string source, StudioExportFormat format)
    {
        var target=Extension(source,format);
        return target switch { ".jpg"=>"JPEG 8 位 · 白底合成透明像素", ".png"=>"PNG 8/16 位 · 保留透明度", _=>"TIFF 16 位 · 保留透明度" };
    }
    public static BitmapSource ApplyOrientation(BitmapSource image,int orientation)
    {
        if(orientation is <2 or >8)return image;
        var matrix=orientation switch
        {
            2=>new Matrix(-1,0,0,1,0,0),3=>new Matrix(-1,0,0,-1,0,0),4=>new Matrix(1,0,0,-1,0,0),
            5=>new Matrix(0,1,1,0,0,0),6=>new Matrix(0,1,-1,0,0,0),7=>new Matrix(0,-1,-1,0,0,0),_=>new Matrix(0,-1,1,0,0,0)
        };
        var transformed=new TransformedBitmap(image,new MatrixTransform(matrix));transformed.Freeze();return transformed;
    }
    public static BitmapSource Load(string path)
    {
        using var file=File.OpenRead(path);
        var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        var orientation=ReadOrientation(frame);
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
        // Normalize the raster, not only its dimensions. Detachment below deliberately
        // discards source metadata, so orientation must be applied before that boundary.
        result=ApplyOrientation(result,orientation);
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
    private static int ReadOrientation(BitmapFrame frame)
    {
        if(frame.Metadata is not BitmapMetadata metadata)return 1;
        foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})
        {
            try
            {
                var value=metadata.GetQuery(query);
                if(value is ushort shortValue && shortValue is >=1 and <=8)return shortValue;
                if(value is uint longValue && longValue is >=1 and <=8)return (int)longValue;
            }
            catch(Exception error) when(error is NotSupportedException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                // A codec can reject queries belonging to another container. Missing
                // or malformed orientation has the same identity meaning as no EXIF.
            }
        }
        return 1;
    }
    public static void Encode(BitmapSource image,string sourcePath,string destination,CancellationToken token=default)
        => Encode(image,sourcePath,destination,StudioExportFormat.Source,token);
    public static void Encode(BitmapSource image,string sourcePath,string destination,StudioExportFormat format,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();var extension=Extension(sourcePath,format);
        BitmapEncoder encoder=extension switch { ".png"=>new PngBitmapEncoder(), ".tif"=>new TiffBitmapEncoder {Compression=TiffCompressOption.Zip}, _=>new JpegBitmapEncoder {QualityLevel=95} };
        // JPEG cannot represent alpha. Composite over an explicit white background, not hidden RGB.
        if(extension==".jpg") image=FlattenWhite(image,token);
        else image=new FormatConvertedBitmap(image,extension==".tif"||image.Format.BitsPerPixel>32?PixelFormats.Rgba64:PixelFormats.Bgra32,null,0);
        BitmapMetadata? metadata=null;
        if(extension is ".jpg" or ".tif")
        {
            metadata=new BitmapMetadata(extension==".jpg"?"jpg":"tiff");
            metadata.SetQuery(extension==".jpg"?"/app1/ifd/{ushort=274}":"/ifd/{ushort=274}",(ushort)1);
        }
        encoder.Frames.Add(BitmapFrame.Create(image,null,metadata,new ReadOnlyCollection<ColorContext>([new ColorContext(PixelFormats.Bgra32)])));
        using var file=File.Create(destination);encoder.Save(file);token.ThrowIfCancellationRequested();
    }
    private static BitmapSource FlattenWhite(BitmapSource image,CancellationToken token)
    {
        var input=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var bgra=new byte[input.PixelWidth*input.PixelHeight*4];input.CopyPixels(bgra,input.PixelWidth*4,0);var rgb=new byte[input.PixelWidth*input.PixelHeight*3];
        for(var p=0;p<rgb.Length/3;p++){if((p&4095)==0)token.ThrowIfCancellationRequested();var alpha=bgra[p*4+3];for(var c=0;c<3;c++)rgb[p*3+c]=(byte)((bgra[p*4+c]*alpha+255*(255-alpha)+127)/255);}
        var result=BitmapSource.Create(input.PixelWidth,input.PixelHeight,input.DpiX,input.DpiY,PixelFormats.Bgr24,null,rgb,input.PixelWidth*3);result.Freeze();return result;
    }
}
