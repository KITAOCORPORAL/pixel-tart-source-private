using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
internal static class WicIccReference
{
 public static void Run(string output)
 {
  if(Directory.Exists(output))throw new IOException("New reference directory required.");Directory.CreateDirectory(output);
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
  // The audit executable may run from an arbitrary build path; the caller pins the explicit fixture root.
  root=Environment.GetEnvironmentVariable("KITAO_HOME_SOURCE_ROOT")??throw new InvalidOperationException("KITAO_HOME_SOURCE_ROOT required.");
  var linearPath=Path.Combine(root,"tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc");var linear=File.ReadAllBytes(linearPath);
  if(Convert.ToHexString(SHA256.HashData(linear))!="DAFBFF7207D22B526BAA8F61A280E197B639AF68CBE2E48529455157612B7407")throw new InvalidDataException("Linear fixture identity mismatch.");
  var target=StudioQuickExport.SrgbProfileBytes();File.WriteAllBytes(Path.Combine(output,"output-srgb.icc"),target);
  ushort[][] samples=[[0,0,0],[1,1,1],[205,205,205],[11800,11800,11800],[32768,32768,32768],[65535,65535,65535],[65535,0,0],[0,65535,0],[0,0,65535],[28000,17000,12000]];
  var input=samples.SelectMany(x=>x).ToArray();var image=BitmapSource.Create(samples.Length,1,96,96,PixelFormats.Rgb48,null,input,samples.Length*6);
  ushort[]? codes=null;string status="NOT_RUN";string? failure=null;
  try{var converted=new ColorConvertedBitmap(image,new ColorContext(new Uri(linearPath)),new ColorContext(PixelFormats.Bgra32),PixelFormats.Rgb48);codes=new ushort[input.Length];converted.CopyPixels(codes,input.Length*2,0);status="EXECUTED";}
  catch(Exception e) when(e is NotSupportedException or System.Runtime.InteropServices.COMException){status="UNSUPPORTED_PIXEL_FORMAT";failure=e.ToString();}
  File.WriteAllText(Path.Combine(output,"wic-reference.json"),JsonSerializer.Serialize(new{sourceProfileSha256=Convert.ToHexString(SHA256.HashData(linear)),outputProfileSha256=Convert.ToHexString(SHA256.HashData(target)),samples,inputCodes=input,wicCodes=codes,status,failure,sourceDomain="linear-sRGB D65 primaries with ICC PCS D50",outputDomain="encoded sRGB 16bit",intent="WIC API has no explicit intent argument; profile/default behavior",engine="Windows WIC ColorConvertedBitmap",windowsCodecs=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory,"WindowsCodecs.dll")).FileVersion,scope="Synthetic reference only; not arbitrary ICC approval or monitor transform"},new JsonSerializerOptions{WriteIndented=true}));
 }
}

