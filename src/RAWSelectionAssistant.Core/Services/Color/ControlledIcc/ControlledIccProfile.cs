using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RAWSelectionAssistant.Core.Services.Color.ControlledIcc;

public sealed class ControlledIccException(string code,string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ControlledIccProfile
{
    public const int MaximumBytes = 4*1024*1024;
    private readonly byte[] bytes;
    public byte[] CopyBytes() => (byte[])bytes.Clone();
    public string Sha256 { get; }
    public string Version { get; }
    public string ColorSpace => "RGB";
    public string Pcs => "XYZ D50";
    public IReadOnlyList<string> Tags { get; }
    private ControlledIccProfile(byte[] data,string version,string[] tags)
    { bytes=data;Version=version;Tags=Array.AsReadOnly(tags);Sha256=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(); }
    public static ControlledIccProfile Read(ReadOnlySpan<byte> data,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        void Fail(string code,string reason)=>throw new ControlledIccException(code,reason);
        if(data.Length>MaximumBytes)Fail("ICC_TOO_LARGE","Profile exceeds4MiB research limit.");
        if(data.Length<132)Fail("ICC_LENGTH","ICC header/tag table incomplete.");
        if(BinaryPrimitives.ReadUInt32BigEndian(data)!=data.Length)Fail("ICC_LENGTH","Declared profile length differs from actual bytes.");
        if(!data.Slice(36,4).SequenceEqual("acsp"u8))Fail("ICC_SIGNATURE","Missing ICC signature.");
        if(data[8] is not (2 or 4))Fail("ICC_VERSION","Only v2/v4 inspected profiles supported.");
        if(!data.Slice(16,4).SequenceEqual("RGB "u8)||!data.Slice(20,4).SequenceEqual("XYZ "u8))Fail("ICC_SPACE","Only RGB/XYZ PCS supported.");
        if(!data.Slice(12,4).SequenceEqual("mntr"u8)&&!data.Slice(12,4).SequenceEqual("scnr"u8))Fail("ICC_CLASS","Only display/input profiles supported.");
        if(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(64,4))>3)Fail("ICC_INTENT","Unknown header intent.");
        var count=BinaryPrimitives.ReadUInt32BigEndian(data.Slice(128,4));
        if(count>128||132L+count*12L>data.Length)Fail("ICC_TAG_TABLE","Tag table exceeds limits.");
        var entries=new Dictionary<string,(int Offset,int Length)>();
        var extents=new List<(int Offset,int Length)>();
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();var row=data.Slice(132+12*i,12);var sig=Encoding.ASCII.GetString(row[..4]);
            var off=BinaryPrimitives.ReadUInt32BigEndian(row.Slice(4,4));var len=BinaryPrimitives.ReadUInt32BigEndian(row.Slice(8,4));
            if(off%4!=0||off<132L+count*12L||len<8||(ulong)off+len>(ulong)data.Length)Fail("ICC_TAG_BOUNDS","Tag outside profile or unaligned.");
            if(!entries.TryAdd(sig,((int)off,(int)len)))Fail("ICC_DUPLICATE_TAG","Duplicate tag signature.");
            foreach(var e in extents)if(off<e.Offset+e.Length&&e.Offset<(long)off+len&&(off!=e.Offset||len!=e.Length))Fail("ICC_TAG_OVERLAP","Partially overlapping tags.");
            extents.Add(((int)off,(int)len));
        }
        // Narrow research scope, deliberately not a general ICC admission parser.
        var allowed=new HashSet<string>{"rXYZ","gXYZ","bXYZ","wtpt","rTRC","gTRC","bTRC","chad","desc","cprt","dmnd","dmdd","chrm"};
        if(entries.Keys.Any(k=>!allowed.Contains(k)))Fail("ICC_UNSUPPORTED_PROFILE","LUT/unknown tags outside first supported subset.");
        foreach(var name in new[]{"rXYZ","gXYZ","bXYZ","wtpt"})
        {
            if(!entries.TryGetValue(name,out var e)||e.Length!=20||!data.Slice(e.Offset,4).SequenceEqual("XYZ "u8))Fail("ICC_MATRIX","Required XYZ tag missing or malformed.");
        }
        foreach(var name in new[]{"rTRC","gTRC","bTRC"})
        {
            if(!entries.TryGetValue(name,out var e)||e.Length<12||!data.Slice(e.Offset,4).SequenceEqual("curv"u8))Fail("ICC_UNSUPPORTED_TRC","Identity curveType required.");
            uint n=BinaryPrimitives.ReadUInt32BigEndian(data.Slice(e.Offset+8,4));
            if(!((n==0&&e.Length==12)||(n==1&&e.Length==14&&BinaryPrimitives.ReadUInt16BigEndian(data.Slice(e.Offset+12,2))==256)))Fail("ICC_UNSUPPORTED_TRC","V1 admits identity or explicit gamma1.0 only.");
        }
        return new(data.ToArray(),$"{data[8]}.{data[9]>>4}",entries.Keys.Order().ToArray());
    }
}

