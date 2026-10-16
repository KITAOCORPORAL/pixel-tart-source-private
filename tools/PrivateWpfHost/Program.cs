using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

if(args.Length<3)throw new ArgumentException("PrivateWpfHost absolutePython absoluteScript report.json [script arguments]");
int timeoutSeconds=10800;
if(Environment.GetEnvironmentVariable("KITAO_PRIVATE_HOST_TIMEOUT_SECONDS") is { } configured && (!int.TryParse(configured,out timeoutSeconds)||timeoutSeconds<1||timeoutSeconds>21600))throw new ArgumentOutOfRangeException("KITAO_PRIVATE_HOST_TIMEOUT_SECONDS","Allowed1..21600seconds");
var clock=Stopwatch.StartNew();var events=new List<object>();
void Record(string stage,object? detail=null){events.Add(new{atUtc=DateTimeOffset.UtcNow,elapsedMs=clock.Elapsed.TotalMilliseconds,stage,detail});File.WriteAllText(args[2],JsonSerializer.Serialize(new{status="RUNNING",timeoutSeconds,events},new JsonSerializerOptions{WriteIndented=true}));}
var original=Native.GetProcessWindowStation();var name="KitaoTests_"+Guid.NewGuid().ToString("N");IntPtr station=IntPtr.Zero,desktop=IntPtr.Zero,job=IntPtr.Zero;Native.PI info=default;uint code=uint.MaxValue;bool timedOut=false,assigned=false;string? failure=null;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
try
{
 Record("start",new{station=name,executable=args[0],script=args[1]});
 job=Native.CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new Win32Exception();
 var limits=new Native.JobLimits();limits.Basic.LimitFlags=0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
 if(!Native.SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<Native.JobLimits>()))throw new Win32Exception();
 station=Native.CreateWindowStation(name,0,0x000F037F,IntPtr.Zero);if(station==IntPtr.Zero)throw new Win32Exception();
 if(!Native.SetProcessWindowStation(station))throw new Win32Exception();
 desktop=Native.CreateDesktop("Isolated",IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(desktop==IntPtr.Zero)throw new Win32Exception();
 var si=new Native.SI{cb=Marshal.SizeOf<Native.SI>(),lpDesktop=name+"\\Isolated"};
 var command=new StringBuilder(Quote(args[0])+" "+Quote(args[1])+" "+string.Join(" ",args.Skip(3).Select(Quote)));
 // Suspended startup closes the race: no descendant can escape before job assignment.
 if(!Native.CreateProcess(args[0],command,IntPtr.Zero,IntPtr.Zero,false,0x08000004,IntPtr.Zero,Path.GetDirectoryName(args[1]),ref si,out info))throw new Win32Exception();
 if(!Native.AssignProcessToJobObject(job,info.hProcess))throw new Win32Exception();assigned=true;
 if(Native.ResumeThread(info.hThread)==uint.MaxValue)throw new Win32Exception();
 if(!Native.SetProcessWindowStation(original))throw new Win32Exception();Record("child-resumed",new{info.pid,jobAssigned=assigned});
 uint wait=Native.WaitForSingleObject(info.hProcess,checked((uint)timeoutSeconds*1000));
 if(wait==258){timedOut=true;code=124;Record("timeout",new{info.pid,timeoutSeconds});if(!Native.TerminateJobObject(job,124))throw new Win32Exception();uint cleanupWait=Native.WaitForSingleObject(info.hProcess,10000);Record("job-termination",new{cleanupWait,wholeOwnedTree=true});}
 else if(wait!=0)throw new Win32Exception();
 else if(!Native.GetExitCodeProcess(info.hProcess,out code))throw new Win32Exception();
 Record("child-exited",new{code});
}
catch(Exception e){failure=e.ToString();if(code==uint.MaxValue)code=125;Record("host-error",failure);}
finally
{
 Native.SetProcessWindowStation(original);
 if(job!=IntPtr.Zero){Native.CloseHandle(job);job=IntPtr.Zero;events.Add(new{stage="owned-job-closed",killRemainingOwnedDescendants=true});}
 if(info.hProcess!=IntPtr.Zero&&!assigned){Native.TerminateProcess(info.hProcess,125);Native.WaitForSingleObject(info.hProcess,10000);}
 if(info.hThread!=IntPtr.Zero)Native.CloseHandle(info.hThread);if(info.hProcess!=IntPtr.Zero)Native.CloseHandle(info.hProcess);
 if(desktop!=IntPtr.Zero)Native.CloseDesktop(desktop);if(station!=IntPtr.Zero)Native.CloseWindowStation(station);
 File.WriteAllText(args[2],JsonSerializer.Serialize(new{station=name,desktop="Isolated",exitCode=code,timeoutSeconds,timedOut,failure,jobAssigned=assigned,interactiveStation=false,inputInjected=false,switchedInputDesktop=false,clipboard="privatewindowstation",completed=code!=uint.MaxValue,events},new JsonSerializerOptions{WriteIndented=true}));
}
Environment.ExitCode=unchecked((int)code);
static string Quote(string s){var b=new StringBuilder("\"");int slashes=0;foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}b.Append('\\',slashes).Append(c);slashes=0;}return b.Append('\\',slashes*2).Append('\"').ToString();}
internal static class Native
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SI{public int cb;public string? lpReserved,lpDesktop,lpTitle;public uint x,y,xs,ys,xc,yc,fill,flags;public ushort show,reserved;public IntPtr reserved2,hInput,hOutput,hError;}
 [StructLayout(LayoutKind.Sequential)]public struct PI{public IntPtr hProcess,hThread;public uint pid,tid;}
 [StructLayout(LayoutKind.Sequential)]public struct BasicLimits{public long ProcessTime,JobTime;public uint LimitFlags;public UIntPtr MinWs,MaxWs;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)]public struct IoCounters{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)]public struct JobLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr CreateJobObject(IntPtr a,string? name);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool SetInformationJobObject(IntPtr j,int kind,ref JobLimits limits,uint length);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool AssignProcessToJobObject(IntPtr j,IntPtr p);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool TerminateJobObject(IntPtr j,uint code);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool TerminateProcess(IntPtr p,uint code);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern uint ResumeThread(IntPtr thread);
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
