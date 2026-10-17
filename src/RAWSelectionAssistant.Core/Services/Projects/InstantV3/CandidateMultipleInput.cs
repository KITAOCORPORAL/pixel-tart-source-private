using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research;
public sealed record CandidateMultipleInput(int Version,string Path,string Sha256,int Width,int Height,ushort Orientation=1,double Weight=.5,string Mode="NormalizedMix",double CropX=.5,double CropY=.5,double Zoom=1,string Domain="OrientedDisplaySrgbD65V1")
{
 public CandidateMultipleInput Validate()
 {
  if(Version!=1||!System.IO.Path.IsPathFullyQualified(Path)||Sha256.Length!=64||!Sha256.All(Uri.IsHexDigit)||Width<=0||Height<=0||Orientation!=1||Mode is not("NormalizedMix" or "ExposureSum")||Domain!="OrientedDisplaySrgbD65V1"||!double.IsFinite(Weight)||Weight<0||Weight>1||!double.IsFinite(CropX)||CropX<0||CropX>1||!double.IsFinite(CropY)||CropY<0||CropY>1||!double.IsFinite(Zoom)||Zoom<1||Zoom>4)throw new InvalidDataException("Unknown/invalid second exposure contract");return this;
 }
 public void ValidateSource(){Validate();if(!File.Exists(Path))throw new FileNotFoundException("SECOND_SOURCE_MISSING",Path);using var file=File.OpenRead(Path);if(!Convert.ToHexString(SHA256.HashData(file)).Equals(Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SECOND_SOURCE_CHANGED");}
}
public interface ISecondExposureDecoder { HighBitDepthImageBuffer Read(CandidateMultipleInput input,CancellationToken token); }
public static class SecondExposureInputs
{
 // Application supplies its existing colour-managed decoder; Core never scans paths or guesses profiles.
 public static ISecondExposureDecoder? Decoder {get;set;}
}
internal sealed class MultiplePixelSource(ICandidatePixelSource first,HighBitDepthImageBuffer second,CandidateMultipleInput contract):ICandidatePixelSource
{
 public int Width=>first.Width;public int Height=>first.Height;
 public ColorVector3 Read(int x,int y)
 {
  double scale=Math.Max(Width/(double)second.Width,Height/(double)second.Height)*contract.Zoom;
  double visibleW=Width/scale,visibleH=Height/scale;
  double sx=(second.Width-visibleW)*contract.CropX+(x+.5)/scale-.5,sy=(second.Height-visibleH)*contract.CropY+(y+.5)/scale-.5;
  sx=Math.Clamp(sx,0,second.Width-1);sy=Math.Clamp(sy,0,second.Height-1);int x0=(int)sx,y0=(int)sy,x1=Math.Min(second.Width-1,x0+1),y1=Math.Min(second.Height-1,y0+1);
  ColorVector3 Pixel(int xx,int yy){int i=(yy*second.Width+xx)*3;var p=second.Rgb32.Span;return new(p[i],p[i+1],p[i+2]);}
  var b=CandidateTone.Mix(CandidateTone.Mix(Pixel(x0,y0),Pixel(x1,y0),sx-x0),CandidateTone.Mix(Pixel(x0,y1),Pixel(x1,y1),sx-x0),sy-y0);var a=first.Read(x,y);
  if(contract.Mode=="NormalizedMix")return CandidateExtensions.MultipleExposure(a,b,contract.Weight);
  var la=CandidateTone.Decode(a);var lb=CandidateTone.Decode(b);return CandidateTone.Encode(new ColorVector3(la.X+lb.X*contract.Weight,la.Y+lb.Y*contract.Weight,la.Z+lb.Z*contract.Weight));
 }
}
