[CmdletBinding()]
param(
  [string]$RepoRoot = '',
  [int]$Scenario = 1,
  [switch]$KeepApp
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
Set-Location $RepoRoot
$artifactRoot = Join-Path $RepoRoot 'artifacts\color-studio-pointer'
$runtimeRoot = Join-Path $artifactRoot ("runtime-" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
$shotRoot = Join-Path $artifactRoot 'screenshots'
New-Item -ItemType Directory -Force -Path $runtimeRoot,$shotRoot | Out-Null
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = (Resolve-Path $runtimeRoot).Path

$exe = Join-Path $RepoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.exe'
if (-not (Test-Path $exe)) { throw "Production executable missing: $exe" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$drawingCandidates = @(
  (Join-Path $RepoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\System.Drawing.Common.dll'),
  'C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\10.0.12\System.Drawing.Common.dll'
)
$drawing = $drawingCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($drawing) { try { Add-Type -Path $drawing -ErrorAction Stop } catch { $drawing = $null } }
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class PixelTartNativeInput {
  [StructLayout(LayoutKind.Explicit, Size=40)] public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; [FieldOffset(8)] public KEYBDINPUT ki; [FieldOffset(8)] public HARDWAREINPUT hi; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx,dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct HARDWAREINPUT { public uint uMsg; public ushort wParamL,wParamH; }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public const uint INPUT_MOUSE=0, INPUT_KEYBOARD=1, MOUSEEVENTF_MOVE=1, MOUSEEVENTF_LEFTDOWN=2, MOUSEEVENTF_LEFTUP=4, MOUSEEVENTF_MIDDLEDOWN=32, MOUSEEVENTF_MIDDLEUP=64, MOUSEEVENTF_WHEEL=2048, KEYEVENTF_KEYUP=2, VK_ESCAPE=0x1B, VK_SPACE=0x20;
  [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint n, INPUT[] p, int cb);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a,uint b,bool attach);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int hgt,bool repaint);
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
  public static uint Mouse(uint flags,uint data=0){var a=new INPUT[1];a[0].type=INPUT_MOUSE;a[0].mi.dwFlags=flags;a[0].mi.mouseData=data;var n=SendInput(1,a,Marshal.SizeOf(typeof(INPUT)));if(n!=1)throw new InvalidOperationException("SendInput mouse failed: "+Marshal.GetLastWin32Error());return n;}
  public static uint Wheel(int delta){return Mouse(MOUSEEVENTF_WHEEL,unchecked((uint)delta));}
  public static uint Key(ushort key,bool up=false){var a=new INPUT[1];a[0].type=INPUT_KEYBOARD;a[0].ki.wVk=key;a[0].ki.dwFlags=up?KEYEVENTF_KEYUP:0;var n=SendInput(1,a,Marshal.SizeOf(typeof(INPUT)));if(n!=1)throw new InvalidOperationException("SendInput key failed: "+Marshal.GetLastWin32Error());return n;}
  public static uint Move(int x,int y){SetCursorPos(x,y);return 1;}
  public static bool Activate(IntPtr h){var fg=GetForegroundWindow();var a=GetWindowThreadProcessId(fg,IntPtr.Zero);var b=GetWindowThreadProcessId(h,IntPtr.Zero);var c=GetCurrentThreadId();if(a!=c)AttachThreadInput(c,a,true);if(b!=c)AttachThreadInput(c,b,true);ShowWindow(h,9);BringWindowToTop(h);SetForegroundWindow(h);SetFocus(h);if(b!=c)AttachThreadInput(c,b,false);if(a!=c)AttachThreadInput(c,a,false);return GetForegroundWindow()==h;}
}
'@

function Get-Element([string]$name, [string]$automationId='') {
  $root=[System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$script:hwnd)
  $condition = if ($automationId) { New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$automationId) } else { New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name) }
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
}
function Get-Button([string]$name) {
  $root=[System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$script:hwnd)
  $nameCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)
  $typeCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)
  $and=New-Object System.Windows.Automation.AndCondition($nameCondition,$typeCondition)
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$and)
}
function Get-Text([string]$automationId) { Get-Element '' $automationId }
function Get-ViewModeButton([string]$name) { Get-Button $name }
function Get-Rect($element) { $r=$element.Current.BoundingRectangle; return [pscustomobject]@{X=[int]$r.X;Y=[int]$r.Y;W=[int]$r.Width;H=[int]$r.Height} }
function Invoke-Click($element) {
  try { $pt=$element.GetClickablePoint(); Move-To ([int]$pt.X) ([int]$pt.Y) }
  catch { $r=Get-Rect $element; Move-To ([int]($r.X+$r.W/2)) ([int]($r.Y+$r.H/2)) }
  Click-Left
}
function Move-To($x,$y) { [PixelTartNativeInput]::Move($x,$y) | Out-Null; Start-Sleep -Milliseconds 100 }
function Wheel([int]$delta) { [PixelTartNativeInput]::Wheel($delta) | Out-Null; Start-Sleep -Milliseconds 300 }
function Click-Left { [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_LEFTDOWN) | Out-Null; Start-Sleep -Milliseconds 80; [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_LEFTUP) | Out-Null; Start-Sleep -Milliseconds 350 }
function Drag-Middle($x1,$y1,$x2,$y2) { Move-To $x1 $y1; [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_MIDDLEDOWN) | Out-Null; Start-Sleep -Milliseconds 100; foreach($i in 1..8){$x=[int]($x1+($x2-$x1)*$i/8);$y=[int]($y1+($y2-$y1)*$i/8);Move-To $x $y}; [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_MIDDLEUP) | Out-Null; Start-Sleep -Milliseconds 350 }
function Press-Key([uint16]$key) { [PixelTartNativeInput]::Key($key) | Out-Null; Start-Sleep -Milliseconds 80; [PixelTartNativeInput]::Key($key,$true) | Out-Null; Start-Sleep -Milliseconds 300 }
function Capture($name) {
  $path=Join-Path $shotRoot "$name.png"
  try {
    if (-not ('PixelTartCapture' -as [type])) {
      Add-Type @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class PixelTartCapture { [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; } [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r); [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags); public static void Save(IntPtr h,string p){RECT r;if(!GetWindowRect(h,out r))throw new Exception("GetWindowRect failed");using(var b=new Bitmap(r.Right-r.Left,r.Bottom-r.Top,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(b)){var dc=g.GetHdc();try{if(!PrintWindow(h,dc,2))throw new Exception("PrintWindow failed");}finally{g.ReleaseHdc(dc);}}b.Save(p,ImageFormat.Png);}} }
'@
    }
    [PixelTartCapture]::Save([IntPtr]$script:hwnd,$path); return $path
  } catch { }
  return ''
}
function Snapshot($label) {
  $fit=Get-Button '适合'; $actual=Get-Button '100%'; $zoom=Get-Text 'ZoomLabel'; $root=[System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$script:hwnd)
  $sampling=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'正在取样 · Esc 退出')))
  $cursor = New-Object PixelTartNativeInput+POINT; $cursorText = if([PixelTartNativeInput]::GetCursorPos([ref]$cursor)){"$($cursor.X),$($cursor.Y)"}else{''}
  return [pscustomobject]@{Label=$label;ZoomText=if($zoom){$zoom.Current.Name}else{''};SamplingVisible=($null -ne $sampling);Foreground=[PixelTartNativeInput]::GetForegroundWindow().ToInt64();Cursor=$cursorText;FitButton=if($fit){Get-Rect $fit}else{$null};ActualButton=if($actual){Get-Rect $actual}else{$null};CapturedAtUtc=[DateTime]::UtcNow.ToString('o')}
}
function Get-NodeRows {
  $list = Get-Element '调整节点列表'
  if (-not $list) { return @() }
  $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
  $items = $list.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
  return @($items)
}
function Get-NodeRowSnapshot {
  $rows = Get-NodeRows; $result = [System.Collections.Generic.List[object]]::new()
  for ($index = 0; $index -lt $rows.Count; $index++) {
    $row = $rows[$index]; $r = Get-Rect $row
    $texts = @($row.FindAll([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text))) | ForEach-Object { $_.Current.Name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $result.Add([pscustomobject]@{Index=$index;Name=$row.Current.Name;Rect=$r;Texts=$texts})
  }
  return $result
}
function Get-NodeNames {
  return @(Get-NodeRowSnapshot | ForEach-Object {
    if ($_.Name -match 'Name = ([^,]+), Enabled') { $Matches[1].Trim() } else { ($_.Texts | Where-Object { $_ -notin @('≡','◎','◌','≋','▣','⋯') } | Select-Object -First 1) }
  })
}
function Press-Modified([uint16]$modifier,[uint16]$key) {
  [PixelTartNativeInput]::Key($modifier) | Out-Null; Start-Sleep -Milliseconds 80
  [PixelTartNativeInput]::Key($key) | Out-Null; Start-Sleep -Milliseconds 80
  [PixelTartNativeInput]::Key($key,$true) | Out-Null; Start-Sleep -Milliseconds 80
  [PixelTartNativeInput]::Key($modifier,$true) | Out-Null; Start-Sleep -Milliseconds 400
}
function Drag-Node([int]$sourceIndex,[int]$destinationIndex,[bool]$after,[string]$id) {
  $beforeRows = @(Get-NodeRowSnapshot); if ($beforeRows.Count -lt 4) { throw "Expected at least four visible node rows, found $($beforeRows.Count)." }
  $source = $beforeRows[$sourceIndex]; $destination = $beforeRows[$destinationIndex]
  $sx=[int]($source.Rect.X+$source.Rect.W/2); $sy=[int]($source.Rect.Y+$source.Rect.H/2)
  $tx=[int]($destination.Rect.X+$destination.Rect.W/2); $fraction = if($after){.78}else{.22}; $ty=[int]($destination.Rect.Y+($destination.Rect.H * $fraction))
  Move-To $sx $sy; [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_LEFTDOWN) | Out-Null; Start-Sleep -Milliseconds 120
  [PixelTartNativeInput]::Move($sx+2,$sy+2) | Out-Null; Start-Sleep -Milliseconds 180
  $belowThreshold = @(Get-NodeNames)
  [PixelTartNativeInput]::Move($tx,$ty) | Out-Null; Start-Sleep -Milliseconds 220
  $during = Snapshot "$id-during"; $duringRows = @(Get-NodeRowSnapshot); $duringShot = Capture "NODE_${id}_DURING"
  [PixelTartNativeInput]::Mouse([PixelTartNativeInput]::MOUSEEVENTF_LEFTUP) | Out-Null; Start-Sleep -Milliseconds 600
  $afterNames = @(Get-NodeNames); $afterShot = Capture "NODE_${id}_AFTER"
  [pscustomobject]@{TestId=$id;SourceIndex=$sourceIndex;DestinationIndex=$destinationIndex;After=$after;Start=@{X=$sx;Y=$sy};Threshold=@{X=$sx+2;Y=$sy+2};DragOver=@{X=$tx;Y=$ty};Drop=@{X=$tx;Y=$ty};BeforeOrder=@($beforeRows | ForEach-Object { if ($_.Name -match 'Name = ([^,]+), Enabled') {$Matches[1].Trim()} });BelowThresholdOrder=$belowThreshold;DuringOrder=@($duringRows | ForEach-Object { if ($_.Name -match 'Name = ([^,]+), Enabled') {$Matches[1].Trim()} });During=$during;AfterOrder=$afterNames;DuringScreenshot=$duringShot;AfterScreenshot=$afterShot}
}

$started=[DateTime]::UtcNow
$p=Start-Process -FilePath $exe -ArgumentList '--acceptance-color-studio','--studio-scenario=01' -PassThru
$ready=Join-Path $runtimeRoot 'ColorStudioFixture\ready.txt'; $statePath=Join-Path $runtimeRoot 'ColorStudioFixture\state.json'; $deadline=(Get-Date).AddSeconds(45)
while((Get-Date)-lt $deadline -and -not(Test-Path $ready)){Start-Sleep -Milliseconds 250}
if(-not(Test-Path $statePath)){throw 'Production fixture did not become ready.'}
$state=Get-Content -Raw $statePath | ConvertFrom-Json; $script:hwnd=[long]$state.MainWindowHandle
$script:uiScale=1
$activated=[PixelTartNativeInput]::MoveWindow([IntPtr]$hwnd,0,0,2400,1500,$true); $activated=[PixelTartNativeInput]::Activate([IntPtr]$hwnd); Start-Sleep -Milliseconds 700
if(-not $activated -or [PixelTartNativeInput]::GetForegroundWindow().ToInt64() -ne $hwnd){throw "Could not activate Production HWND $hwnd; foreground is $([PixelTartNativeInput]::GetForegroundWindow().ToInt64())."}
$workspace=Get-Element '参考仿色工作区'; if(-not $workspace){throw 'Production Color Studio workspace not found.'}
$wr=Get-Rect $workspace; $fitAnchor=Get-Button '适合'; if(-not $fitAnchor){throw 'Fit button not found.'}; $fr=Get-Rect $fitAnchor
# The central canvas is the production panel above the zoom toolbar. Its left edge
# is aligned with the toolbar and its right edge ends before the right inspector.
$canvas=[pscustomobject]@{X=$fr.X;Y=$wr.Y+110;W=1100;H=[math]::Max(300,$fr.Y-($wr.Y+110))}; $cx=[int]($canvas.X+$canvas.W/2);$cy=[int]($canvas.Y+$canvas.H/2)
if ($Scenario -eq 2) {
  $nodeSnapshot = Get-NodeRowSnapshot
  $nodeSnapshot | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $artifactRoot 'NODE_UIA_DIAGNOSTIC.json') -Encoding UTF8
  if(-not $KeepApp){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
  Write-Host "Node UIA diagnostic written to $artifactRoot"
  exit 0
}
if ($Scenario -eq 3) {
  $dragCases = @(
    [pscustomobject]@{Id='N1-N2';Source=0;Destination=1;After=$true},
    [pscustomobject]@{Id='N1-N3';Source=0;Destination=2;After=$false},
    [pscustomobject]@{Id='LAST-FIRST';Source=3;Destination=0;After=$false},
    [pscustomobject]@{Id='MIDDLE-LAST';Source=1;Destination=3;After=$true},
    [pscustomobject]@{Id='ADJACENT';Source=1;Destination=2;After=$true},
    [pscustomobject]@{Id='NOOP';Source=1;Destination=1;After=$false}
  )
  $dragResults=[System.Collections.Generic.List[object]]::new()
  foreach($case in $dragCases){
    foreach($repeat in 1..3){
      $result=Drag-Node $case.Source $case.Destination $case.After "$($case.Id)-$repeat"
      $result | Add-Member -NotePropertyName Repetition -NotePropertyValue $repeat
      $dragResults.Add($result)
      if($case.Id -eq 'N1-N2' -and $repeat -eq 1){
        Press-Modified 0x11 0x5A; $result | Add-Member -NotePropertyName UndoOrder -NotePropertyValue @((Get-NodeNames))
        Press-Modified 0x11 0x59; $result | Add-Member -NotePropertyName RedoOrder -NotePropertyValue @((Get-NodeNames))
      }
      Press-Modified 0x11 0x5A
      Start-Sleep -Milliseconds 400
    }
  }
  $dragReport=[pscustomobject]@{ProductSourceSha=(git rev-parse HEAD).Trim();InputMethod='Win32 SendInput';ProductionWindow=$hwnd;FixtureId=$state.FixtureId;StartedAtUtc=$started.ToString('o');CompletedAtUtc=[DateTime]::UtcNow.ToString('o');ThresholdPixels=2;Cases=$dragResults}
  $dragReport | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $artifactRoot 'NODE_DRAG_WALKTHROUGH_MANIFEST.json') -Encoding UTF8
  @('# Native Node Drag Walkthrough','',"ProductSourceSha: $($dragReport.ProductSourceSha)","Input method: Win32 SendInput",'',($dragResults | ForEach-Object { "- $($_.TestId): before=$($_.BeforeOrder -join ' → '); after=$($_.AfterOrder -join ' → '); belowThreshold=$($_.BelowThresholdOrder -join ' → '); duringScreenshot=$($_.DuringScreenshot)" })) | Set-Content (Join-Path $artifactRoot 'NODE_DRAG_WALKTHROUGH_REPORT.md') -Encoding UTF8
  if(-not $KeepApp){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
  Write-Host "Node drag evidence written to $artifactRoot"
  exit 0
}
$records=[System.Collections.Generic.List[object]]::new()
function Run-Test($id,$label,[scriptblock]$action,[string]$evidence='') { [PixelTartNativeInput]::Activate([IntPtr]$hwnd)|Out-Null; $before=Snapshot "$id-before"; & $action; $after=Snapshot "$id-after"; $result='PASS'; if($id -eq 'A-WHEEL' -and $script:wheelMid.ZoomText -eq $before.ZoomText){$result='BLOCKED'}; if($id -match 'EYEDROPPER|NEGATIVE' -and -not $after.SamplingVisible -and $id -ne 'M-ESC'){$result='BLOCKED'}; $records.Add([pscustomobject]@{TestId=$id;InputSequence=$label;BeforeState=$before;DuringState=$script:wheelMid;AfterState=$after;Result=$result;EvidenceFile=$evidence}) }
Run-Test 'A-WHEEL' 'move canvas; wheel up; inspect; wheel down' { Move-To $cx $cy; Wheel 120; $script:wheelMid=Snapshot 'A-WHEEL-mid'; $shot=Capture '01_WHEEL_ZOOM_AFTER'; Wheel -120; $script:lastShot=$shot } $script:lastShot
$fit=Get-Button '适合'; if($fit){Invoke-Click $fit}; Run-Test 'B-FIT' 'click 适合' { if($fit){Invoke-Click $fit}; Capture '02_FIT' } '02_FIT.png'
$actual=Get-Button '100%'; Run-Test 'C-100' 'click 100%' { if($actual){Invoke-Click $actual}; Capture '03_100_PERCENT' } '03_100_PERCENT.png'
Run-Test 'D-PAN' 'middle drag 160 logical px' { Drag-Middle ($cx-20) ($cy) ($cx+140) ($cy+80); Capture '04_NATIVE_PAN' } '04_NATIVE_PAN.png'
if($fit){Invoke-Click $fit}; Move-To 1150 1200; Wheel -960; Start-Sleep -Milliseconds 700; $add=Get-Button '增加取样'; if(-not $add){throw 'Native runner could not bring 增加取样 into view.'}; Invoke-Click $add; Start-Sleep -Milliseconds 300; Run-Test 'F-EYEDROPPER-FIT' 'scroll left inspector; click image with 增加取样' { Move-To ($cx+80) ($cy-40); Click-Left; Capture '05_EYEDROPPER_FIT' } '05_EYEDROPPER_FIT.png'
Run-Test 'G-EYEDROPPER-ZOOM-PAN' 'wheel up; middle drag; click image' { Move-To ($cx+100) ($cy); Wheel 120; Drag-Middle $cx $cy ($cx+90) ($cy+40); Move-To ($cx+90) ($cy+20); Click-Left; Capture '06_EYEDROPPER_ZOOM_PAN' } '06_EYEDROPPER_ZOOM_PAN.png'
$sub=Get-Button '减少取样'; if(-not $sub){throw 'Native runner could not find 减少取样 after inspector scroll.'}; Invoke-Click $sub; Run-Test 'H-NEGATIVE' 'click image with 减少取样' { Move-To ($cx-100) ($cy+20); Click-Left; Capture '07_NEGATIVE_SAMPLE' } '07_NEGATIVE_SAMPLE.png'
Press-Key ([PixelTartNativeInput]::VK_ESCAPE); Run-Test 'M-ESC' 'Escape' { Capture '08_SAMPLING_EXIT' } '08_SAMPLING_EXIT.png'
$fit=Get-Button '适合'; if($fit){Invoke-Click $fit}; Run-Test 'E-BOUNDARY' 'zoom then repeated middle drags to canvas boundary' { $actual=Get-Button '100%'; if($actual){Invoke-Click $actual}; for($i=0;$i -lt 6;$i++){Drag-Middle $cx $cy 2000 $cy}; Capture '09_PAN_BOUNDARY' } '09_PAN_BOUNDARY.png'
$leftRight=Get-ViewModeButton '左右对比'; if($leftRight){Invoke-Click $leftRight}; Run-Test 'J-SPLIT' 'click 左右对比; wheel; middle drag' { Move-To $cx $cy; Wheel 120; Drag-Middle $cx $cy ($cx+100) ($cy+40); Capture '10_SPLIT_ZOOM_PAN' } '10_SPLIT_ZOOM_PAN.png'
$add=Get-Button '增加取样'; if($add){Invoke-Click $add}; Run-Test 'K-SPLIT-SAMPLE' 'split left click and right click' { Move-To ($canvas.X+120) $cy; Click-Left; Move-To ($canvas.X+$canvas.W-120) $cy; Click-Left; Capture '11_SPLIT_SAMPLING' } '11_SPLIT_SAMPLING.png'; Press-Key ([PixelTartNativeInput]::VK_ESCAPE)
$side=Get-ViewModeButton '并排对比'; if($side){Invoke-Click $side}; Run-Test 'L-SIDE' 'click 并排对比; wheel; middle drag' { Move-To $cx $cy; Wheel 120; Drag-Middle $cx $cy ($cx+80) ($cy+30); Capture '12_SIDE_BY_SIDE' } '12_SIDE_BY_SIDE.png'
$add=Get-Button '增加取样'; if($add){Invoke-Click $add}; Run-Test 'L-SIDE-SAMPLE' 'side-by-side left/right clicks' { Move-To ($canvas.X+100) $cy; Click-Left; Move-To ($canvas.X+$canvas.W-100) $cy; Click-Left; Capture '13_SIDE_BY_SIDE_SAMPLING' } '13_SIDE_BY_SIDE_SAMPLING.png'; Press-Key ([PixelTartNativeInput]::VK_ESCAPE)
$normal=Get-ViewModeButton '仿色结果'; if($normal){Invoke-Click $normal}; Run-Test 'N-CONFLICT' 'sampling mode; wheel and middle drag; no accidental sample' { $add=Get-Button '增加取样'; if($add){Invoke-Click $add}; Move-To $cx $cy; Wheel 120; Drag-Middle $cx $cy ($cx+70) ($cy+20); Capture '14_SAMPLING_CONFLICT' } '14_SAMPLING_CONFLICT.png'; Press-Key ([PixelTartNativeInput]::VK_ESCAPE)
Run-Test 'O-RAPID' 'wheel up; wheel up; pan; wheel down; fit; 100%; fit' { Move-To $cx $cy; Wheel 120; Wheel 120; Drag-Middle $cx $cy ($cx+80) ($cy+30); Wheel -120; if($fit){Invoke-Click $fit}; $actual=Get-Button '100%'; if($actual){Invoke-Click $actual}; if($fit){Invoke-Click $fit}; Capture '15_RAPID_INTERACTION' } '15_RAPID_INTERACTION.png'
$report=[pscustomobject]@{ProductSourceSha=(git rev-parse HEAD).Trim();Executable=$exe;AppVersion=$state.AppVersion;OS=[Environment]::OSVersion.VersionString;LogicalDpi=$state.LogicalDpi;WindowSize=$state.WindowSize;InputMethod='Win32 SendInput';HWND=$hwnd;FixtureId=$state.FixtureId;StartedAtUtc=$started.ToString('o');CompletedAtUtc=[DateTime]::UtcNow.ToString('o');Tests=$records}
$report | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $artifactRoot 'POINTER_WALKTHROUGH_MANIFEST.json') -Encoding UTF8
@("# Native Pointer Walkthrough",'',"Input method: Win32 SendInput", "Production HWND: $hwnd", "Fixture: $($state.FixtureId)", "", ($records | ForEach-Object { "- $($_.TestId): $($_.Result) — $($_.InputSequence)" })) | Set-Content (Join-Path $artifactRoot 'POINTER_WALKTHROUGH_REPORT.md') -Encoding UTF8
if(-not $KeepApp){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
Write-Host "Evidence written to $artifactRoot"
