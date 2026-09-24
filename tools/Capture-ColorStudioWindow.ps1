param([Parameter(Mandatory=$true)][int]$TargetProcessId, [Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$Name='window', [long]$MainWindowHandle=0)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class StudioWindowCapture {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc p,IntPtr l);
 [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint command);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int count);
 public static string ClassName(IntPtr h){var name=new System.Text.StringBuilder(256);GetClassName(h,name,256);return name.ToString();}
 private delegate bool EnumProc(IntPtr h,IntPtr l);
 public static IntPtr[] Windows(uint pid) {
  var result=new List<IntPtr>();
  EnumWindows((h,l)=>{ GetWindowThreadProcessId(h,out uint p); if(p==pid && IsWindowVisible(h))result.Add(h);return true;},IntPtr.Zero);
  return result.ToArray();
 }
}
'@
[StudioWindowCapture]::SetProcessDPIAware() | Out-Null
$target=Get-Process -Id $TargetProcessId
if ($target.ProcessName -notmatch 'KitaoPhotoSelector|PixelTart') { throw 'Not a Pixel Tart process.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$records=@()
$windows=[StudioWindowCapture]::Windows($TargetProcessId)
$mainHandle=[IntPtr]::Zero; $largestArea=0
foreach($candidate in $windows) {
 $bounds=New-Object StudioWindowCapture+Rect
 if([StudioWindowCapture]::GetWindowRect($candidate,[ref]$bounds)) {
  $area=($bounds.Right-$bounds.Left)*($bounds.Bottom-$bounds.Top)
  if($area -gt $largestArea){$largestArea=$area;$mainHandle=$candidate}
 }
}
if($MainWindowHandle -ne 0){$mainHandle=[IntPtr]$MainWindowHandle}
foreach($handle in $windows) {
 $rect=New-Object StudioWindowCapture+Rect
 if(-not [StudioWindowCapture]::GetWindowRect($handle,[ref]$rect)){continue}
 $width=$rect.Right-$rect.Left; $height=$rect.Bottom-$rect.Top
 if($width -le 1 -or $height -le 1){continue}
 # Process.MainWindowHandle may point at an active WPF popup. Ownership is stable.
 $owner=[StudioWindowCapture]::GetWindow($handle,4)
 if($handle -ne $mainHandle -and [StudioWindowCapture]::ClassName($handle) -notlike 'HwndWrapper*'){continue} # Ignore IME/tool windows, retain WPF popups without an owner.
 $file=if($handle -eq $mainHandle){"$Name.png"}else{"$Name-popup-$($handle.ToInt64()).png"}
 $bitmap=New-Object System.Drawing.Bitmap $width,$height
 $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
 $dc=$graphics.GetHdc()
 try { if(-not [StudioWindowCapture]::PrintWindow($handle,$dc,2)){throw "PrintWindow failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"} }
 finally{$graphics.ReleaseHdc($dc)}
 try{$bitmap.Save((Join-Path $OutputDirectory $file),[System.Drawing.Imaging.ImageFormat]::Png)}
 finally{$graphics.Dispose();$bitmap.Dispose()}
 $records+=@{file=$file;HWND=$handle.ToInt64();Width=$width;Height=$height;CaptureMethod='PrintWindow(PW_RENDERFULLCONTENT)';CursorAccess=$false;CapturedAtUtc=[DateTime]::UtcNow.ToString('o');ProcessId=$TargetProcessId}
}
$records | ConvertTo-Json -Depth 4
