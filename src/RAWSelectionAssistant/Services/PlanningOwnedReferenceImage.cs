using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
/// <summary>PlanningOwnedReferenceV1: verified snapshot bytes, bounded PNG/JPEG only. Never resolves an external original.</summary>
public static class PlanningOwnedReferenceImage
{
    public const int MaximumSide=16384;public const long MaximumPixels=40_000_000;
    public static BitmapSource Read(PlanningProjectCopy copy,Guid project,Guid document,Guid reference)
    {
        if(copy.Kind!="reference"||copy.ProjectId!=project||copy.DocumentId!=document||copy.ReferenceId!=reference)throw new InvalidDataException("自有参考与当前项目/文档/引用身份不符。");
        // Path, no-reparse, size and SHA-256 are checked before every decode, including cache refresh/large view.
        return ReadBounded(copy,project,document,reference,4096);
    }
    public const int ThumbnailEdge=512,SummaryEdge=2048,LargeViewEdge=4096;
    private static readonly System.Threading.SemaphoreSlim DecodeGate=new(1,1);
    public static BitmapSource ReadBounded(PlanningProjectCopy copy,Guid project,Guid document,Guid reference,int maximumEdge,CancellationToken token=default)
    {
        Identity(copy,project,document,reference);DecodeGate.Wait(token);
        try{using var input=PlanningProjectCopyStorage.OpenVerifiedRead(copy,token);return DecodeBounded(input,Path.GetExtension(copy.OriginalPath),maximumEdge,token);}
        finally{DecodeGate.Release();}
    }
    private static void Identity(PlanningProjectCopy copy,Guid project,Guid document,Guid reference)
    {if(copy.Kind!="reference"||copy.ProjectId!=project||copy.DocumentId!=document||copy.ReferenceId!=reference)throw new InvalidDataException("自有参考与当前项目/文档/引用身份不符。");}
    public sealed record Thumbnail(string Path,BitmapSource Image,bool CacheHit);
    public static Thumbnail ReadThumbnail(PlanningProjectCopy copy,Guid project,Guid document,Guid reference,string cacheRoot,CancellationToken token=default)
    {
        Identity(copy,project,document,reference);DecodeGate.Wait(token);
        try
        {
            // Even a cache hit must hash the current original copy, under its no-write/no-delete lease.
            using var source=PlanningProjectCopyStorage.OpenVerifiedRead(copy,token);var header=Header(source,Path.GetExtension(copy.OriginalPath));Dimensions(header.Width,header.Height);source.Position=0;
            var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"PlanningBoundedV2|{copy.StorageRoot}|{copy.RelativePath}|{copy.ProjectId:N}|{copy.DocumentId:N}|{copy.ReferenceId:N}|{copy.Sha256}|{ThumbnailEdge}")));
            var directory=System.IO.Path.Combine(cacheRoot,"PlanningBoundedV2");System.IO.Directory.CreateDirectory(directory);var path=System.IO.Path.Combine(directory,key+".png");var receipt=path+".sha256";
            PlanningImageStorage.RejectLinks(cacheRoot,path);PlanningImageStorage.RejectLinks(cacheRoot,receipt);
            if(File.Exists(path)&&File.Exists(receipt))
            {
                try
                {
                    using var cached=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(cached.Length>4*1024*1024)throw new InvalidDataException("缓存超限");var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cached));
                    if(new FileInfo(receipt).Length!=64||digest!=File.ReadAllText(receipt))throw new InvalidDataException("缓存hash不符");cached.Position=0;var h=Header(cached,".png");var expected=Size(header.Width,header.Height,ThumbnailEdge);if(header.Orientation>=5)expected=(expected.Height,expected.Width);
                    if(h.Width!=expected.Width||h.Height!=expected.Height)throw new InvalidDataException("缓存尺寸不符");cached.Position=0;var image=DecodeBounded(cached,".png",ThumbnailEdge,token);token.ThrowIfCancellationRequested();return new(path,image,true);
                }
                catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException){/* Regenerate this exact cache only; never use unknown bytes. */}
            }
            source.Position=0;var result=DecodeBounded(source,Path.GetExtension(copy.OriginalPath),ThumbnailEdge,token);token.ThrowIfCancellationRequested();var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var tempHash=temp+".sha256";
            try
            {
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(result));encoder.Save(output);output.Flush(true);}
                using(var bytes=File.OpenRead(temp))File.WriteAllText(tempHash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));token.ThrowIfCancellationRequested();File.Move(temp,path,true);File.Move(tempHash,receipt,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(tempHash))File.Delete(tempHash);}
            token.ThrowIfCancellationRequested();return new(path,result,false);
        }
        finally{DecodeGate.Release();}
    }
    private static (int Width,int Height) Size(int width,int height,int edge)
    {if(edge<1||edge>LargeViewEdge)throw new ArgumentOutOfRangeException(nameof(edge));var scale=Math.Min(1,(double)edge/Math.Max(width,height));return(Math.Max(1,(int)Math.Round(width*scale)),Math.Max(1,(int)Math.Round(height*scale)));}
    private static (int Width,int Height,int Orientation) Header(Stream input,string extension)
    {
        if(extension.Equals(".png",StringComparison.OrdinalIgnoreCase))
        {var b=new byte[33];input.ReadExactly(b);if(!b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||!b.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new InvalidDataException("PNG文件头无效");var w=BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16,4));var h=BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20,4));if(w>int.MaxValue||h>int.MaxValue)throw new InvalidDataException("图片尺寸超限");Dimensions((int)w,(int)h);return((int)w,(int)h,1);}
        if(!extension.Equals(".jpg",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("已保存但无法预览：当前安全呈现仅支持PNG/JPEG。");
        if(input.ReadByte()!=255||input.ReadByte()!=216)throw new InvalidDataException("JPEG文件头不符");using var relevant=new MemoryStream();relevant.Write(new byte[]{255,216});int segments=0;
        while(input.Position<input.Length)
        {
            if(++segments>65536||input.ReadByte()!=255)throw new InvalidDataException("JPEG段无效/过多");int marker;do{marker=input.ReadByte();}while(marker==255);if(marker<0)throw new InvalidDataException("JPEG截断");if(marker is 0xDA or 0xD9){relevant.Write(new byte[]{255,(byte)marker});break;}if(marker is 0xD8 or 0x01||marker is >=0xD0 and <=0xD7)continue;
            int a=input.ReadByte(),b=input.ReadByte();int length=(a<<8)|b;if(a<0||b<0||length<2||length-2>input.Length-input.Position)throw new InvalidDataException("JPEG段长度无效");var part=new byte[length-2];input.ReadExactly(part);
            if(marker==0xE1&&part.AsSpan().StartsWith("Exif\0\0"u8)||marker is >=0xC0 and <=0xCF&&marker is not(0xC4 or 0xC8 or 0xCC))
            {if(relevant.Length+length>256*1024)throw new InvalidDataException("重复/过多EXIF或尺寸段");relevant.Write(new byte[]{255,(byte)marker,(byte)a,(byte)b});relevant.Write(part);}
        }
        return JpegHeader(relevant.ToArray());
    }
    private static BitmapSource DecodeBounded(Stream input,string extension,int edge,CancellationToken token)
    {
        var h=Header(input,extension);var target=Size(h.Width,h.Height,edge);input.Position=0;token.ThrowIfCancellationRequested();
        try
        {
            // DelayCreation header inspection must not materialize full source pixels. WIC receives decode dimensions before EndInit.
            var decoder=BitmapDecoder.Create(input,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);if(decoder.Frames.Count!=1||decoder.Frames[0].PixelWidth!=h.Width||decoder.Frames[0].PixelHeight!=h.Height)throw new InvalidDataException("帧数/尺寸不符");input.Position=0;
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.CreateOptions=BitmapCreateOptions.PreservePixelFormat;bitmap.DecodePixelWidth=target.Width;bitmap.DecodePixelHeight=target.Height;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();token.ThrowIfCancellationRequested();
            if(bitmap.PixelWidth!=target.Width||bitmap.PixelHeight!=target.Height)throw new InvalidDataException("有界解码尺寸不符");var result=h.Orientation==1?(BitmapSource)bitmap:Orient(bitmap,h.Orientation,token);token.ThrowIfCancellationRequested();return result;
        }
        catch(Exception e)when(e is System.Runtime.InteropServices.COMException or FormatException or ArgumentException or InvalidOperationException or NotSupportedException){throw new InvalidDataException("已保存但无法预览：有界解码失败。"+e.Message,e);}
    }
    private static void Dimensions(int width,int height)
    {if(width<1||height<1||width>MaximumSide||height>MaximumSide||(long)width*height>MaximumPixels)throw new InvalidDataException("图片尺寸超限（最长边16384、总像素4000万）。");}
    public static BitmapSource Decode(byte[] data,string extension)
    {
        int width,height,orientation=1;bool jpeg;
        if(extension.Equals(".png",StringComparison.OrdinalIgnoreCase))
        {
            if(data.Length<33||!data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||!data.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new InvalidDataException("PNG文件头与声明格式不符。");
            var w=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));var h=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));if(w>int.MaxValue||h>int.MaxValue)throw new InvalidDataException("图片尺寸超限。");width=(int)w;height=(int)h;jpeg=false;
        }
        else if(extension.Equals(".jpg",StringComparison.OrdinalIgnoreCase)||extension.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))
        {(width,height,orientation)=JpegHeader(data);jpeg=true;}
        else throw new NotSupportedException("已保存但无法预览：当前安全呈现仅支持PNG及8位灰度/RGB JPEG。");
        Dimensions(width,height);
        try
        {
            using var stream=new MemoryStream(data,false);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            if(decoder.Frames.Count!=1)throw new NotSupportedException("已保存但无法预览：不支持多帧图片。");var frame=decoder.Frames[0];if(frame.PixelWidth!=width||frame.PixelHeight!=height)throw new InvalidDataException("图片文件头与解码尺寸不符。");frame.Freeze();
            if(!jpeg||orientation==1)return frame;
            return Orient(frame,orientation);
        }
        catch(Exception e)when(e is System.Runtime.InteropServices.COMException or FormatException or ArgumentException or InvalidOperationException or NotSupportedException)
        {throw new InvalidDataException("已保存但无法预览：图片解码失败。"+e.Message,e);}
    }
    private static (int Width,int Height,int Orientation) JpegHeader(byte[] data)
    {
        if(data.Length<4||data[0]!=255||data[1]!=216)throw new InvalidDataException("JPEG文件头不符。");int pos=2,w=0,h=0,orientation=1;bool exif=false;
        while(pos<data.Length)
        {
            if(data[pos++]!=255)throw new InvalidDataException("JPEG标记不完整。");while(pos<data.Length&&data[pos]==255)pos++;if(pos>=data.Length)break;var marker=data[pos++];if(marker is 0xDA or 0xD9)break;if(marker is 0xD8 or 0x01 ||marker is >=0xD0 and <=0xD7)continue;
            if(pos+2>data.Length)throw new InvalidDataException("JPEG段截断。");int length=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos,2));if(length<2||length>data.Length-pos)throw new InvalidDataException("JPEG段长度无效。");var segment=data.AsSpan(pos+2,length-2);
            if(marker==0xE1&&segment.StartsWith("Exif\0\0"u8)){if(exif)throw new InvalidDataException("重复EXIF方向元数据。");orientation=ExifOrientation(segment[6..]);exif=true;}
            if(marker is >=0xC0 and <=0xCF && marker is not(0xC4 or 0xC8 or 0xCC))
            {
                if(marker is not(0xC0 or 0xC1 or 0xC2)||segment.Length<6||segment[0]!=8||segment[5] is not(1 or 3))throw new NotSupportedException("已保存但无法预览：仅支持8位灰度/RGB baseline/progressive JPEG。");
                var nextH=BinaryPrimitives.ReadUInt16BigEndian(segment[1..3]);var nextW=BinaryPrimitives.ReadUInt16BigEndian(segment[3..5]);if(segment.Length!=6+3*segment[5]||w!=0)throw new InvalidDataException("JPEG尺寸段重复或无效。");w=nextW;h=nextH;Dimensions(w,h);
            }
            pos+=length;
        }
        if(w==0||h==0)throw new InvalidDataException("JPEG缺少受支持的尺寸段。");return(w,h,orientation);
    }
    private static int ExifOrientation(ReadOnlySpan<byte> tiff)
    {
        if(tiff.Length<8)throw new InvalidDataException("EXIF头截断。");bool little=tiff[0]==73&&tiff[1]==73;if(!little&&!(tiff[0]==77&&tiff[1]==77))throw new InvalidDataException("EXIF字节序无效。");
        static ushort U16(ReadOnlySpan<byte> b,bool le)=>le?BinaryPrimitives.ReadUInt16LittleEndian(b):BinaryPrimitives.ReadUInt16BigEndian(b);
        static uint U32(ReadOnlySpan<byte> b,bool le)=>le?BinaryPrimitives.ReadUInt32LittleEndian(b):BinaryPrimitives.ReadUInt32BigEndian(b);
        if(U16(tiff[2..4],little)!=42)throw new InvalidDataException("EXIF TIFF签名无效。");var offset=U32(tiff[4..8],little);if(offset>tiff.Length-2)throw new InvalidDataException("EXIF IFD偏移超界。");var count=U16(tiff.Slice((int)offset,2),little);if(count>1024||(long)offset+2+count*12>tiff.Length)throw new InvalidDataException("EXIF目录超限或截断。");int orientation=1;bool found=false;
        for(int i=0;i<count;i++){var entry=tiff.Slice((int)offset+2+i*12,12);if(U16(entry,little)!=274)continue;if(found||U16(entry[2..4],little)!=3||U32(entry[4..8],little)!=1)throw new InvalidDataException("EXIF方向字段无效/重复。");orientation=U16(entry[8..10],little);if(orientation is <1 or >8)throw new InvalidDataException("EXIF方向须为1到8。");found=true;}return orientation;
    }
    // Discrete pixel permutation; no resampling, color correction or implicit second orientation.
    private static BitmapSource Orient(BitmapSource input,int orientation,CancellationToken token=default)
    {
        var source=new FormatConvertedBitmap(input,PixelFormats.Bgra32,null,0);source.Freeze();int w=source.PixelWidth,h=source.PixelHeight,ow=orientation>=5?h:w,oh=orientation>=5?w:h;var bytes=new byte[checked(w*h*4)];source.CopyPixels(bytes,w*4,0);var output=new byte[bytes.Length];
        for(int y=0;y<h;y++){if((y&31)==0)token.ThrowIfCancellationRequested();for(int x=0;x<w;x++){var (ox,oy)=orientation switch{2=>(w-1-x,y),3=>(w-1-x,h-1-y),4=>(x,h-1-y),5=>(y,x),6=>(h-1-y,x),7=>(h-1-y,w-1-x),8=>(y,w-1-x),_=>(x,y)};Buffer.BlockCopy(bytes,(y*w+x)*4,output,(oy*ow+ox)*4,4);}}
        var bitmap=BitmapSource.Create(ow,oh,96,96,PixelFormats.Bgra32,null,output,ow*4);bitmap.Freeze();return bitmap;
    }
}
