[CmdletBinding()]
param(
  [string]$RepoRoot = '',
  [int]$StabilityCount = 100,
  [int]$DragRepetitions = 3,
  [switch]$KeepApp
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
Set-Location $RepoRoot
$runId = 'native-capture-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$artifactRoot = Join-Path $RepoRoot 'artifacts\color-studio-native-capture'
$runRoot = Join-Path $artifactRoot $runId
$shotRoot = Join-Path $runRoot 'screenshots'
New-Item -ItemType Directory -Force -Path $shotRoot | Out-Null
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = (Join-Path $runRoot 'runtime')
New-Item -ItemType Directory -Force -Path $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT | Out-Null

$exe = Join-Path $RepoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.exe'
if (-not (Test-Path $exe)) { throw "Production executable missing: $exe" }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationFramework

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PixelTartNativeCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
  public const uint PW_CLIENTONLY=1, PW_RENDERFULLCONTENT=2, SRCCOPY=0x00CC0020, CAPTUREBLT=0x40000000, DWMWA_EXTENDED_FRAME_BOUNDS=9, DWMWA_CLOAKED=14, MONITOR_DEFAULTTONEAREST=2;
  public const int GWL_STYLE=-16, GWL_EXSTYLE=-20;
  [DllImport("user32.dll",SetLastError=true)] public static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool GetClientRect(IntPtr h,out RECT r);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool ClientToScreen(IntPtr h,ref POINT p);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr p);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int i);
  [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h,uint f);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool GetMonitorInfo(IntPtr h,ref MONITORINFO i);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,uint a,IntPtr v,uint s);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool repaint);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowDC(IntPtr h);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h,IntPtr dc);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int w,int h);
  [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst,int x,int y,int w,int h,IntPtr src,int sx,int sy,uint rop);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
  public static string Text(IntPtr h){var b=new StringBuilder(512);GetWindowText(h,b,b.Capacity);return b.ToString();}
  public static RECT WindowRect(IntPtr h){RECT r; if(!GetWindowRect(h,out r)) throw new Exception("GetWindowRect failed: "+Marshal.GetLastWin32Error()); return r;}
  public static RECT ExtendedRect(IntPtr h){RECT r=WindowRect(h);IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));try{if(DwmGetWindowAttribute(h,DWMWA_EXTENDED_FRAME_BOUNDS,p,(uint)Marshal.SizeOf(typeof(RECT)))==0)r=(RECT)Marshal.PtrToStructure(p,typeof(RECT));}finally{Marshal.FreeHGlobal(p);}return r;}
  public static RECT ClientScreenRect(IntPtr h){RECT c;if(!GetClientRect(h,out c))throw new Exception("GetClientRect failed");POINT p=new POINT();if(!ClientToScreen(h,ref p))throw new Exception("ClientToScreen failed");return new RECT{Left=p.X,Top=p.Y,Right=p.X+(c.Right-c.Left),Bottom=p.Y+(c.Bottom-c.Top)};}
  public static IntPtr CapturePrint(IntPtr h,uint flags,out int w,out int ht){RECT r=flags==PW_CLIENTONLY?ClientScreenRect(h):WindowRect(h);w=Math.Max(1,r.Right-r.Left);ht=Math.Max(1,r.Bottom-r.Top);IntPtr screen=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(screen),bmp=CreateCompatibleBitmap(screen,w,ht),old=SelectObject(mem,bmp);bool ok=PrintWindow(h,mem,flags);SelectObject(mem,old);DeleteDC(mem);ReleaseDC(IntPtr.Zero,screen);if(!ok){DeleteObject(bmp);return IntPtr.Zero;}return bmp;}
  public static IntPtr CaptureWindowBlt(IntPtr h,out int w,out int ht){RECT r=WindowRect(h);w=Math.Max(1,r.Right-r.Left);ht=Math.Max(1,r.Bottom-r.Top);IntPtr src=GetWindowDC(h),mem=CreateCompatibleDC(src),bmp=CreateCompatibleBitmap(src,w,ht),old=SelectObject(mem,bmp);bool ok=BitBlt(mem,0,0,w,ht,src,0,0,SRCCOPY|CAPTUREBLT);SelectObject(mem,old);DeleteDC(mem);ReleaseDC(h,src);if(!ok){DeleteObject(bmp);return IntPtr.Zero;}return bmp;}
  public static IntPtr CaptureScreen(IntPtr h,out int w,out int ht){RECT r=ExtendedRect(h);w=Math.Max(1,r.Right-r.Left);ht=Math.Max(1,r.Bottom-r.Top);IntPtr src=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(src),bmp=CreateCompatibleBitmap(src,w,ht),old=SelectObject(mem,bmp);bool ok=BitBlt(mem,0,0,w,ht,src,r.Left,r.Top,SRCCOPY|CAPTUREBLT);SelectObject(mem,old);DeleteDC(mem);ReleaseDC(IntPtr.Zero,src);if(!ok){DeleteObject(bmp);return IntPtr.Zero;}return bmp;}
  public static object Diagnostics(IntPtr h){RECT wr=WindowRect(h),cr=ClientScreenRect(h),er=ExtendedRect(h);MONITORINFO mi=new MONITORINFO();mi.cbSize=Marshal.SizeOf(typeof(MONITORINFO));IntPtr mon=MonitorFromWindow(h,MONITOR_DEFAULTTONEAREST);if(mon!=IntPtr.Zero)GetMonitorInfo(mon,ref mi);return new {WindowRect=wr,ClientRect=cr,ExtendedFrameRect=er,MonitorRect=mi.rcMonitor,WorkRect=mi.rcWork,IsWindow=IsWindow(h),IsVisible=IsWindowVisible(h),IsIconic=IsIconic(h),Foreground=GetForegroundWindow()==h,ProcessId=GetWindowThreadProcessId(h,IntPtr.Zero),Title=Text(h),Style=GetWindowLongPtr(h,GWL_STYLE).ToInt64(),ExtendedStyle=GetWindowLongPtr(h,GWL_EXSTYLE).ToInt64(),Dpi=GetDpiForWindow(h),Hwnd=h.ToInt64()};}
}
'@

