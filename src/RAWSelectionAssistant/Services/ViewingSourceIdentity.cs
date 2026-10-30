using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace RAWSelectionAssistant.Services;
/// <summary>Handle-bound identity. NTFS ChangeTime catches same-length edits with restored LastWriteTime.
/// Unknown/non-Windows identity never permits hash reuse. A read lease blocks writers during verification/render.</summary>
internal sealed class ViewingSourceLease:IDisposable
{
 internal readonly record struct Stamp(string Path,long Length,long Write,long Change,uint Volume,ulong FileId);
 public FileStream Stream{get;} public Stamp? Identity{get;}
 public ViewingSourceLease(string path){Stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.Asynchronous|FileOptions.SequentialScan);Identity=Read(path,Stream.SafeFileHandle,Stream.Length);}
 static Stamp? Read(string path,SafeFileHandle handle,long length){if(!OperatingSystem.IsWindows())return null;var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);if(drive.DriveType!=DriveType.Fixed||drive.DriveFormat is not("NTFS" or "ReFS"))return null;if(!GetFileInformationByHandleEx(handle,0,out Basic b,(uint)Marshal.SizeOf<Basic>())||!GetFileInformationByHandle(handle,out Info i))return null;if(b.Change<=0)return null;return new(Path.GetFullPath(path),length,b.Write,b.Change,i.Volume,((ulong)i.IndexHigh<<32)|i.IndexLow);}
 [StructLayout(LayoutKind.Sequential)]struct Basic{public long Creation,Access,Write,Change;public uint Attributes;}
 [StructLayout(LayoutKind.Sequential)]struct Info{public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int kind,out Basic info,uint bytes);
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
 public void Dispose()=>Stream.Dispose();
}
