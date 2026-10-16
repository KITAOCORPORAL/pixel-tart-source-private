using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Color.DisplayPreview;

internal static class DisplayReferenceAudit
{
 public static void Run(string native,string output)
 {
  if(Directory.Exists(output))throw new IOException("New output directory required");Directory.CreateDirectory(output);
  var source=StudioQuickExport.SrgbProfileBytes();File.WriteAllBytes(Path.Combine(output,"source.private.icc"),source);
  ushort[] samples=[0,0,0,1,1,1,205,205,205,11800,11800,11800,32768,32768,32768,65535,65535,65535,65535,0,0,0,65535,0,0,0,65535,28000,17000,12000,50000,30000,23000,65534,1,32768];
  using var engine=new ResearchDisplayLcms(native);var cache=new DisplayPreviewCache(engine);var rows=new List<object>();
  foreach(var device in new[]{@"\\.\DISPLAY1",@"\\.\DISPLAY2"})
  {
   var association=WindowsMonitorProfileProbe.ObserveDevice(device);
   if(association.State!="AssociatedRgbProfileHeaderVerified"||association.ProfilePath is null){rows.Add(new{device,status="NO_VALID_ASSOCIATION_UNCALIBRATED",association});continue;}
   var profile=File.ReadAllBytes(association.ProfilePath);var admittedPath=Path.Combine(output,(device.EndsWith('1')?"display1":"display2")+".private.icc");File.WriteAllBytes(admittedPath,profile);
   try
   {
    var first=cache.Convert(true,device,samples,samples.Length/3,1,source,profile);var repeat=cache.Convert(true,device,samples,samples.Length/3,1,source,profile);
    if(!first.Pixels.Span.SequenceEqual(repeat.Pixels.Span)||repeat.State!="ExperimentalCacheHit")throw new InvalidDataException("Display repeat/cache mismatch");
    var alpha=new ushort[samples.Length/3*4];for(int i=0;i<samples.Length/3;i++){samples.AsSpan(i*3,3).CopyTo(alpha.AsSpan(i*4,3));alpha[i*4+3]=65535;}
    var bitmap=BitmapSource.Create(samples.Length/3,1,96,96,PixelFormats.Rgba64,null,alpha,alpha.Length*2);
    ushort[]? wic=null;string wicStatus="NOT_RUN";string? failure=null;
    try
    {
     var transformed=new ColorConvertedBitmap(bitmap,new ColorContext(new Uri(Path.GetFullPath(Path.Combine(output,"source.private.icc")))),new ColorContext(new Uri(Path.GetFullPath(admittedPath))),PixelFormats.Rgba64);var rgba=new ushort[alpha.Length];transformed.CopyPixels(rgba,rgba.Length*2,0);wic=new ushort[samples.Length];for(int i=0;i<wic.Length/3;i++)rgba.AsSpan(i*4,3).CopyTo(wic.AsSpan(i*3,3));wicStatus="EXECUTED_RGBA64_NO_8BIT_DOWNGRADE";
    }
    catch(Exception error){wicStatus="REFUSED";failure=error.Message;}
    rows.Add(new{device,status=first.State,key=first.Key,inputRgb16=samples,lcmsRgb16=first.Pixels.ToArray(),wicRgb16=wic,wicStatus,wicFailure=failure,maximumWicDifference=wic is null?(int?)null:wic.Zip(first.Pixels.ToArray()).Max(pair=>Math.Abs(pair.First-pair.Second)),repeatExact=true,cacheHit=true,association});
   }
   catch(Exception error){rows.Add(new{device,status="REFUSED_NOT_ADMITTED_OR_INVALID_PROFILE",message=error.Message,association});}
  }
  var identity=cache.Convert(true,"identity",samples,samples.Length/3,1,source,source);if(!identity.Pixels.Span.SequenceEqual(samples))throw new InvalidDataException("sRGB identity changed");
  File.WriteAllText(Path.Combine(output,"DISPLAY_REFERENCE.json"),JsonSerializer.Serialize(new{status="RESEARCH_DISPLAY_TRANSFORMS_EXECUTED_NOT_PRODUCT_INTEGRATION",contract=new DisplayPreviewContract(),engine=engine.EngineIdentity,rows,srgbIdentityExact=true,disabledByDefault=true,wpfCompositor="NOT_RUN",physicalMonitors="NOT_RUN",exportIntegration="NOT_CONNECTED",privateProfilesNotCommitted=true,negativeOrAboveOne="Rejected by RGB16 contract; no silent clipping",alpha="OpaqueOnly; no premultiplied alpha claim"},new JsonSerializerOptions{WriteIndented=true}));
 }
}
