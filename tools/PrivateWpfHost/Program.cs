using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

if(args.Length<3)throw new ArgumentException("PrivateWpfHost absolutePython absoluteScript report.json");
var original=Native.GetProcessWindowStation();var name="KitaoTests_"+Guid.NewGuid().ToString("N");IntPtr station=IntPtr.Zero,desktop=IntPtr.Zero;Native.PI info=default;uint code=uint.MaxValue;
try
{
 station=Native.CreateWindowStation(name,0,0x000F037F,IntPtr.Zero);if(station==IntPtr.Zero)throw new Win32Exception();
 if(!Native.SetProcessWindowStation(station))throw new Win32Exception();
 desktop=Native.CreateDesktop("Isolated",IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(desktop==IntPtr.Zero)throw new Win32Exception();
 var si=new Native.SI{cb=Marshal.SizeOf<Native.SI>(),lpDesktop=name+"\\Isolated"};
 // Unique private window station also owns its own clipboard; never SwitchDesktop or inject input.
 if(!Native.CreateProcess(args[0],new StringBuilder("\""+args[0]+"\" \""+args[1]+"\" "+string.Join(" ",args.Skip(3).Select(v=>"\""+v.Replace("\"","\\\"")+"\""))),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(args[1]),ref si,out info))throw new Win32Exception();
 if(!Native.SetProcessWindowStation(original))throw new Win32Exception();
 Native.WaitForSingleObject(info.hProcess,0xFFFFFFFF);if(!Native.GetExitCodeProcess(info.hProcess,out code))throw new Win32Exception();
}
finally
{
 Native.SetProcessWindowStation(original);if(info.hThread!=IntPtr.Zero)Native.CloseHandle(info.hThread);if(info.hProcess!=IntPtr.Zero)Native.CloseHandle(info.hProcess);if(desktop!=IntPtr.Zero)Native.CloseDesktop(desktop);if(station!=IntPtr.Zero)Native.CloseWindowStation(station);
 File.WriteAllText(args[2],JsonSerializer.Serialize(new{station=name,desktop="Isolated",exitCode=code,interactiveStation=false,inputInjected=false,switchedInputDesktop=false,clipboard="privatewindowstation",completed=code!=uint.MaxValue},new JsonSerializerOptions{WriteIndented=true}));
}
Environment.ExitCode=unchecked((int)code);
internal static class Native
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SI{public int cb;public string? lpReserved,lpDesktop,lpTitle;public uint x,y,xs,ys,xc,yc,fill,flags;public ushort show,reserved;public IntPtr reserved2,hInput,hOutput,hError;}
 [StructLayout(LayoutKind.Sequential)]public struct PI{public IntPtr hProcess,hThread;public uint pid,tid;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr CreateWindowStation(string name,uint flags,uint access,IntPtr attributes);
 [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr GetProcessWindowStation();
 [DllImport("user32.dll",SetLastError=true)]public static extern bool SetProcessWindowStation(IntPtr station);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr attributes);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern bool CreateProcess(string app,StringBuilder command,IntPtr pAttr,IntPtr tAttr,bool inherit,uint flags,IntPtr env,string? cwd,ref SI si,out PI pi);
 [DllImport("kernel32.dll")]public static extern uint WaitForSingleObject(IntPtr handle,uint ms);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool GetExitCodeProcess(IntPtr h,out uint code);
 [DllImport("kernel32.dll")]public static extern bool CloseHandle(IntPtr h);
 [DllImport("user32.dll")]public static extern bool CloseDesktop(IntPtr h);
 [DllImport("user32.dll")]public static extern bool CloseWindowStation(IntPtr h);
}
