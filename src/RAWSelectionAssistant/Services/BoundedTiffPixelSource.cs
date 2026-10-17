using System.Buffers.Binary;
using System.IO;
using System.IO.MemoryMappedFiles;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;

/// <summary>Read-only original TIFF3 codes, not a resampled proxy. The session owns the
/// file lock and mapped pages. Same ushort/65535f arithmetic as BoundedSrgbTiffInput.
/// Product single-strip uncompressed RGB16 + exact sRGB only; NOT general TIFF.</summary>
public sealed unsafe class BoundedTiffPixelSource : ISecondExposureSource
{
    private FileStream? stream;
    private MemoryMappedFile? mapping;
    private MemoryMappedViewAccessor? view;
    private IDisposable? reservation;
    private byte* pointer;
    private bool acquired;
    private long offset;
    private int rawWidth,rawHeight,orientation;
    public int Width {get;private set;}
    public int Height {get;private set;}
    public BoundedTiffPixelSource(CandidateMultipleInput input,CancellationToken token)
    {
        using var measurement=StudioPreviewDiagnostics.Measure("SecondMappedOpen");
        try
        {
            input.Validate();if(input.Version!=3)throw new NotSupportedException("Mapped input is TIFF3 only.");
            CandidateProcessingEvidence.Enter("SecondHashBefore",token);
            stream=new(input.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(CandidateMultipleInput.HashSource(stream,token)!=input.Sha256)throw new InvalidDataException("SECOND_SOURCE_CHANGED");
            CandidateProcessingEvidence.Enter("SecondDecode",token);
            var meta=TiffStreamValidator.Validate(stream,token);
            if(meta.BitsPerSample!=16||!meta.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Bounded TIFF3 profile changed");
            rawWidth=meta.Width;rawHeight=meta.Height;orientation=meta.Orientation;
            Width=orientation>=5?rawHeight:rawWidth;Height=orientation>=5?rawWidth:rawHeight;
            if(Width!=input.Width||Height!=input.Height||checked((long)Width*Height*12)>512L*1024*1024)throw new InvalidDataException("SECOND_SOURCE_DIMENSIONS_CHANGED");
            offset=stream.Length-meta.PixelBytes;
            reservation=StudioGlobalResources.ReserveWork(rawWidth*6L+1024*1024);
            mapping=MemoryMappedFile.CreateFromFile(stream,null,0,MemoryMappedFileAccess.Read,HandleInheritability.None,true);
            view=mapping.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);acquired=true;pointer+=view.PointerOffset;
            CandidateProcessingEvidence.Enter("SecondHashAfter",token);stream.Position=0;
            if(CandidateMultipleInput.HashSource(stream,token)!=input.Sha256)throw new InvalidDataException("SECOND_SOURCE_CHANGED");
        }
        catch{Dispose();throw;}
    }
    public ColorVector3 Read(int x,int y)
    {
        if(view is null)throw new ObjectDisposedException(nameof(BoundedTiffPixelSource));
        if((uint)x>=(uint)Width||(uint)y>=(uint)Height)throw new ArgumentOutOfRangeException(nameof(x));
        var(px,py)=orientation switch{2=>(rawWidth-1-x,y),3=>(rawWidth-1-x,rawHeight-1-y),4=>(x,rawHeight-1-y),5=>(y,x),6=>(y,rawHeight-1-x),7=>(rawWidth-1-y,rawHeight-1-x),8=>(rawWidth-1-y,x),_=>(x,y)};
        var bytes=new ReadOnlySpan<byte>(pointer+offset+((long)py*rawWidth+px)*6,6);
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes)/65535f,BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..])/65535f,BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..])/65535f);
    }
    public void Dispose()
    {
        if(acquired){view!.SafeMemoryMappedViewHandle.ReleasePointer();acquired=false;pointer=null;}
        view?.Dispose();view=null;mapping?.Dispose();mapping=null;stream?.Dispose();stream=null;reservation?.Dispose();reservation=null;
    }
}
