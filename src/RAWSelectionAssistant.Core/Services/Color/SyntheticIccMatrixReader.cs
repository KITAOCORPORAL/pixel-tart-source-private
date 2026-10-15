using System.Buffers.Binary;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.ColorScience;
public sealed record SyntheticIccMatrixContract(string Sha256,ColorMatrix3x3 RgbToPcsD50,string ProfileId,string Transfer,string PcsWhitePoint);
/// <summary>Strict laboratory ICC v2 matrix/linear-TRC fixture subset, not general input-profile support.</summary>
public static class SyntheticIccMatrixReader
{
 public const string FixtureId="AnalystSyntheticIccLinearMatrixV1";
 public static SyntheticIccMatrixContract Read(ReadOnlySpan<byte> bytes,string expectedSha256,string declaration)
 {
  if(declaration!=FixtureId)throw new NotSupportedException("Only declared synthetic fixture profile; unknown device profile refused.");
  if(bytes.Length<132||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("ICC fixture hash or size mismatch.");
  var data=bytes.ToArray();uint U(int at)=>BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at,4));string Sig(int at)=>System.Text.Encoding.ASCII.GetString(data,at,4);
  if(U(0)!=data.Length||data[8]!=2||Sig(12)!="mntr"||Sig(16)!="RGB "||Sig(20)!="XYZ "||Sig(36)!="acsp")throw new NotSupportedException("Only ICC v2 RGB/XYZ matrix fixture supported.");
  double Fixed(int at)=>BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at,4))/65536d;
  if(Math.Abs(Fixed(68)-.9642)>.0001||Math.Abs(Fixed(72)-1)>.0001||Math.Abs(Fixed(76)-.8249)>.0001)throw new InvalidDataException("ICC PCS header must be D50.");
  var count=U(128);if(count!=7||132L+count*12>data.Length)throw new NotSupportedException("Unexpected ICC tag table; no implicit LUT/transfer fallback.");
  var tags=new Dictionary<string,(int Offset,int Length)>();var regions=new List<(int,int)>();var allowed=new HashSet<string>{"rXYZ","gXYZ","bXYZ","wtpt","rTRC","gTRC","bTRC"};
  for(int i=0;i<count;i++)
  {
   int row=132+i*12;var signature=Sig(row);var offset=U(row+4);var length=U(row+8);
   if(!allowed.Contains(signature)||!tags.TryAdd(signature,((int)offset,(int)length))||offset%4!=0||offset<132+count*12||(ulong)offset+length>(ulong)data.Length||regions.Any(r=>offset<(uint)(r.Item1+r.Item2)&&(ulong)offset+length>(uint)r.Item1))throw new InvalidDataException("ICC fixture unknown/duplicate/overlapping/out-of-range tags.");
   regions.Add(((int)offset,(int)length));
  }
  ColorVector3 Xyz(string key){var tag=tags[key];if(tag.Length!=20||Sig(tag.Offset)!="XYZ "||U(tag.Offset+4)!=0)throw new InvalidDataException("ICC XYZ fixture tag malformed.");return new(Fixed(tag.Offset+8),Fixed(tag.Offset+12),Fixed(tag.Offset+16));}
  foreach(var key in new[]{"rTRC","gTRC","bTRC"}){var t=tags[key];if(t.Length!=12||Sig(t.Offset)!="curv"||U(t.Offset+4)!=0||U(t.Offset+8)!=0)throw new NotSupportedException("Only identity/linear fixture TRC; encoded curves not assumed linear.");}
  var white=Xyz("wtpt");if(Math.Abs(white.X-.9642)>.0001||Math.Abs(white.Y-1)>.0001||Math.Abs(white.Z-.8249)>.0001)throw new InvalidDataException("ICC fixture media white must be D50.");
  var r=Xyz("rXYZ");var g=Xyz("gXYZ");var b=Xyz("bXYZ");var m=new ColorMatrix3x3(r.X,g.X,b.X,r.Y,g.Y,b.Y,r.Z,g.Z,b.Z);
  var sum=m.Transform(new(1,1,1));if(Math.Abs(sum.X-white.X)>.0003||Math.Abs(sum.Y-white.Y)>.0003||Math.Abs(sum.Z-white.Z)>.0003)throw new InvalidDataException("ICC matrix neutral axis inconsistent with media white.");
  return new(expectedSha256.ToUpperInvariant(),m,FixtureId,"Linear identity curv(0)","ICC PCS D50");
 }
 public static ColorVector3 ToLinearSrgb(ColorVector3 source,SyntheticIccMatrixContract profile)
 {
  if(profile.ProfileId!=FixtureId||profile.Transfer!="Linear identity curv(0)"||!double.IsFinite(source.X)||!double.IsFinite(source.Y)||!double.IsFinite(source.Z))throw new InvalidDataException("Synthetic profile/source contract mismatch.");
  var xyz=profile.RgbToPcsD50.Transform(source);var white=new ColorVector3(.9642,1,.8249);var xySum=white.X+white.Y+white.Z;
  xyz=LinearRgbColorScience.ChromaticAdaptation(white.X/xySum,white.Y/xySum,.3127,.3290).Transform(xyz);
  return LinearRgbColorScience.Get(WorkingColorSpace.LinearSRgb).FromXyz.Transform(xyz);
 }
}
