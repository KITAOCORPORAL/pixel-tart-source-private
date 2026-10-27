using System.Buffers.Binary;
using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
/// <summary>Content-bound RGB ICC bytes, not a filename identity. General ICC parsing stays in LCMS.</summary>
public sealed record RgbIccIdentityV1(string Sha256,string BytesBase64,string Label,int Version=1)
{
 public static RgbIccIdentityV1 FromBytes(byte[] bytes,string label){ValidateBytes(bytes);return new(Convert.ToHexString(SHA256.HashData(bytes)),Convert.ToBase64String(bytes),label);}
 public byte[] Bytes(){if(Version!=1)throw new InvalidDataException("Unknown profile identity version");if(BytesBase64 is null||BytesBase64.Length>5600000)throw new InvalidDataException("ICC encoded size invalid");var b=Convert.FromBase64String(BytesBase64);ValidateBytes(b);if(Convert.ToHexString(SHA256.HashData(b))!=Sha256)throw new InvalidDataException("ICC content changed");return b;}
 private static void ValidateBytes(ReadOnlySpan<byte> b)
 {
  if(b.Length is <132 or >4194304||BinaryPrimitives.ReadUInt32BigEndian(b)!=b.Length||!b.Slice(36,4).SequenceEqual("acsp"u8)||!b.Slice(16,4).SequenceEqual("RGB "u8)||!b.Slice(20,4).SequenceEqual("XYZ "u8)||b[8] is not(2 or 4))throw new InvalidDataException("RGB matrix/shaper ICC v2/v4 XYZ header required; corrupt/LUT/CMYK profile refused");
  uint count=BinaryPrimitives.ReadUInt32BigEndian(b.Slice(128,4));if(count>128||132L+12L*count>b.Length)throw new InvalidDataException("Invalid ICC tag count");var tags=new HashSet<string>();var ranges=new List<(uint Start,uint Length)>();
  for(int i=0;i<count;i++){var entry=b.Slice(132+i*12,12);string tag=System.Text.Encoding.ASCII.GetString(entry[..4]);uint offset=BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(4,4)),length=BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(8,4));if(!tags.Add(tag)||offset%4!=0||offset<132L+count*12||length<8||(ulong)offset+length>(ulong)b.Length)throw new InvalidDataException("Invalid ICC tag bounds/duplicates");foreach(var r in ranges)if(offset<r.Start+r.Length&&r.Start<offset+length&&(offset!=r.Start||length!=r.Length))throw new InvalidDataException("Overlapping ICC tag data");ranges.Add((offset,length));if(tag.StartsWith("AToB",StringComparison.Ordinal)||tag.StartsWith("BToA",StringComparison.Ordinal)||tag.StartsWith("D2B",StringComparison.Ordinal)||tag.StartsWith("B2D",StringComparison.Ordinal))throw new NotSupportedException("DocumentColorV1 admits matrix-shaper RGB profiles only; LUT profiles not released");}
  foreach(string tag in new[]{"rXYZ","gXYZ","bXYZ","rTRC","gTRC","bTRC","wtpt"})if(!tags.Contains(tag))throw new InvalidDataException("RGB matrix/shaper profile missing "+tag);
 }
}
/// <summary>Explicit new variant; working pixels are encoded sRGB for existing photographic adjustment tools.
/// InputInterpretation Assign reinterprets original codes; Convert honours embedded profile. Not scene RAW.</summary>
public sealed record DocumentColorStateV1(RgbIccIdentityV1 Input,RgbIccIdentityV1 Working,RgbIccIdentityV1 Output,string InputPolicy="Embedded",int Version=1,string Engine="BundledLittleCms212ExactV1",string GamutPolicy="RelativeColorimetricClipAtOutputV1")
{
 public DocumentColorStateV1 Validate(){if(Version!=1||Engine!="BundledLittleCms212ExactV1"||GamutPolicy!="RelativeColorimetricClipAtOutputV1"||InputPolicy is not("Embedded" or "UntaggedExplicitSrgb" or "AssignExplicitProfile"))throw new InvalidDataException("Unknown document color state");if(Input is null||Working is null||Output is null)throw new InvalidDataException("ICC state missing");Input.Bytes();Working.Bytes();Output.Bytes();return this;}
}
