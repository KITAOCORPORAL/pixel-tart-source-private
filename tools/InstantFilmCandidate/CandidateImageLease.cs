using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

/// <summary>Explicit retained ownership. Dispose relinquishes the pixels and admission lease; no GC release claims.</summary>
public sealed class CandidateImageLease : IDisposable
{
    private HighBitDepthImageBuffer? image;
    private IDisposable? reservation;
    private readonly object gate=new();
    private int borrowers;
    private bool retired;
    public long Bytes { get; }
    private CandidateImageLease(HighBitDepthImageBuffer value,IDisposable lease,long bytes)
    { image=value;reservation=lease;Bytes=bytes; }
    internal static CandidateImageLease Transfer(HighBitDepthImageBuffer value,IDisposable lease)
        =>new(value,lease,checked((long)value.Rgb32.Length*sizeof(float)));
    /// <summary>Caller transfers exclusive ownership; it must not retain a mutable alias.</summary>
    public static CandidateImageLease Adopt(HighBitDepthImageBuffer value)
        =>new(value,StudioGlobalResources.ReserveWork(checked((long)value.Rgb32.Length*sizeof(float))),checked((long)value.Rgb32.Length*sizeof(float)));
    public int Width {get{lock(gate){ThrowDisposed();return image!.Width;}}}
    public int Height {get{lock(gate){ThrowDisposed();return image!.Height;}}}
    public BorrowedImage Borrow()
    {
        lock(gate){ThrowDisposed();borrowers++;return new(this,image!);}
    }
    private void ThrowDisposed(){if(retired||image is null)throw new ObjectDisposedException(nameof(CandidateImageLease));}
    public sealed class BorrowedImage : IDisposable
    {
        private CandidateImageLease? owner;
        private HighBitDepthImageBuffer? value;
        internal BorrowedImage(CandidateImageLease owner,HighBitDepthImageBuffer value){this.owner=owner;this.value=value;}
        public HighBitDepthImageBuffer Image=>value??throw new ObjectDisposedException(nameof(BorrowedImage));
        public void Dispose(){var o=Interlocked.Exchange(ref owner,null);value=null;if(o is not null)o.ReturnBorrow();}
    }
    private void ReturnBorrow()
    {
        lock(gate){borrowers--;if(retired&&borrowers==0)Release();}
    }
    private void Release(){image=null;reservation?.Dispose();reservation=null;}
    public void Dispose(){lock(gate){if(retired)return;retired=true;if(borrowers==0)Release();}}
}

public sealed class CandidateImageCache : IDisposable
{
    private CandidateImageLease? current;
    public void Replace(CandidateImageLease next){ArgumentNullException.ThrowIfNull(next);var previous=Interlocked.Exchange(ref current,next);if(!ReferenceEquals(previous,next))previous?.Dispose();}
    public CandidateImageLease.BorrowedImage Borrow()=>Volatile.Read(ref current)?.Borrow()??throw new InvalidOperationException("Empty candidate cache");
    public void Dispose()=>Interlocked.Exchange(ref current,null)?.Dispose();
}