function Get-RectObject($r) { [pscustomobject]@{ Left=[int]$r.Left; Top=[int]$r.Top; Right=[int]$r.Right; Bottom=[int]$r.Bottom; Width=[int]($r.Right-$r.Left); Height=[int]($r.Bottom-$r.Top) } }
function Save-HBitmap([IntPtr]$hBitmap,[string]$path) {
  if ($hBitmap -eq [IntPtr]::Zero) { return $null }
  try {
    $source=[System.Windows.Interop.Imaging]::CreateBitmapSourceFromHBitmap($hBitmap,[IntPtr]::Zero,[System.Windows.Int32Rect]::Empty,[System.Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
    $source.Freeze(); $enc=[System.Windows.Media.Imaging.PngBitmapEncoder]::new(); $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($source)); $fs=[System.IO.File]::Open($path,[System.IO.FileMode]::Create); try{$enc.Save($fs)}finally{$fs.Dispose()}; return $source
  } finally { [PixelTartNativeCapture]::DeleteObject($hBitmap) | Out-Null }
}
function Test-Capture([System.Windows.Media.Imaging.BitmapSource]$source,[string]$path,[string]$method,[bool]$apiSuccess,[string]$error='') {
  $metrics=[ordered]@{Method=$method;Path=$path;ApiSuccess=$apiSuccess;Error=$error;Valid=$false;Width=0;Height=0;NonZeroRatio=0;UniqueColors=0;LuminanceVariance=0;AlphaCoverage=0;DarkRatio=0;EdgeVariance=0;Sha256=$null}
  if ($null -eq $source) { $metrics.Reason='NO_BITMAP'; return [pscustomobject]$metrics }
  $bgra=New-Object System.Windows.Media.Imaging.FormatConvertedBitmap; $bgra.BeginInit(); $bgra.Source=$source; $bgra.DestinationFormat=[System.Windows.Media.PixelFormats]::Bgra32; $bgra.EndInit(); $bgra.Freeze(); $w=$bgra.PixelWidth; $h=$bgra.PixelHeight; $bytes=New-Object byte[] ($w*$h*4); $bgra.CopyPixels($bytes,$w*4,0)
  $metrics.Width=$w; $metrics.Height=$h; $count=$w*$h; $non=0L; $alpha=0L; $dark=0L; $sum=0.0; $sum2=0.0; $colors=[System.Collections.Generic.HashSet[int]]::new(); $edgeSum=0.0; $edgeN=0L
  for($i=0;$i -lt $count;$i++){ $b=$bytes[$i*4];$g=$bytes[$i*4+1];$r=$bytes[$i*4+2];$a=$bytes[$i*4+3]; if(($r+$g+$b)-gt 0){$non++};if($a -gt 0){$alpha++};if(($r+$g+$b)-lt 24){$dark++};$lum=.2126*$r+.7152*$g+.0722*$b;$sum+=$lum;$sum2+=$lum*$lum;[void]$colors.Add(($r -shl 16)-bor($g -shl 8)-bor $b);if($i -ge $w){$j=($i-$w)*4;$edgeSum += [math]::Abs($lum-(.2126*$bytes[$j+2]+.7152*$bytes[$j+1]+.0722*$bytes[$j]));$edgeN++};if(($i % $w) -gt 0){$j=($i-1)*4;$edgeSum += [math]::Abs($lum-(.2126*$bytes[$j+2]+.7152*$bytes[$j+1]+.0722*$bytes[$j]));$edgeN++} }
  $mean=$sum/[math]::Max(1,$count); $metrics.NonZeroRatio=$non/[double][math]::Max(1,$count);$metrics.UniqueColors=$colors.Count;$metrics.LuminanceVariance=[math]::Max(0,($sum2/[math]::Max(1,$count))-$mean*$mean);$metrics.AlphaCoverage=$alpha/[double][math]::Max(1,$count);$metrics.DarkRatio=$dark/[double][math]::Max(1,$count);$metrics.EdgeVariance=$edgeSum/[double][math]::Max(1,$edgeN);$metrics.Sha256=(Get-FileHash $path -Algorithm SHA256).Hash
  if($w -le 1 -or $h -le 1){$metrics.Reason='DEGENERATE_DIMENSIONS'}elseif($metrics.NonZeroRatio -lt .01){$metrics.Reason='BLANK_OR_TRANSPARENT'}elseif($metrics.UniqueColors -lt 8){$metrics.Reason='SINGLE_COLOR'}elseif($metrics.LuminanceVariance -lt 4 -and $metrics.EdgeVariance -lt 1){$metrics.Reason='FLAT_OR_BLACK'}else{$metrics.Valid=$true;$metrics.Reason='VALID'}
  return [pscustomobject]$metrics
}
function Capture-One([IntPtr]$hwnd,[string]$method,[string]$fileStem) {
  $path=Join-Path $shotRoot ($fileStem+'.png'); $w=0;$h=0;$bmp=[IntPtr]::Zero;$ok=$false;$err=''
  try { switch($method){'PRINTWINDOW_0'{$bmp=[PixelTartNativeCapture]::CapturePrint($hwnd,0,[ref]$w,[ref]$h)}'PRINTWINDOW_CLIENTONLY'{$bmp=[PixelTartNativeCapture]::CapturePrint($hwnd,1,[ref]$w,[ref]$h)}'PRINTWINDOW_FULLCONTENT'{$bmp=[PixelTartNativeCapture]::CapturePrint($hwnd,2,[ref]$w,[ref]$h)}'BITBLT_WINDOW'{$bmp=[PixelTartNativeCapture]::CaptureWindowBlt($hwnd,[ref]$w,[ref]$h)}'SCREEN_REGION_NATIVE'{$bmp=[PixelTartNativeCapture]::CaptureScreen($hwnd,[ref]$w,[ref]$h)}}; $ok=($bmp -ne [IntPtr]::Zero); if(-not $ok){$err='API returned false or bitmap handle was null'}; $src=Save-HBitmap $bmp $path; return Test-Capture $src $path $method $ok $err } catch { if($bmp -ne [IntPtr]::Zero){[PixelTartNativeCapture]::DeleteObject($bmp)|Out-Null}; return Test-Capture $null $path $method $false $_.Exception.Message }
}
function Request-NativeEvidence([string]$folder,[string]$label) {
  $nonce=[guid]::NewGuid().ToString();$request=Join-Path $folder 'native-observe-request.txt';$response=Join-Path $folder 'native-observe-response.json';Set-Content $request $nonce -Encoding ASCII;$deadline=(Get-Date).AddSeconds(3)
  while((Get-Date)-lt $deadline){if(Test-Path $response){try{$obj=Get-Content -Raw $response|ConvertFrom-Json;if($obj.Nonce -eq $nonce){$obj|Add-Member NoteProperty Label $label -Force;return $obj}}catch{}};Start-Sleep -Milliseconds 60};return [pscustomobject]@{Nonce=$nonce;Label=$label;Timeout=$true}
}
function Get-Element([string]$name,[string]$automationId=''){ $root=[System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$script:hwnd);$condition=if($automationId){New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$automationId)}else{New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)};return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition) }
function Get-Rect($e){$r=$e.Current.BoundingRectangle;[pscustomobject]@{X=[int]$r.X;Y=[int]$r.Y;W=[int]$r.Width;H=[int]$r.Height}}
function Get-NodeRows{$list=Get-Element '调整节点列表';if(-not $list){return @()};$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem);@($list.FindAll([System.Windows.Automation.TreeScope]::Children,$c))}
function Get-NodeNames{$rows=Get-NodeRows;@($rows|ForEach-Object{$n=$_.Current.Name;if($n -match 'Name = ([^,]+), Enabled'){$Matches[1].Trim()}else{$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text);@($_.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)|ForEach-Object{$_.Current.Name}|Where-Object{$_ -notin @('≡','◎','◌','≋','▣','⋯')}|Select-Object -First 1)}})}
function Native-Move($x,$y){[PixelTartNativeInput]::Move($x,$y)|Out-Null;Start-Sleep -Milliseconds 80}
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PixelTartNativeInput { [StructLayout(LayoutKind.Explicit,Size=40)] public struct INPUT{[FieldOffset(0)]public uint type;[FieldOffset(8)]public MOUSEINPUT mi;[FieldOffset(8)]public KEYBDINPUT ki;} [StructLayout(LayoutKind.Sequential)]public struct MOUSEINPUT{public int dx,dy;public uint mouseData,dwFlags,time;public IntPtr dwExtraInfo;} [StructLayout(LayoutKind.Sequential)]public struct KEYBDINPUT{public ushort wVk,wScan;public uint dwFlags,time;public IntPtr dwExtraInfo;} public const uint MOUSEEVENTF_LEFTDOWN=2,MOUSEEVENTF_LEFTUP=4,KEYEVENTF_KEYUP=2; [DllImport("user32.dll")]public static extern uint SendInput(uint n,INPUT[] p,int cb);[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);public static uint Move(int x,int y){SetCursorPos(x,y);return 1;}public static uint Mouse(uint f){var a=new INPUT[1];a[0].type=0;a[0].mi.dwFlags=f;return SendInput(1,a,Marshal.SizeOf(typeof(INPUT)));}public static uint Key(ushort k,bool up=false){var a=new INPUT[1];a[0].type=1;a[0].ki.wVk=k;a[0].ki.dwFlags=up?KEYEVENTF_KEYUP:0;return SendInput(1,a,Marshal.SizeOf(typeof(INPUT)));}}
'@

