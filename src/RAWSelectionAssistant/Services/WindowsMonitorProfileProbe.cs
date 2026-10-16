using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace RAWSelectionAssistant.Services;
public sealed record MonitorProfileObservation(string Device,string State,string? ProfilePath,string? ProfileSha256,int NativeError,string PreviewConversion="NotAppliedByColorStudio",string Authority="GetICMProfileW display DC association; not calibration or WPF compositor verification");
/// <summary>Read-only Windows association probe. Never installs a profile, alters DPI or transforms pixels.</summary>
public static class WindowsMonitorProfileProbe
{
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public int Left,Top,Right,Bottom,WorkLeft,WorkTop,WorkRight,WorkBottom; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool GetMonitorInfoW(IntPtr monitor,ref MonitorInfo info);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateDCW(string driver,string device,IntPtr output,IntPtr init);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool GetICMProfileW(IntPtr dc,ref uint size,StringBuilder profile);
    public static MonitorProfileObservation ObserveWindow(IntPtr window)
    {
        if(window==IntPtr.Zero)return new("unknown","NoWindow",null,null,0);
        var monitor=MonitorFromWindow(window,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>(),Device=""};
        if(!GetMonitorInfoW(monitor,ref info))return new("unknown","MonitorQueryFailed",null,null,Marshal.GetLastWin32Error());
        return ObserveDevice(info.Device);
    }
    public static MonitorProfileObservation ObserveDevice(string device)
    {
        if(string.IsNullOrWhiteSpace(device)||device.Length>64)return new(device,"InvalidDevice",null,null,0);
        var dc=CreateDCW("DISPLAY",device,IntPtr.Zero,IntPtr.Zero);
        if(dc==IntPtr.Zero)return new(device,"DisplayDcUnavailable",null,null,Marshal.GetLastWin32Error());
        try
        {
            uint size=32768;var path=new StringBuilder((int)size);
            if(!GetICMProfileW(dc,ref size,path))return new(device,"ProfileQueryFailed",null,null,Marshal.GetLastWin32Error());
            return InspectAssociation(device,path.ToString());
        }
        finally{DeleteDC(dc);}
    }
    public static MonitorProfileObservation InspectAssociation(string device,string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return new(device,"NoAssociationReported",null,null,0);
        try
        {
            if(!Path.IsPathFullyQualified(path))return new(device,"InvalidAssociationPath",null,null,0);
            var full=Path.GetFullPath(path);using var file=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(file.Length is <128 or >16777216)return new(device,"InvalidProfile",full,null,0);
            Span<byte> header=stackalloc byte[128];file.ReadExactly(header);
            if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header)!=(uint)file.Length||!header.Slice(36,4).SequenceEqual("acsp"u8)||!header.Slice(16,4).SequenceEqual("RGB "u8))return new(device,"InvalidProfile",full,null,0);
            file.Position=0;var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file));
            return new(device,"AssociatedRgbProfileHeaderVerified",full,hash,0);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {return new(device,"ProfileUnreadable",path,null,e.HResult);}
    }
}
