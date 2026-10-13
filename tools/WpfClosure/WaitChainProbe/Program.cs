using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
var process=Process.GetProcessById(int.Parse(args[0]));var rows=new List<object>();var session=Native.OpenThreadWaitChainSession(0,IntPtr.Zero);
if(session==IntPtr.Zero)throw new Exception("WCT session error "+Marshal.GetLastWin32Error());
try
{
 foreach(ProcessThread thread in process.Threads)
 {
  var buffer=Marshal.AllocHGlobal(16*280);
  try
  {
   uint count=16;var ok=Native.GetThreadWaitChain(session,IntPtr.Zero,1,(uint)thread.Id,ref count,buffer,out var cycle);var error=ok?0:Marshal.GetLastWin32Error();var nodes=new List<object>();
   if(ok)for(var i=0;i<count;i++)
   {
    var p=IntPtr.Add(buffer,i*280);var type=Marshal.ReadInt32(p);var status=Marshal.ReadInt32(p,4);
    nodes.Add(type==8?new{type,status,pid=Marshal.ReadInt32(p,8),tid=Marshal.ReadInt32(p,12),waitTime=Marshal.ReadInt32(p,16),contextSwitches=Marshal.ReadInt32(p,20)}:(object)new{type,status,objectName=Marshal.PtrToStringUni(IntPtr.Add(p,8),128)?.TrimEnd('\0')});
   }
   rows.Add(new{thread=thread.Id,threadHex=$"0x{thread.Id:X}",state=thread.ThreadState.ToString(),waitReason=thread.ThreadState==System.Diagnostics.ThreadState.Wait?thread.WaitReason.ToString():null,wctSucceeded=ok,error,cycle,nodes});
  }
  finally{Marshal.FreeHGlobal(buffer);}
 }
}
finally{Native.CloseThreadWaitChainSession(session);}
File.WriteAllText(args[1],JsonSerializer.Serialize(new{capturedUtc=DateTimeOffset.UtcNow,pid=process.Id,name=process.ProcessName,rows},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Captured {rows.Count} thread wait-chain snapshots for {process.Id}.");
static class Native
{
 [DllImport("advapi32.dll",SetLastError=true)]public static extern IntPtr OpenThreadWaitChainSession(uint flags,IntPtr callback);
 [DllImport("advapi32.dll")]public static extern void CloseThreadWaitChainSession(IntPtr handle);
 [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetThreadWaitChain(IntPtr handle,IntPtr context,uint flags,uint tid,ref uint count,IntPtr nodes,[MarshalAs(UnmanagedType.Bool)]out bool cycle);
}
