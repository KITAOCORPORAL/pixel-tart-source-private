using System.IO;using System.Text.Json;using System.Buffers.Binary;using System.Security.Cryptography;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;
internal static class LinearOutputAudit
{
 public static async Task Run(string manifest,string output)
 {
  if(Directory.Exists(output))throw new IOException("Fresh linear output evidence");Directory.CreateDirectory(output);var rows=new List<object>();
  foreach(var item in JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!)
  {
   using(var file=File.OpenRead(item.FrozenTiff))if(Convert.ToHexString(SHA256.HashData(file))!=item.ExpectedFrozenSha256)throw new InvalidDataException("Authorized hash");
   using var input=File.OpenRead(item.FrozenTiff);var frame=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];var rgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);const int w=64,h=48;var source=new ushort[w*h*3];rgb.CopyPixels(new Int32Rect(frame.PixelWidth/2,frame.PixelHeight/2,w,h),source,w*6,0);
   var data=source.Select(x=>x/65535f).ToArray();var basis=InstantFilmJpegInput.Definition;var master=new ProfessionalRawMaster(new(w,h,data,"16","sRGB",1),new("AuthorizedRGB16","Rendered",item.Id,item.ExpectedFrozenSha256,basis.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));
   var definition=basis with{SrgbExposure=new(ExposureEv:.7)};var folder=Path.Combine(output,item.Id);Directory.CreateDirectory(folder);var sourceBytes=new byte[data.Length*4];Buffer.BlockCopy(data,0,sourceBytes,0,sourceBytes.Length);File.WriteAllBytes(Path.Combine(folder,"input.f32"),sourceBytes);
   var formats=new List<object>();foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
   {
    var path=Path.Combine(folder,"output"+StudioQuickExport.Extension("source.png",format));await ProfessionalRawExport.WriteAsync(master,definition,path,format);var receipt=ProfessionalRawExport.Inspect(path,master,definition,format);
    using var file=File.OpenRead(path);var decoded=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];var pixels=new FormatConvertedBitmap(decoded,format==StudioExportFormat.Jpeg?PixelFormats.Rgb24:PixelFormats.Rgb48,null,0);var bytes=new byte[w*h*3*(format==StudioExportFormat.Jpeg?1:2)];pixels.CopyPixels(bytes,w*3*(format==StudioExportFormat.Jpeg?1:2),0);File.WriteAllBytes(Path.Combine(folder,format+".raw"),bytes);formats.Add(new{format,receipt});
   }
   rows.Add(new{id=item.Id,width=w,height=h,inputSha256=item.ExpectedFrozenSha256,exposure=.7,formats,scope="Actual production render plus three encoded files read independently; authorized center crop, not scene RAW"});
  }
  File.WriteAllText(Path.Combine(output,"MANIFEST.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
}
