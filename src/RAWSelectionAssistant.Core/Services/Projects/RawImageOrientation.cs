using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Color Studio owns upright pixels so viewport, sampling, analysis and export share one coordinate system.</summary>
public static class RawImageOrientation
{
    /// <summary>Same decoded RGB48 division and orientation permutation in one owned allocation.
    /// Reads the decoder result without changing it; no camera decoding or color transform here.</summary>
    public static HighBitDepthImageBuffer FromDecodedRgb48(RawDecodedImage image,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(image);token.ThrowIfCancellationRequested();
        if(image.Rgb48Pixels is not{} values || image.BitsPerChannel<16)
            throw new ArgumentException("An existing RGB48 decoder result is required.",nameof(image));
        if(values.Length!=checked(image.Width*image.Height*3))throw new ArgumentException("RGB48 dimensions mismatch.");
        var orientation=image.Metadata.Orientation;
        if(orientation is <2 or >8)return HighBitDepthImageBuffer.FromRaw(image);
        var width=orientation>=5?image.Height:image.Width;var height=orientation>=5?image.Width:image.Height;
        var pixels=new float[checked(width*height*3)];
        for(int y=0;y<image.Height;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=0;x<image.Width;x++)
            {
                var (dx,dy)=orientation switch
                {
                    2=>(image.Width-1-x,y),3=>(image.Width-1-x,image.Height-1-y),4=>(x,image.Height-1-y),
                    5=>(y,x),6=>(image.Height-1-y,x),7=>(image.Height-1-y,image.Width-1-x),
                    8=>(y,image.Width-1-x),_=>(x,y)
                };
                for(int c=0;c<3;c++)pixels[(dy*width+dx)*3+c]=values[(y*image.Width+x)*3+c]/65535f;
            }
        }
        return new(width,height,pixels,image.BitsPerChannel.ToString(),image.Metadata.ColorSpace,1,
            image.Metadata with{Orientation=1});
    }

    public static HighBitDepthImageBuffer NormalizePixels(HighBitDepthImageBuffer source, CancellationToken token = default)
    {
        var orientation = source.Orientation;
        if (orientation is < 2 or > 8) return source;
        var swap = orientation >= 5; var width = swap ? source.Height : source.Width; var height = swap ? source.Width : source.Height;
        var pixels = new float[checked(width * height * 3)];
        for (var y = 0; y < source.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < source.Width; x++)
            {
                var (dx, dy) = orientation switch
                {
                    2 => (source.Width-1-x,y), 3 => (source.Width-1-x,source.Height-1-y), 4 => (x,source.Height-1-y),
                    5 => (y,x), 6 => (source.Height-1-y,x), 7 => (source.Height-1-y,source.Width-1-x), 8 => (y,source.Width-1-x), _ => (x,y)
                };
                source.Rgb32.Span.Slice((y*source.Width+x)*3,3).CopyTo(pixels.AsSpan((dy*width+dx)*3,3));
            }
        }
        return new(width,height,pixels,source.SourceBitDepth,source.WorkingColorSpace,1,source.Metadata is null ? null : source.Metadata with {Orientation=1});
    }
}
