using System.IO;
using System.Buffers.Binary;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
internal static class InstantV3DirectExport
{
 private sealed class Rows:IDisposable
 {
  private readonly CandidateRenderSession session;private CandidateImageLease? strip;private CandidateImageLease.BorrowedImage? borrow;private int top=-1;
  public Rows(CandidateRenderSession session){this.session=session;}
  public ReadOnlySpan<float> Get(int y,CancellationToken token)
  {
   if(strip is null||y<top||y>=top+strip.Height){borrow?.Dispose();strip?.Dispose();top=y;strip=session.RenderRegion(0,y,session.Layout.Width,Math.Min(32,session.Layout.Height-y),token);borrow=strip.Borrow();}
   return borrow!.Image.Rgb32.Span.Slice((y-top)*session.Layout.Width*3,session.Layout.Width*3);
  }
  public void Dispose(){borrow?.Dispose();strip?.Dispose();}
 }
 public static async Task<ProfessionalOutputInspection> Write(ProfessionalRawMaster master,ProfessionalRawDefinition definition,string destination,StudioExportFormat format,CancellationToken token,Action<string>? stage)
 {
  definition=definition with{InstantFilm=InstantFilmPipeline.Freeze(definition.InstantFilm!)};
  if(File.Exists(destination))throw new IOException("Output exists");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);var temporary=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!,".v3-"+Guid.NewGuid().ToString("N")+".tmp");
  await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
  try
  {
   var encodeClock=System.Diagnostics.Stopwatch.StartNew();
   {
   using var session=InstantV3ProductPipeline.Open(master,definition,token);using var rows=new Rows(session);using var rowLease=StudioGlobalResources.ReserveWork(session.Layout.Width*12L);
   void Fill(int y,Memory<byte> row){CandidateProcessingEvidence.Enter("EncodeStrip",token);var pixels=rows.Get(y,token);for(int i=0;i<pixels.Length;i++)BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i*2)..],(ushort)Math.Clamp(Math.Round(pixels[i]*65535),0,65535));}
   if(format is StudioExportFormat.Tiff or StudioExportFormat.Source)
    await Task.Run(()=>AtomicTiffWriter.WriteRowsAsync(temporary,session.Layout.Width,session.Layout.Height,Fill,new(IccProfile:StudioQuickExport.SrgbProfileBytes(),Orientation:1,Software:"Kitao Instant V3.1"),token,overwrite:false,stage:stage),token);
   else if(format is StudioExportFormat.Png or StudioExportFormat.Jpeg)
    await Task.Run(()=>ProfessionalWicRowEncoder.WriteRows(temporary,session.Layout.Width,session.Layout.Height,95,(y,row)=>{CandidateProcessingEvidence.Enter("EncodeStrip",token);if(format==StudioExportFormat.Png)Fill(y,row);else{var pixels=rows.Get(y,token);for(int x=0;x<session.Layout.Width;x++)for(int c=0;c<3;c++)row[x*3+2-c]=(byte)Math.Clamp(Math.Round(pixels[x*3+c]*255),0,255);}},token,png:format==StudioExportFormat.Png),token);
   else throw new NotSupportedException("Unknown V3 output format");
   }
   stage?.Invoke("EncodeMs="+encodeClock.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
   var validationClock=System.Diagnostics.Stopwatch.StartNew();
   CandidateProcessingEvidence.Enter("OutputValidation",token);
   token.ThrowIfCancellationRequested();stage?.Invoke("Encoded");var result=await Task.Run(()=>Inspect(temporary,master,definition,format,token),token);stage?.Invoke("ValidationMs="+validationClock.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));token.ThrowIfCancellationRequested();File.Move(temporary,destination,false);return result;
  }
  finally{try{if(File.Exists(temporary))File.Delete(temporary);}finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}}
 }
 public static ProfessionalOutputInspection Inspect(string path,ProfessionalRawMaster master,ProfessionalRawDefinition definition,StudioExportFormat format,CancellationToken token)
 {
  using var session=InstantV3ProductPipeline.Open(master,definition,token);using var expected=new Rows(session);var size=session.Layout;using var rowLease=StudioGlobalResources.ReserveWork(size.Width*12L);
  if(format is StudioExportFormat.Source or StudioExportFormat.Tiff)
  {
   using var stream=File.OpenRead(path);var result=TiffStreamValidator.Validate(stream,token,(y,row)=>{var rgb=expected.Get(y,token);for(int i=0;i<rgb.Length;i++)if(BinaryPrimitives.ReadUInt16LittleEndian(row.Span[(i*2)..])!=(ushort)Math.Clamp(Math.Round(rgb[i]*65535),0,65535))throw new InvalidDataException("V3 TIFF pixel mismatch");});
   if(result.Width!=size.Width||result.Height!=size.Height||result.Orientation!=1||!result.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("V3 TIFF ICC/geometry");return new(size.Width,size.Height,16,1,true,null,0,0,0){EncoderConfiguration="V3DirectStripsTIFF16"};
  }
  using var file=File.OpenRead(path);var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];bool png=format==StudioExportFormat.Png;var depth=frame.Format.BitsPerPixel>32?16:8;
  if(frame.PixelWidth!=size.Width||frame.PixelHeight!=size.Height||depth!=(png?16:8)||frame.ColorContexts is not{Count:>0})throw new InvalidDataException("V3 raster geometry/depth");
  using(var profile=frame.ColorContexts[0].OpenProfileStream()){using var bytes=new MemoryStream();profile.CopyTo(bytes);if(!bytes.ToArray().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("V3 ICC mismatch");}
  if(frame.Metadata is BitmapMetadata metadata)
  {
   foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})try{var orientation=metadata.GetQuery(query);if(orientation is ushort value&&value!=1)throw new InvalidDataException("V3 raster EXIF orientation not upright");}catch(NotSupportedException){}
  }
  // No orientation tag means upright by the raster container default; dimensions above are independently checked.
  var bitmap=new FormatConvertedBitmap(frame,png?PixelFormats.Rgb48:PixelFormats.Rgb24,null,0);var row=new byte[size.Width*3*(png?2:1)];double sum=0,squares=0;int max=0;
  for(int y=0;y<size.Height;y++){token.ThrowIfCancellationRequested();bitmap.CopyPixels(new Int32Rect(0,y,size.Width,1),row,row.Length,0);var rgb=expected.Get(y,token);for(int i=0;i<rgb.Length;i++){int want=(int)Math.Clamp(Math.Round(rgb[i]*(png?65535:255)),0,png?65535:255);int actual=png?BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(i*2)):row[i];var error=Math.Abs(want-actual);sum+=error;squares+=(double)error*error;max=Math.Max(max,error);}}
  double n=(double)size.Width*size.Height*3,mae=sum/n,rmse=Math.Sqrt(squares/n);if(png&&max!=0||!png&&(mae>5||rmse>15))throw new InvalidDataException("V3 raster pixel gate");
  return new(size.Width,size.Height,depth,1,true,png?null:95,mae,rmse,max){EncoderConfiguration=png?"V3DirectStripsPNG16":"V3DirectStripsJPEG95-444"};
 }
}
