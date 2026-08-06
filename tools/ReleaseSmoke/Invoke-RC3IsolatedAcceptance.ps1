param(
    [ValidateSet('Fresh','Upgrade','Both')][string]$Mode='Both',
    [string]$Rc2Installer='',
    [string]$Rc3Installer='',
    [string]$EvidenceRoot=''
)

$ErrorActionPreference='Stop'
$repoRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$installerRoot=Join-Path $repoRoot 'artifacts\releases\2.3.0\installer'
if([string]::IsNullOrWhiteSpace($Rc2Installer)){$Rc2Installer=(Get-ChildItem $installerRoot -Filter '*Setup_2.3.0_RC2_x64.exe' -File|Select-Object -First 1).FullName}
if([string]::IsNullOrWhiteSpace($Rc3Installer)){$Rc3Installer=(Get-ChildItem $installerRoot -Filter '*Setup_2.3.0_RC3_x64.exe' -File|Select-Object -First 1).FullName}
if([string]::IsNullOrWhiteSpace($EvidenceRoot)){$EvidenceRoot=Join-Path $repoRoot 'artifacts\diagnostics\2.3.0\rc3-isolated-acceptance'}
New-Item -ItemType Directory -Force -Path $EvidenceRoot|Out-Null
$workspaceDotnet=Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
$dotnet=if(Test-Path $workspaceDotnet){$workspaceDotnet}else{(Get-Command dotnet -ErrorAction Stop).Source}

Add-Type @'
using System;using System.ComponentModel;using System.Runtime.InteropServices;using System.Text;
public static class PixelTartRc3Desktop{[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct STARTUPINFO{public int cb;public string lpReserved;public string lpDesktop;public string lpTitle;public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;public short wShowWindow,cbReserved2;public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;}[StructLayout(LayoutKind.Sequential)]public struct PROCESS_INFORMATION{public IntPtr hProcess,hThread;public int dwProcessId,dwThreadId;}[DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktopW(string n,string d,IntPtr m,uint f,uint a,IntPtr s);[DllImport("user32.dll")]static extern bool CloseDesktop(IntPtr d);[DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessW(string a,StringBuilder c,IntPtr pa,IntPtr ta,bool i,uint f,IntPtr e,string w,ref STARTUPINFO s,out PROCESS_INFORMATION p);[DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint m);[DllImport("kernel32.dll")]static extern bool GetExitCodeProcess(IntPtr h,out uint c);[DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);public static uint Run(string n,string c,string w){var d=CreateDesktopW(n,null,IntPtr.Zero,0,0x10000000,IntPtr.Zero);if(d==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());try{var s=new STARTUPINFO{cb=Marshal.SizeOf<STARTUPINFO>(),lpDesktop=n};PROCESS_INFORMATION p;if(!CreateProcessW(null,new StringBuilder(c),IntPtr.Zero,IntPtr.Zero,false,0x200,IntPtr.Zero,w,ref s,out p))throw new Win32Exception(Marshal.GetLastWin32Error());try{WaitForSingleObject(p.hProcess,0xffffffff);uint code;GetExitCodeProcess(p.hProcess,out code);return code;}finally{CloseHandle(p.hThread);CloseHandle(p.hProcess);}}finally{CloseDesktop(d);}}}
'@

function Invoke-IsolatedRun([string]$RunMode){
    $runId=[Guid]::NewGuid().ToString('N')
    $runRoot=Join-Path $EvidenceRoot ($RunMode.ToLowerInvariant()+'-'+$runId)
    $localAppData=Join-Path $runRoot 'local-appdata'
    $installRoot=Join-Path $runRoot 'installed'
    $sourceRoot=Join-Path $runRoot 'source'
    $watchRoot=Join-Path $runRoot 'watch-folder'
    New-Item -ItemType Directory -Force -Path $localAppData,$sourceRoot,$watchRoot|Out-Null
    $sourceFile=Join-Path $sourceRoot 'isolated-source.txt'
    [IO.File]::WriteAllText($sourceFile,'pixel-tart-rc3-isolated-source',[Text.UTF8Encoding]::new($false))
    $context=[ordered]@{
        Mode=$RunMode;RunId=$runId;RepoRoot=$repoRoot;EvidenceRoot=$runRoot;LocalAppData=$localAppData;InstallRoot=$installRoot
        SourceFile=$sourceFile;WatchRoot=$watchRoot;Rc2Installer=[IO.Path]::GetFullPath($Rc2Installer);Rc3Installer=[IO.Path]::GetFullPath($Rc3Installer);DotnetPath=$dotnet
    }
    $contextPath=Join-Path $runRoot 'context.json'
    [IO.File]::WriteAllText($contextPath,($context|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($true))
    $worker=Join-Path $PSScriptRoot 'Invoke-RC3IsolatedWorker.ps1'
    $powershell=Join-Path $PSHOME 'powershell.exe'
    $command=$powershell+' -NoProfile -ExecutionPolicy Bypass -File "'+$worker+'" -ContextPath "'+$contextPath+'"'
    $exit=[PixelTartRc3Desktop]::Run("PixelTartRc3_${RunMode}_$runId",$command,$repoRoot)
    $resultPath=Join-Path $runRoot 'result.json'
    if(-not(Test-Path $resultPath)){throw "$RunMode isolated worker exited $exit without a result."}
    Copy-Item $resultPath (Join-Path $EvidenceRoot ($RunMode.ToLowerInvariant()+'-latest-result.json')) -Force
    $result=Get-Content $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
    if($exit-ne0 -or -not$result.Passed){throw "$RunMode isolated acceptance failed: $($result.Error)"}
    $result
}

$summary=[ordered]@{StartedAt=[DateTimeOffset]::Now.ToString('O');Fresh=$null;Upgrade=$null}
if($Mode -in @('Fresh','Both')){$summary.Fresh=Invoke-IsolatedRun 'Fresh'}
if($Mode -in @('Upgrade','Both')){$summary.Upgrade=Invoke-IsolatedRun 'Upgrade'}
$summary.Passed=($null-eq$summary.Fresh -or $summary.Fresh.Passed)-and($null-eq$summary.Upgrade -or $summary.Upgrade.Passed)
$summary.CompletedAt=[DateTimeOffset]::Now.ToString('O')
$summaryPath=Join-Path $EvidenceRoot 'rc3-isolated-summary.json'
[IO.File]::WriteAllText($summaryPath,($summary|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($true))
Get-Content $summaryPath -Raw -Encoding UTF8
if(-not$summary.Passed){exit 1}
