using System.Buffers.Binary;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Read-only full input plus one owned 16-row result. Disposing never releases the shared input.</summary>
public sealed class HighPrecisionExportRows : IDisposable
{
    private HighBitDepthImageBuffer? source, strip;
    private readonly ColorAdjustmentStackNode[] nodes;
    private readonly CancellationToken token;
    private int stripStart = -1;
    internal HighPrecisionExportRows(HighBitDepthImageBuffer source,ColorAdjustmentStackNode[] nodes,CancellationToken token)
    { this.source=source;this.nodes=nodes;this.token=token; }
    public bool BorrowsSource => true;
    public string Path => nodes.Length==0 ? "BorrowedIdentityRows" : "OwnedPointStripRows";
    private ReadOnlySpan<float> ReadRow(int y)
    {
        var input=source ?? throw new ObjectDisposedException(nameof(HighPrecisionExportRows));
        token.ThrowIfCancellationRequested();
        if((uint)y>=(uint)input.Height)throw new ArgumentOutOfRangeException(nameof(y));
        if(nodes.Length==0)return input.Rgb32.Span.Slice(y*input.Width*3,input.Width*3);
        if(strip is null||y<stripStart||y>=stripStart+strip.Height)
        {
            strip?.Dispose();strip=null;stripStart=y;
            strip=RenderStrip(input,y,Math.Min(16,input.Height-y));
        }
        return strip.Rgb32.Span.Slice((y-stripStart)*input.Width*3,input.Width*3);
    }
    public void FillRow(int y,Memory<byte> destination)
    {
        var row=ReadRow(y);
        if(destination.Length!=row.Length*2)throw new ArgumentOutOfRangeException(nameof(destination));
        // Identical rounding and byte order to TiffExport.WriteRgb48.
        for(int i=0;i<row.Length;i++)BinaryPrimitives.WriteUInt16LittleEndian(destination.Span.Slice(i*2,2),
            (ushort)Math.Clamp(Math.Round(row[i]*65535),0,65535));
    }
    public void FillDisplayRow(int y,Memory<byte> destination)
    {
        var row=ReadRow(y);
        if(destination.Length!=row.Length)throw new ArgumentOutOfRangeException(nameof(destination));
        // Direct float-to-display rounding, exactly HighBitDepthImageBuffer.ToVisualRgb24;
        // never derive display samples from quantized RGB48 export rows.
        for(int i=0;i<row.Length;i++)destination.Span[i]=(byte)Math.Clamp(Math.Round(row[i]*255),0,255);
    }
    private HighBitDepthImageBuffer RenderStrip(HighBitDepthImageBuffer input,int y,int rows)
    {
        using var buffers=GraphPixelAllocation.EnterIfNeeded();
        var copied=GraphPixelAllocation.Copy(input.Rgb32.Slice(y*input.Width*3,rows*input.Width*3));
        HighBitDepthImageBuffer? current=new(input.Width,rows,copied,input.SourceBitDepth,input.WorkingColorSpace,input.Orientation,input.Metadata);
        try
        {
            foreach(var node in nodes)
            {
                token.ThrowIfCancellationRequested();
                var next=ColorStudioRenderPipeline.ApplyPointEffect(current,node,token);
                current.Dispose();current=next;
            }
            var result=current;current=null;return result;
        }
        finally { current?.Dispose(); }
    }
    public void Dispose(){strip?.Dispose();strip=null;source=null;}
}
