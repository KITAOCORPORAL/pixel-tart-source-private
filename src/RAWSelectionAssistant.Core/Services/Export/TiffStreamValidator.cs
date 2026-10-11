using System.Buffers.Binary;
using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.Export;

public sealed record TiffStreamValidation(int Width,int Height,int BitsPerSample,int Orientation,long PixelBytes,byte[] IccProfile,string PixelSha256);

/// <summary>Bounded verifier for our single-strip uncompressed RGB TIFF. Never buffers the file or full pixels.</summary>
public static class TiffStreamValidator
{
    public static TiffStreamValidation Validate(Stream source,CancellationToken token=default,Action<int,ReadOnlyMemory<byte>>? inspectRow=null)
    {
        if(!source.CanRead||!source.CanSeek)throw new ArgumentException("A seekable readable stream is required.");
        using var reader=new BinaryReader(source,System.Text.Encoding.ASCII,true);source.Position=0;
        if(reader.ReadByte()!='I'||reader.ReadByte()!='I'||reader.ReadUInt16()!=42)throw new InvalidDataException("Not baseline little-endian TIFF.");
        var ifd=reader.ReadUInt32();if(ifd<8||ifd>source.Length-2)throw new InvalidDataException("Invalid IFD.");source.Position=ifd;
        var count=reader.ReadUInt16();if(count>64)throw new InvalidDataException("Too many TIFF entries.");
        var tags=new Dictionary<ushort,(ushort Type,uint Count,uint Value)>();
        for(var i=0;i<count;i++)tags.Add(reader.ReadUInt16(),(reader.ReadUInt16(),reader.ReadUInt32(),reader.ReadUInt32()));
        uint Tag(ushort id)=>tags.TryGetValue(id,out var t)&&t.Count==1?t.Value:throw new InvalidDataException($"Missing or invalid tag {id}.");
        var width=checked((int)Tag(256));var height=checked((int)Tag(257));
        if(width<=0||height<=0||Tag(259)!=1||Tag(262)!=2||Tag(277)!=3||Tag(278)!=height)throw new InvalidDataException("Unsupported TIFF layout.");
        var bitsTag=tags[258];if(bitsTag.Type!=3||bitsTag.Count!=3)throw new InvalidDataException("Invalid bits tag.");source.Position=bitsTag.Value;
        var bits=reader.ReadUInt16();if(bits is not (8 or 16)||reader.ReadUInt16()!=bits||reader.ReadUInt16()!=bits)throw new InvalidDataException("Invalid bit depth.");
        var offset=Tag(273);var byteCount=checked((long)width*height*3*(bits/8));
        if(Tag(279)!=byteCount||offset+byteCount!=source.Length)throw new InvalidDataException("Truncated or trailing TIFF pixels.");
        var orientation=checked((int)Tag(274));if(orientation is <1 or >8)throw new InvalidDataException("Invalid orientation.");
        byte[] icc=[];if(tags.TryGetValue(34675,out var profile)){if(profile.Count>16*1024*1024||(long)profile.Value+profile.Count>offset)throw new InvalidDataException("Invalid ICC extent.");source.Position=profile.Value;icc=reader.ReadBytes(checked((int)profile.Count));if(icc.Length<128||!icc.AsSpan(36,4).SequenceEqual("acsp"u8)||BinaryPrimitives.ReadUInt32BigEndian(icc)!=icc.Length)throw new InvalidDataException("Invalid ICC profile.");}
        source.Position=offset;var row=new byte[checked(width*3*(bits/8))];using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for(var y=0;y<height;y++){token.ThrowIfCancellationRequested();source.ReadExactly(row);hash.AppendData(row);inspectRow?.Invoke(y,row);}
        return new(width,height,bits,orientation,byteCount,icc,Convert.ToHexString(hash.GetHashAndReset()));
    }
}
