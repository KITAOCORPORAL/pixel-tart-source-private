using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
internal static class WicMatrixAudit
{
 public static void Run(string root,string output)
 {
  if(Directory.Exists(output))throw new IOException("New output required");Directory.CreateDirectory(output);
  var source=File.ReadAllBytes(Path.Combine(root,"tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc"));var target=StudioQuickExport.SrgbProfileBytes();
  File.WriteAllBytes(Path.Combine(output,"source.private.icc"),source);File.WriteAllBytes(Path.Combine(output,"target.private.icc"),target);
  var rows=new List<object>();
  foreach(var inputFormat in new[]{PixelFormats.Rgb24,PixelFormats.Bgra32,PixelFormats.Rgb48,PixelFormats.Rgba64,PixelFormats.Rgb128Float})
  foreach(var outputFormat in new[]{PixelFormats.Bgra32,PixelFormats.Rgb48,PixelFormats.Rgba64,PixelFormats.Rgb128Float})
  {
   var bytesPerPixel=inputFormat.BitsPerPixel/8;var data=new byte[3*bytesPerPixel];
   for(int i=0;i<data.Length;i++)data[i]=(byte)(i*37);
   if(inputFormat==PixelFormats.Rgb128Float)Buffer.BlockCopy(new float[]{.18f,.18f,.18f,1,.5f,.2f,.1f,1,1,0,0,1},0,data,0,data.Length);
   var image=BitmapSource.Create(3,1,96,96,inputFormat,null,data,data.Length);
   foreach(var profileKind in new[]{"linear-to-srgb","srgb-to-srgb"})
   {
    try
    {
     var sc=new ColorContext(new Uri(Path.GetFullPath(Path.Combine(output,profileKind=="linear-to-srgb"?"source.private.icc":"target.private.icc"))));var dc=new ColorContext(new Uri(Path.GetFullPath(Path.Combine(output,"target.private.icc"))));
     var converted=new ColorConvertedBitmap(image,sc,dc,outputFormat);var result=new byte[3*(outputFormat.BitsPerPixel/8)];converted.CopyPixels(result,result.Length,0);
     rows.Add(new{input=inputFormat.ToString(),output=outputFormat.ToString(),profileKind,status="EXECUTED",resultHex=Convert.ToHexString(result),bits=outputFormat.BitsPerPixel});
    }
    catch(Exception e){rows.Add(new{input=inputFormat.ToString(),output=outputFormat.ToString(),profileKind,status="REFUSED",hresult=e.HResult,message=e.Message});}
   }
  }
  File.WriteAllText(Path.Combine(output,"WIC_FORMAT_MATRIX.json"),JsonSerializer.Serialize(new{rows,nativeDesktop="NOT_RUN",sourceSha256=SonyProbe.Hash(Path.Combine(output,"source.private.icc")),targetSha256=SonyProbe.Hash(Path.Combine(output,"target.private.icc")),precisionPolicy="No 16-bit request substituted with 8-bit"},new JsonSerializerOptions{WriteIndented=true}));
 }
}
