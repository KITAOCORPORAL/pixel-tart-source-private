using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using RAWSelectionAssistant.Services;
if(args.Length==2&&args[0]=="instant-product-visual"){await InstantProductVisual.Run(args[1]);return;}
if(args.Length==4&&args[0]=="instant-product-preview"){await InstantProductAudit.Run(args[1],args[3],args[2]);return;}
if(args.Length==3&&args[0]=="instant-product"){await InstantProductAudit.Run(args[1],args[2]);return;}
if(args.Length==2&&args[0]=="instant-rasters"){InstantRasterAudit.Run(args[1]);return;}
if(args.Length==3&&args[0]=="instant-full"){await InstantFullAudit.Run(args[1],args[2]);return;}
if(args.Length==3&&args[0]=="instant-review"){await InstantReviewAudit.Run(args[1],args[2]);return;}
if(args.Length==4&&args[0]=="legacy-codes"){await RawRepeatAudit.Run(args[1],args[2],args[3]);return;}
if(args.Length==4&&args[0]=="compare-tiff"){RawRepeatAudit.CompareTiff(args[1],args[2],args[3]);return;}
if(args.Length==4&&args[0]=="product-raw"){await ProductRawAudit.Run(args[1],args[2],args[3]);return;}
if(args.Length==2&&args[0]=="wic-reference"){WicIccReference.Run(args[1]);return;}
if(args.Length!=2||args[0]!="inventory")throw new ArgumentException("inventory <new-json>");
if(File.Exists(args[1]))throw new IOException("Refuse to overwrite inventory evidence.");
var observations=new List<object>();
for(uint i=0;i<32;i++)
{
 var d=new Native.Device{Size=Marshal.SizeOf<Native.Device>()};if(!Native.EnumDisplayDevicesW(null,i,ref d,0))break;
 if((d.State&1)==0)continue;
 var profile=WindowsMonitorProfileProbe.ObserveDevice(d.Name);
 observations.Add(new{d.Name,adapter=d.Description,stateFlags=d.State,profile});
}
File.WriteAllText(args[1],JsonSerializer.Serialize(new{observations,desktopLaunched=false,dpiModified=false,claim="Read-only display association; not physical calibration, WPF ICC conversion or HDR desktop proof."},new JsonSerializerOptions{WriteIndented=true}));
static class Native
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Device
 {public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint State;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern bool EnumDisplayDevicesW(string? name,uint index,ref Device device,uint flags);
}
