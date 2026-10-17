using System.Buffers.Binary;
using System.IO;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
public static class BoundedSrgbTiffInput
{
 public static CandidateMultipleInput Describe(string path,CancellationToken token)
 {
  using var stream=File.OpenRead(path);var meta=TiffStreamValidator.Validate(stream,token);
  if(meta.BitsPerSample!=16||!meta.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new NotSupportedException("Bounded TIFF3 requires exact embedded sRGB RGB16 uncompressed product TIFF");
  int w=meta.Orientation>=5?meta.Height:meta.Width,h=meta.Orientation>=5?meta.Width:meta.Height;
  if(checked((long)w*h*12)>512L*1024*1024)throw new NotSupportedException("Bounded TIFF3 float source exceeds unchanged 512MiB input limit");
  stream.Position=0;return new(3,Path.GetFullPath(path),CandidateMultipleInput.HashSource(stream,token),w,h);
 }
 public static HighBitDepthImageBuffer Read(CandidateMultipleInput input,CancellationToken token)
 {
  using var stream=new FileStream(input.Path,FileMode.Open,FileAccess.Read,FileShare.Read);CandidateProcessingEvidence.Enter("SecondHashBefore",token);
  if(CandidateMultipleInput.HashSource(stream,token)!=input.Sha256)throw new InvalidDataException("SECOND_SOURCE_CHANGED");
  CandidateProcessingEvidence.Enter("SecondDecode",token);var pixels=new float[checked(input.Width*input.Height*3)];
  // Validator streams exactly one row; no whole-image decoder/cache/intermediate array.
  var meta=TiffStreamValidator.Validate(stream,token);if(meta.BitsPerSample!=16||!meta.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Bounded TIFF3 profile changed");
  int w=meta.Orientation>=5?meta.Height:meta.Width,h=meta.Orientation>=5?meta.Width:meta.Height;if(w!=input.Width||h!=input.Height)throw new InvalidDataException("SECOND_SOURCE_DIMENSIONS_CHANGED");
  using var rowLease=StudioGlobalResources.ReserveWork(meta.Width*6L+1024*1024);
  TiffStreamValidator.Validate(stream,token,(y,row)=>
  {
   CandidateProcessingEvidence.Enter("SecondCopyRow",token);
   for(int x=0;x<meta.Width;x++)
   {
    if((x&4095)==0)token.ThrowIfCancellationRequested();var(ox,oy)=meta.Orientation switch{2=>(meta.Width-1-x,y),3=>(meta.Width-1-x,meta.Height-1-y),4=>(x,meta.Height-1-y),5=>(y,x),6=>(meta.Height-1-y,x),7=>(meta.Height-1-y,meta.Width-1-x),8=>(y,meta.Width-1-x),_=>(x,y)};
    for(int c=0;c<3;c++)pixels[(oy*w+ox)*3+c]=BinaryPrimitives.ReadUInt16LittleEndian(row.Span[(x*6+c*2)..])/65535f;
   }
  });
  CandidateProcessingEvidence.Enter("SecondHashAfter",token);stream.Position=0;if(CandidateMultipleInput.HashSource(stream,token)!=input.Sha256)throw new InvalidDataException("SECOND_SOURCE_CHANGED");return new(w,h,pixels,"16","sRGB",1);
 }
}