/// <summary>Bounded embedded-profile inspection without decoding pixels or touching source files.</summary>
public static class EmbeddedIccInspector
{
    public sealed record Inspection(string Status,string? Sha256,string? Version,string? ColorSpace,string? Pcs,string? RejectionCode,string? Reason);
    public static Inspection Inspect(string file,CancellationToken token=default)
    {
        byte[] bytes;try{bytes=ReadEmbeddedBytes(file,token);}catch(ControlledIccException e){return new("REFUSED",null,null,null,null,e.Code,e.Message);}
        var sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string? version=bytes.Length>9?$"{bytes[8]}.{bytes[9]>>4}":null;
        string? space=bytes.Length>=20?Encoding.ASCII.GetString(bytes,16,4):null;string? pcs=bytes.Length>=24?Encoding.ASCII.GetString(bytes,20,4):null;
        try{ControlledIccProfile.Read(bytes,token);return new("SUPPORTED_RESEARCH_SUBSET_NOT_ACCURACY_APPROVAL",sha,version,space,pcs,null,null);}
        catch(ControlledIccException e){return new("REFUSED",sha,version,space,pcs,e.Code,e.Message);}
    }
    public static ControlledIccProfile Read(string file,CancellationToken token=default)=>ControlledIccProfile.Read(ReadEmbeddedBytes(file,token),token);
    private static byte[] ReadEmbeddedBytes(string file,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();using var s=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(s.Length<4)throw new ControlledIccException("ICC_CONTAINER","Container header incomplete.");
        Span<byte> magic=stackalloc byte[4];s.ReadExactly(magic);s.Position=0;
        byte[] profile;
        if(magic[0]==0xff&&magic[1]==0xd8)profile=Jpeg(s,token);
        else if((magic[0]==73&&magic[1]==73)||(magic[0]==77&&magic[1]==77))profile=Tiff(s,token);
        else throw new ControlledIccException("ICC_CONTAINER","Only classic TIFF/JPEG profile inspection supported.");
        return profile;
    }
    private static byte[] Jpeg(Stream s,CancellationToken token)
    {
        s.Position=2;var chunks=new SortedDictionary<int,byte[]>();int expected=0;
        while(s.Position<s.Length)
        {
            token.ThrowIfCancellationRequested();if(s.ReadByte()!=255)throw new ControlledIccException("ICC_CONTAINER","Invalid JPEG marker.");
            int marker;do{marker=s.ReadByte();}while(marker==255);
            if(marker is 0xda or 0xd9)break;if(marker<0)break;
            if(marker is 0x01 or >=0xd0 and <=0xd7)continue;
            int hi=s.ReadByte(),lo=s.ReadByte();if(hi<0||lo<0)throw new ControlledIccException("ICC_CONTAINER","Truncated JPEG.");
            int n=(hi<<8|lo)-2;if(n<0||s.Position+n>s.Length)throw new ControlledIccException("ICC_CONTAINER","Segment out of bounds.");
            if(marker==0xe2)
            {
                var b=new byte[n];s.ReadExactly(b);
                if(n>=12&&b.AsSpan(0,12).SequenceEqual("ICC_PROFILE\0"u8))
                {
                    if(n<14)throw new ControlledIccException("ICC_CHUNKS","ICC APP2 sequence header incomplete.");
                }
                if(n>=14&&b.AsSpan(0,12).SequenceEqual("ICC_PROFILE\0"u8))
                {
                    int index=b[12],total=b[13];if(index<1||index>total||total==0||(expected!=0&&total!=expected)||!chunks.TryAdd(index,b[14..]))throw new ControlledIccException("ICC_CHUNKS","Missing/duplicate ICC chunks.");
                    expected=total;if(chunks.Sum(p=>p.Value.Length)>ControlledIccProfile.MaximumBytes)throw new ControlledIccException("ICC_TOO_LARGE","Embedded ICC too large.");
                }
            }
            else s.Position+=n;
        }
        if(expected==0)throw new ControlledIccException("ICC_MISSING","No embedded profile; no silent sRGB assignment.");
        if(chunks.Count!=expected)throw new ControlledIccException("ICC_CHUNKS","Incomplete embedded profile.");
        return chunks.Values.SelectMany(b=>b).ToArray();
    }
    private static byte[] Tiff(Stream s,CancellationToken token)
    {
        byte[] h=new byte[8];s.ReadExactly(h);bool le=h[0]==73;
        uint U32(ReadOnlySpan<byte> b)=>le?BinaryPrimitives.ReadUInt32LittleEndian(b):BinaryPrimitives.ReadUInt32BigEndian(b);
        ushort U16(ReadOnlySpan<byte> b)=>le?BinaryPrimitives.ReadUInt16LittleEndian(b):BinaryPrimitives.ReadUInt16BigEndian(b);
        if(U16(h.AsSpan(2))!=42)throw new ControlledIccException("ICC_CONTAINER","BigTIFF/unknown TIFF unsupported.");
        long off=U32(h.AsSpan(4));if(off<8||off+2>s.Length)throw new ControlledIccException("ICC_CONTAINER","IFD outside file.");
        s.Position=off;byte[] nb=new byte[2];s.ReadExactly(nb);int count=U16(nb);if(count>4096||off+2+12L*count+4>s.Length)throw new ControlledIccException("ICC_CONTAINER","Invalid IFD extent.");
        byte[]? result=null;
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();byte[] e=new byte[12];s.ReadExactly(e);if(U16(e)!=34675)continue;
            if(result!=null)throw new ControlledIccException("ICC_DUPLICATE_TAG","Duplicate TIFF ICC tag.");
            if(U16(e.AsSpan(2)) is not (1 or 7))throw new ControlledIccException("ICC_CONTAINER","ICC TIFF type invalid.");
            uint n=U32(e.AsSpan(4)),pos=U32(e.AsSpan(8));if(n>ControlledIccProfile.MaximumBytes)throw new ControlledIccException("ICC_TOO_LARGE","Embedded ICC too large.");
            if(n<132||(ulong)pos+n>(ulong)s.Length)throw new ControlledIccException("ICC_LENGTH","ICC TIFF extent invalid.");
            long cursor=s.Position;s.Position=pos;result=new byte[n];s.ReadExactly(result);s.Position=cursor;
        }
        byte[] next=new byte[4];s.ReadExactly(next);
        if(U32(next)!=0)throw new ControlledIccException("ICC_CONTAINER","V1 accepts single-image first-IFD TIFF only; non-first IFD/multipage rejected.");
        return result??throw new ControlledIccException("ICC_MISSING","No embedded profile; no silent sRGB assignment.");
    }
}