$started=[DateTime]::UtcNow; $p=Start-Process -FilePath $exe -ArgumentList '--acceptance-color-studio','--studio-scenario=01','--native-evidence-observer' -PassThru; $ready=Join-Path $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT 'ColorStudioFixture\ready.txt';$statePath=Join-Path $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT 'ColorStudioFixture\state.json';$deadline=(Get-Date).AddSeconds(45);while((Get-Date)-lt $deadline -and -not(Test-Path $ready)){Start-Sleep -Milliseconds 250};if(-not(Test-Path $statePath)){throw 'Production fixture did not become ready.'};$state=Get-Content -Raw $statePath|ConvertFrom-Json;$script:hwnd=[IntPtr][long]$state.MainWindowHandle
$diag=[PixelTartNativeCapture]::Diagnostics($script:hwnd);$dr=[ordered]@{};$diag.psobject.Properties|ForEach-Object{$dr[$_.Name]=$_.Value};$dr.ProductSourceSha=(git rev-parse HEAD).Trim();$dr.CapturedAtUtc=[DateTime]::UtcNow.ToString('o');$dr|ConvertTo-Json -Depth 8|Set-Content (Join-Path $runRoot 'WINDOW_DIAGNOSTICS.json') -Encoding UTF8
$work=$diag.WorkRect;$ww=[math]::Min(1700,[int]$work.Right-[int]$work.Left-100);$wh=[math]::Min(1100,[int]$work.Bottom-[int]$work.Top-100);[PixelTartNativeCapture]::ShowWindow($script:hwnd,9)|Out-Null;[PixelTartNativeCapture]::MoveWindow($script:hwnd,[int]$work.Left+50,[int]$work.Top+50,$ww,$wh,$true)|Out-Null;[PixelTartNativeCapture]::BringWindowToTop($script:hwnd)|Out-Null;[PixelTartNativeCapture]::SetForegroundWindow($script:hwnd)|Out-Null;Start-Sleep -Milliseconds 1000
$methods=@('PRINTWINDOW_0','PRINTWINDOW_CLIENTONLY','PRINTWINDOW_FULLCONTENT','BITBLT_WINDOW','SCREEN_REGION_NATIVE');$diagnosticCaptures=[System.Collections.Generic.List[object]]::new();$i=0;foreach($m in $methods){$i++;$diagnosticCaptures.Add((Capture-One $script:hwnd $m ('diagnostic_'+$i+'_'+$m)))};$selected=($diagnosticCaptures|Where-Object Valid|Select-Object -First 1).Method
$stable=[System.Collections.Generic.List[object]]::new();if($selected){for($i=1;$i -le $StabilityCount;$i++){$stable.Add((Capture-One $script:hwnd $selected ('stability_'+('{0:D3}' -f $i))))}};$counts=@{Valid=@($stable|Where-Object Valid).Count;Invalid=@($stable|Where-Object{$_ -and -not $_.Valid}).Count;Blank=@($stable|Where-Object{$_.Reason -eq 'BLANK_OR_TRANSPARENT'}).Count;Black=@($stable|Where-Object{$_.Reason -eq 'FLAT_OR_BLACK'}).Count;Stale=0;Exception=@($stable|Where-Object{$_.Reason -eq 'EXCEPTION'}).Count}
$manifest=[ordered]@{RunId=$runId;ProductSourceSha=(git rev-parse HEAD).Trim();WindowDiagnostics=$dr;CaptureDiagnosis=$diagnosticCaptures;SelectedCaptureMethod=$selected;Stability=$counts;StabilityTarget=$StabilityCount;NativeProductionWindow=($null -ne $selected);StartedAtUtc=$started.ToString('o');CompletedAtUtc=[DateTime]::UtcNow.ToString('o')};$manifest|ConvertTo-Json -Depth 15|Set-Content (Join-Path $runRoot 'COLOR_STUDIO_PHASE1_NATIVE_CAPTURE_MANIFEST.json') -Encoding UTF8
Write-Host "Capture run: $runRoot";Write-Host "Selected method: $selected";Write-Host "Stability: Valid=$($counts.Valid) Invalid=$($counts.Invalid) Blank=$($counts.Blank) Black=$($counts.Black)"
if(-not $KeepApp){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
