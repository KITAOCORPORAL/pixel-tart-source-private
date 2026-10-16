using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
internal static class DisplayMappingAudit
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Device
 {public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint State;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplayDevicesW(string? name,uint index,ref Device device,uint flags);
 public static void Run(string output)
 {
  if(File.Exists(output))throw new IOException("New result required");var rows=new List<object>();
  for(uint i=0;i<32;i++)
  {
   var adapter=new Device{Size=Marshal.SizeOf<Device>()};if(!EnumDisplayDevicesW(null,i,ref adapter,0))break;if((adapter.State&1)==0)continue;
   var monitors=new List<object>();for(uint j=0;j<16;j++){var monitor=new Device{Size=Marshal.SizeOf<Device>()};if(!EnumDisplayDevicesW(adapter.Name,j,ref monitor,1))break;monitors.Add(new{name=monitor.Name,description=monitor.Description,id=monitor.Id,key=monitor.Key,state=monitor.State});}
   rows.Add(new{adapter=adapter.Name,description=adapter.Description,monitors,association=WindowsMonitorProfileProbe.ObserveDevice(adapter.Name)});
  }
  // Offscreen WPF surface, not Desktop Window Manager output or physical display evidence.
  byte[] rgb=[10,80,220,128,128,128,250,30,20];var source=BitmapSource.Create(3,1,96,96,PixelFormats.Rgb24,null,rgb,9);source.Freeze();var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(source,new Rect(0,0,3,1));var target=new RenderTargetBitmap(3,1,96,96,PixelFormats.Pbgra32);target.Render(visual);var actual=new byte[12];target.CopyPixels(actual,12,0);bool exact=true;for(int i=0;i<3;i++)for(int c=0;c<3;c++)exact&=actual[i*4+c]==rgb[i*3+2-c];
  File.WriteAllText(output,JsonSerializer.Serialize(new{rows,offscreenWpfRgb24Preserved=exact,scope="WPF offscreen DrawImage/RenderTargetBitmap only; NOT physical screen or DWM compositor proof",systemSettingsModified=false,foregroundInteraction=false,physicalCrossMonitor="NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));
 }
}
