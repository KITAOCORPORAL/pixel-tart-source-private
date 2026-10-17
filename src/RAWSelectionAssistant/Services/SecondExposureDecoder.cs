using System.IO;using System.Windows.Media;using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.RawToJpeg;using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Services;
public sealed class SecondExposureDecoder:ISecondExposureDecoder
{
 public HighBitDepthImageBuffer Read(CandidateMultipleInput input,CancellationToken token)
 {
  input.ValidateSource();CandidateProcessingEvidence.Enter("SecondDecode",token);
  if(RAWSelectionAssistant.Core.Services.Projects.RawMatchTiff16ProductPipeline.IsRaw(input.Path))
  {var decoded=new LibRawDecoder().DecodeAsync(input.Path,new(AutoRotate:true,DecodeMode:RawDecodeMode.ProfessionalDecode),token).GetAwaiter().GetResult();return HighBitDepthImageBuffer.FromRaw(decoded);}
  var bitmap=StudioQuickExport.Load(input.Path);var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb48,null,0);var values=new float[rgb.PixelWidth*rgb.PixelHeight*3];var row=new ushort[rgb.PixelWidth*3];for(int y=0;y<rgb.PixelHeight;y++){token.ThrowIfCancellationRequested();rgb.CopyPixels(new System.Windows.Int32Rect(0,y,rgb.PixelWidth,1),row,row.Length*2,0);for(int i=0;i<row.Length;i++)values[y*row.Length+i]=row[i]/65535f;}return new(rgb.PixelWidth,rgb.PixelHeight,values,"16","sRGB",1);
 }
 public static void EnsureRegistered()=>SecondExposureInputs.Decoder??=new SecondExposureDecoder();
 public static CandidateMultipleInput Describe(string path)
 {
  path=System.IO.Path.GetFullPath(path);using var stream=File.OpenRead(path);var sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
  // Exact selected file only. Metadata does not justify decoding an unbounded unknown image.
  using var s=File.OpenRead(path);var frame=BitmapDecoder.Create(s,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None).Frames[0];var orientation=1;
  if(frame.Metadata is BitmapMetadata meta){foreach(var q in new[]{"/ifd/{ushort=274}","/app1/ifd/{ushort=274}"})try{if(meta.GetQuery(q) is ushort o)orientation=o;}catch(NotSupportedException){}}
  int w=orientation>=5?frame.PixelHeight:frame.PixelWidth,h=orientation>=5?frame.PixelWidth:frame.PixelHeight;
  if((long)w*h*12>512L*1024*1024)throw new NotSupportedException("Second exposure exceeds explicit 512MiB input budget");return new(1,path,sha,w,h);
 }
}
