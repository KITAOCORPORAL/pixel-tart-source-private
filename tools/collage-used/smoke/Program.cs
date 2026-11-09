using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;
using System.Runtime.InteropServices;
if(args.Length!=3)throw new ArgumentException("<package-root> <new-evidence-root> <exe|cmd>");
var package=Path.GetFullPath(args[0]);var evidence=Path.GetFullPath(args[1]);if(Directory.Exists(evidence))throw new IOException("Preserve previous smoke evidence");Directory.CreateDirectory(evidence);
var exe=Path.Combine(package,"app","KitaoPhotoSelector.exe");var before=Process.GetProcessesByName("KitaoPhotoSelector").Select(p=>p.Id).ToHashSet();
var si=new ProcessStartInfo {FileName=args[2]=="cmd"?"cmd.exe":exe,UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=package};if(args[2]=="cmd"){si.ArgumentList.Add("/d");si.ArgumentList.Add("/c");si.ArgumentList.Add(Path.Combine(package,"Start-Guangjing-Diagnostic.cmd"));}
si.Environment["PIXEL_TART_ISOLATED_RUNTIME"]="1";si.Environment["PIXEL_TART_ISOLATED_RUNTIME_ROOT"]=Path.Combine(evidence,"runtime");using var launcher=Process.Start(si)!;
Process? app=null;var until=DateTime.UtcNow.AddSeconds(45);
while(DateTime.UtcNow<until)
{
 foreach(var p in Process.GetProcessesByName("KitaoPhotoSelector"))if(!before.Contains(p.Id)&&string.Equals(p.MainModule?.FileName,exe,StringComparison.OrdinalIgnoreCase)){p.Refresh();if(p.MainWindowHandle!=IntPtr.Zero){try{var candidate=AutomationElement.FromHandle(p.MainWindowHandle);if(candidate.Current.Name.Contains("完整范围诊断",StringComparison.Ordinal)&&candidate.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"PrimaryNavigationWorkbench")) is not null){app=p;break;}}catch(ElementNotAvailableException){}}}
 if(app!=null)break;await Task.Delay(100);
}
if(app==null)throw new InvalidOperationException("New isolated published app did not create a window");
var root=AutomationElement.FromHandle(app.MainWindowHandle);var names=new List<object>();
foreach(var id in new[]{"PrimaryNavigationWorkbench","AssetLibraryNavigationButton","PrimaryNavigationWorkflow","PrimaryNavigationWorkCalendar","PrimaryNavigationPlanning","PrimaryNavigationTether","PrimaryNavigationReferenceColor","PrimaryNavigationOnlineSelection","PrimaryNavigationFinance","PrimaryNavigationHistory","PrimaryNavigationToolbox"})
{var e=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));if(e==null)throw new InvalidOperationException("Missing full-app navigation "+id);names.Add(new{id,name=e.Current.Name});}
var title=root.Current.Name;if(!title.Contains("完整范围诊断",StringComparison.Ordinal))throw new InvalidOperationException("Not the complete diagnostic shell");var pid=app.Id;var heldHandle=app.Handle;((WindowPattern)root.GetCurrentPattern(WindowPattern.Pattern)).Close();if(!app.WaitForExit(30000)||!launcher.WaitForExit(30000))throw new TimeoutException("Published app did not close normally");
if(!SmokeNative.GetExitCodeProcess(heldHandle,out var measuredAppExit))throw new InvalidOperationException("Cannot read actual app exit code");
var receipt=new{status="PUBLISHED_FULL_APP_NATIVE_UIA_SMOKE_PASS",mode=args[2],appProcessId=pid,launcherProcessId=launcher.Id,appExit=measuredAppExit,launcherExit=launcher.ExitCode,title,navigation=names,exe,exeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe))),runtime=si.Environment["PIXEL_TART_ISOLATED_RUNTIME_ROOT"],physicalMouse=false};
File.WriteAllText(Path.Combine(evidence,"SMOKE.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{mode=args[2],pid,appExit=measuredAppExit,launcherExit=launcher.ExitCode,navigation=names.Count}));if(measuredAppExit!=0||launcher.ExitCode!=0)Environment.Exit(1);

internal static class SmokeNative{[DllImport("kernel32.dll",SetLastError=true)]internal static extern bool GetExitCodeProcess(IntPtr process,out uint exitCode);}
