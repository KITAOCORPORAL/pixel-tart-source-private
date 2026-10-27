using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
/// <summary>CPU exact sRGB document-display pixels -> monitor device RGB. Returned bitmap is untagged.
/// This is an explicit SDR app conversion, not camera/edit/output conversion or an ACM calibration promise.</summary>
public static class MonitorRgbDisplayV1
{
 public static BitmapSource Convert(BitmapSource source,RgbIccIdentityV1 monitor,CancellationToken token=default)
 {
  using var budget=StudioGlobalResources.ReserveWork(checked((long)source.PixelWidth*source.PixelHeight*12));
  using var cms=new LcmsRgbEngine();cms.Validate(monitor);using var transform=cms.CreateTransform(cms.StandardProfile("sRGB"),monitor);
  var raw=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[raw.PixelWidth*raw.PixelHeight*4];raw.CopyPixels(bytes,raw.PixelWidth*4,0);var rgb=new float[raw.PixelWidth*3];
  for(int y=0;y<raw.PixelHeight;y++){token.ThrowIfCancellationRequested();for(int x=0;x<raw.PixelWidth;x++){int i=(y*raw.PixelWidth+x)*4;rgb[x*3]=bytes[i+2]/255f;rgb[x*3+1]=bytes[i+1]/255f;rgb[x*3+2]=bytes[i]/255f;}var output=transform.Convert(rgb,token);for(int x=0;x<raw.PixelWidth;x++){int i=(y*raw.PixelWidth+x)*4;for(int c=0;c<3;c++)bytes[i+2-c]=(byte)Math.Clamp(Math.Round(output[x*3+c]*255),0,255);}}
  var result=BitmapSource.Create(raw.PixelWidth,raw.PixelHeight,96,96,PixelFormats.Bgra32,null,bytes,raw.PixelWidth*4);result.Freeze();return result;
 }
 public static (RgbIccIdentityV1? Profile,string State) Resolve(MonitorProfileObservation observation,RgbIccIdentityV1? manual)
 {
  try{using var cms=new LcmsRgbEngine();if(manual is not null){cms.Validate(manual);return(manual,"ManualRgbIccExactV1");}
   if(observation.ProfilePath is{} path&&observation.ProfileSha256 is{} expected){var profile=RgbIccIdentityV1.FromBytes(File.ReadAllBytes(path),"Monitor "+observation.Device);if(!profile.Sha256.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Monitor profile changed during read");cms.Validate(profile);return(profile,"WindowsAssociatedRgbIccExactV1");}
   return(null,"FallbackSrgb_NoUsableMonitorAssociation");
  }catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or NotSupportedException){return(null,"FallbackSrgb_ProfileRejected_"+e.GetType().Name);}
 }
}
