using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
internal static class ProductRawAudit
{
 private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
 public static async Task<int> Run(string input,string expected,string output)
 {
  if(Directory.Exists(output))throw new IOException("New isolated output directory required.");
  if(!Hash(input).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Authorized original identity mismatch.");
  Directory.CreateDirectory(output);var copy=Path.Combine(output,"authorized-copy"+Path.GetExtension(input));File.Copy(input,copy);var rows=new List<object>();var process=Process.GetCurrentProcess();var timeline=new List<object>();var timer=Stopwatch.StartNew();
  void Sample(string stage){process.Refresh();timeline.Add(new{stage,ms=timer.Elapsed.TotalMilliseconds,managed=GC.GetTotalMemory(false),workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,ledgerBytes=StudioGlobalResources.AccountedBytes});}
  async Task Legacy()
  {
   var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var frozen=await pipeline.DecodeFrozenMasterAsync(copy);Sample("legacy-decoded");
   if(frozen.Image.Width<1000||frozen.Image.Height<1000||frozen.Image.SourceBitDepth!="16")throw new InvalidDataException("No full-resolution RGB16 product output.");
   using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{pipeline.Render(frozen.Image,null,null,token:cancel.Token);throw new InvalidDataException("Cancellation not observed.");}catch(OperationCanceledException){rows.Add(new{path="legacy",operation="pre-cancel",status="PASSED"});}}
   var tif=Path.Combine(output,"legacy.tif");await pipeline.ExportAsync(frozen,tif,null,null,outputIcc:StudioQuickExport.SrgbProfileBytes());
   TiffStreamValidation decoded;using(var f=File.OpenRead(tif))decoded=TiffStreamValidator.Validate(f);
   if(decoded.Width!=frozen.Image.Width||decoded.Height!=frozen.Image.Height||decoded.BitsPerSample!=16||decoded.Orientation!=frozen.Image.Orientation||!decoded.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Legacy full TIFF product contract mismatch.");
   rows.Add(new{path="legacy",operation="full-TIFF16",status="PASSED",decoded.Width,decoded.Height,decoded.Orientation,decoded.BitsPerSample,pixelSha256=decoded.PixelSha256,fileSha256=Hash(tif)});
   // Exercise the existing product's quick PNG16 encoder, not the research pipeline.
   var codes=frozen.Image.Rgb32.Span.ToArray().Select(v=>(ushort)Math.Clamp(Math.Round(v*65535),0,65535)).ToArray();
   var bitmap=BitmapSource.Create(frozen.Image.Width,frozen.Image.Height,96,96,PixelFormats.Rgb48,null,codes,frozen.Image.Width*6);bitmap.Freeze();
   var upright=StudioQuickExport.ApplyOrientation(bitmap,frozen.Image.Orientation);var png=Path.Combine(output,"legacy-quick.png");StudioQuickExport.Encode(upright,copy,png,StudioExportFormat.Png);
   using(var stream=File.OpenRead(png))
   {
    var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];
    if(frame.Format.BitsPerPixel<48||frame.PixelWidth!=upright.PixelWidth||frame.PixelHeight!=upright.PixelHeight||frame.ColorContexts is not {Count:>0})throw new InvalidDataException("Legacy PNG16 geometry/depth/profile missing.");
    using var profile=frame.ColorContexts[0].OpenProfileStream();using var profileBytes=new MemoryStream();profile.CopyTo(profileBytes);if(!profileBytes.ToArray().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Legacy PNG output profile mismatch.");
    var rgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var before=new FormatConvertedBitmap(upright,PixelFormats.Rgb48,null,0);var expectedRow=new byte[frame.PixelWidth*6];var actualRow=new byte[expectedRow.Length];
    for(int y=0;y<frame.PixelHeight;y++){rgb.CopyPixels(new System.Windows.Int32Rect(0,y,frame.PixelWidth,1),actualRow,actualRow.Length,0);before.CopyPixels(new System.Windows.Int32Rect(0,y,frame.PixelWidth,1),expectedRow,expectedRow.Length,0);if(!actualRow.SequenceEqual(expectedRow))throw new InvalidDataException("Legacy quick PNG16 lossless pixels differ.");}
    rows.Add(new{path="legacy",operation="full-quick-PNG16",status="PASSED",frame.PixelWidth,frame.PixelHeight,bitsPerChannel=16,orientationNormalized=1,iccExact=true,pixelsExact=true,fileSha256=Hash(png)});
   }
  }
  async Task Pv4()
  {
   var recipe=new ProfessionalRawDefinition();var master=await new ProfessionalRawDecoder().DecodeAsync(copy,recipe);Sample("PV4-decoded");
   var small=ProfessionalRawPipeline.RenderPreview(master,recipe,768);var region=ProfessionalRawPipeline.RenderRegion(master,recipe,0,0,Math.Min(64,ProfessionalRawPipeline.OrientedSize(master).Width),Math.Min(64,ProfessionalRawPipeline.OrientedSize(master).Height));
   if(small.Width<=0||region.Width<=0)throw new InvalidDataException("Product preview/region missing.");
   foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png})
   {
    var path=Path.Combine(output,format==StudioExportFormat.Tiff?"pv4.tif":"pv4.png");await ProfessionalRawExport.WriteAsync(master,recipe,path,format);var report=ProfessionalRawExport.Inspect(path,master,recipe,format);
    rows.Add(new{path="PV4",operation=format.ToString(),status="PASSED",inspection=report,fileSha256=Hash(path)});Sample("PV4-export-"+format);
   }
   rows.Add(new{path="PV4",operation="numeric-contract",status="PASSED",master.Contract.DecoderVersion,master.Contract.WhiteBalanceApplied,master.Contract.ClippingPolicy,master.Contract.SaturatedChannelSamples,master.Contract.ZeroChannelSamples,master.Contract.OutputSpace,preview=new{small.Width,small.Height},region=new{region.Width,region.Height}});
  }
  var status="FAILED";
  try
  {
   Sample("baseline");await Legacy();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Sample("legacy-released-for-next-operation");
   try{await Pv4();}catch(Exception e) when(e is NotSupportedException){rows.Add(new{path="PV4",operation="decode",status="REFUSED",reason=e.Message});}
   for(var i=0;i<10;i++){await Task.Delay(1000);Sample("natural-after-task-"+i);}
   if(Hash(input)!=expected.ToUpperInvariant()||Hash(copy)!=expected.ToUpperInvariant())throw new InvalidDataException("Original or isolated copy changed.");status="COMPLETED_PRODUCT_BACKEND_NOT_DESKTOP";return 0;
  }
  catch(Exception e){rows.Add(new{status="FAILED",error=e.ToString()});throw;}
  finally
  {
   var modules=process.Modules.Cast<ProcessModule>().Where(m=>new[]{"raw_r.dll","lcms2.dll","jpeg8.dll","zlib1.dll"}.Contains(m.ModuleName)).Select(m=>new{name=m.ModuleName,path=m.FileName,sha256=Hash(m.FileName)}).ToArray();
   await File.WriteAllTextAsync(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{status,sourceSha256=expected,isolatedCopySha256=Hash(copy),rows,timeline,modules,desktopStartup="NOT_RUN",navigation="NOT_RUN",inflightCancel="NOT_RUN",saveReopen="Covered separately by product regression, not this probe",notes="Backend product APIs with full source dimensions; not foreground/native UI acceptance. Natural process series is not workspace-disposal proof."},new JsonSerializerOptions{WriteIndented=true}));
  }
 }
}

