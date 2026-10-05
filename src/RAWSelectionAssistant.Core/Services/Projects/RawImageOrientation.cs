using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Color Studio owns upright pixels so viewport, sampling, analysis and export share one coordinate system.</summary>
public static class RawImageOrientation
{
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
