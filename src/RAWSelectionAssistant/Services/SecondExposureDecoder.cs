using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
public sealed class SecondExposureDecoder:ISecondExposureDecoder,ISecondExposurePixelDecoder
{
 public bool ValidatesSourceIdentity=>true;
 public ISecondExposureSource OpenPixels(CandidateMultipleInput input,CancellationToken token)=>new BoundedTiffPixelSource(input,token);
 public HighBitDepthImageBuffer Read(CandidateMultipleInput input,CancellationToken token)
 {
  using var measurement=StudioPreviewDiagnostics.Measure("SecondDecode");
  input.Validate();if(input.Version==3)return BoundedSrgbTiffInput.Read(input,token);if(input.Version==1)return ReadLegacy(input,token);
  CandidateProcessingEvidence.Enter("SecondHashBefore",token);
  using var stream=new FileStream(input.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
  Verify(stream,input,token);stream.Position=0;
  // Header access and each WIC call are synchronous, non-interruptible codec boundaries.
  CandidateProcessingEvidence.Enter("SecondDecode",token);
  var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];
  token.ThrowIfCancellationRequested();int orientation=ReadOrientation(frame);
  int width=orientation>=5?frame.PixelHeight:frame.PixelWidth,height=orientation>=5?frame.PixelWidth:frame.PixelHeight;
  if(width!=input.Width||height!=input.Height)throw new InvalidDataException("SECOND_SOURCE_DIMENSIONS_CHANGED");
  using var scratch=StudioGlobalResources.ReserveWork(checked((long)frame.PixelWidth*frame.PixelHeight*64+stream.Length+1024*1024));
  using var rowBudget=StudioGlobalResources.ReserveWork(checked(frame.PixelWidth*14L));
  var rgba=new FormatConvertedBitmap(frame,PixelFormats.Rgba64,null,0);
  BitmapSource rgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);
  if(frame.ColorContexts is {Count:>0})rgb=new ColorConvertedBitmap(rgb,frame.ColorContexts[0],new ColorContext(PixelFormats.Bgra32),PixelFormats.Rgb48);
  token.ThrowIfCancellationRequested();var values=new float[checked(width*height*3)];var colorRow=new ushort[frame.PixelWidth*3];var alphaRow=new ushort[frame.PixelWidth*4];
  for(int y=0;y<frame.PixelHeight;y++)
  {
   CandidateProcessingEvidence.Enter("SecondCopyRow",token);var rectangle=new Int32Rect(0,y,frame.PixelWidth,1);
   rgba.CopyPixels(rectangle,alphaRow,frame.PixelWidth*8,0);rgb.CopyPixels(rectangle,colorRow,frame.PixelWidth*6,0);token.ThrowIfCancellationRequested();
   for(int x=0;x<frame.PixelWidth;x++)
   {
    if((x&4095)==0)token.ThrowIfCancellationRequested();
    var(ox,oy)=Orient(x,y,frame.PixelWidth,frame.PixelHeight,orientation);int i=(oy*width+ox)*3;double alpha=alphaRow[x*4+3]/65535d;
    for(int c=0;c<3;c++)values[i+c]=(float)(colorRow[x*3+c]/65535d*alpha+1-alpha);
   }
  }
  CandidateProcessingEvidence.Enter("SecondHashAfter",token);stream.Position=0;Verify(stream,input,token);
  return new(width,height,values,"16","sRGB",1);
 }
 private static void Verify(Stream stream,CandidateMultipleInput input,CancellationToken token)
 {if(!CandidateMultipleInput.HashSource(stream,token).Equals(input.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SECOND_SOURCE_CHANGED");}
 private static HighBitDepthImageBuffer ReadLegacy(CandidateMultipleInput input,CancellationToken token)
 {
  input.ValidateSource(token);CandidateProcessingEvidence.Enter("SecondDecode",token);
  // Saved component1 RAW secondary inputs keep their original decoder contract.
  if(RawMatchTiff16ProductPipeline.IsRaw(input.Path))
  {
   var decoded=new RAWSelectionAssistant.Core.Services.RawToJpeg.LibRawDecoder().DecodeAsync(input.Path,new RAWSelectionAssistant.Core.Models.RawToJpegOptions(AutoRotate:true,DecodeMode:RAWSelectionAssistant.Core.Models.RawDecodeMode.ProfessionalDecode),token).GetAwaiter().GetResult();
   return HighBitDepthImageBuffer.FromRaw(decoded);
  }
  var bitmap=StudioQuickExport.Load(input.Path);var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb48,null,0);var values=new float[checked(rgb.PixelWidth*rgb.PixelHeight*3)];var row=new ushort[rgb.PixelWidth*3];
  for(int y=0;y<rgb.PixelHeight;y++){token.ThrowIfCancellationRequested();rgb.CopyPixels(new Int32Rect(0,y,rgb.PixelWidth,1),row,row.Length*2,0);for(int i=0;i<row.Length;i++)values[y*row.Length+i]=row[i]/65535f;}return new(rgb.PixelWidth,rgb.PixelHeight,values,"16","sRGB",1);
 }
 public static void EnsureRegistered()=>SecondExposureInputs.Decoder??=new SecondExposureDecoder();
 public static CandidateMultipleInput Describe(string path,CancellationToken token=default)
 {
  path=Path.GetFullPath(path);
  if(Path.GetExtension(path).ToLowerInvariant() is ".tif" or ".tiff")
  {try{return BoundedSrgbTiffInput.Describe(path,token);}catch(Exception e)when(e is InvalidDataException or NotSupportedException){/* Ordinary TIFF remains WIC2, never downsample. */}}
  if(Path.GetExtension(path).ToLowerInvariant() is not(".png" or ".tif" or ".tiff" or ".jpg" or ".jpeg"))throw new NotSupportedException("Second exposure supports PNG, TIFF and JPEG only");
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);token.ThrowIfCancellationRequested();
  var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];token.ThrowIfCancellationRequested();var o=ReadOrientation(frame);
  int w=o>=5?frame.PixelHeight:frame.PixelWidth,h=o>=5?frame.PixelWidth:frame.PixelHeight;
  if(checked((long)w*h*76+stream.Length)>StudioGlobalResources.TotalLimit/2)throw new NotSupportedException("Second exposure decoded working set exceeds explicit 1GiB admission");
  stream.Position=0;var sha=CandidateMultipleInput.HashSource(stream,token);return new(2,path,sha,w,h);
 }
 public static int ReadOrientation(BitmapFrame frame)
 {
  if(frame.Metadata is not BitmapMetadata metadata)return 1;
  foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})
  try
  {
   var value=metadata.GetQuery(query);long o=value switch{byte x=>x,ushort x=>x,uint x=>x,short x=>x,int x=>x,long x=>x,_=>0};if(o is>=1 and<=8)return (int)o;
  }
  catch(Exception e)when(e is NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException){}
  return 1;
 }
 private static (int X,int Y) Orient(int x,int y,int w,int h,int orientation)=>orientation switch{2=>(w-1-x,y),3=>(w-1-x,h-1-y),4=>(x,h-1-y),5=>(y,x),6=>(h-1-y,x),7=>(h-1-y,w-1-x),8=>(y,w-1-x),_=>(x,y)};
}
