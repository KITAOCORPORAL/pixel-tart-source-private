param(
    [string]$Installer = '',
    [string]$EvidenceRoot = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($Installer)) {
    $Installer = (Get-ChildItem (Join-Path $repoRoot 'artifacts\releases\2.2.0\rc\installer') -Filter '*RC_Setup_2.2.0_x64.exe' -File | Select-Object -First 1).FullName
}
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $repoRoot 'artifacts\diagnostics\2.2.0\isolated-desktop-acceptance'
}
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $EvidenceRoot $runId
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
$contextPath = Join-Path $runRoot 'context.json'
$localAppData = Join-Path $runRoot 'local-appdata'
$installRoot = Join-Path $runRoot 'installed'
$inputRoot = Join-Path $runRoot 'input'
$collageOutput = Join-Path $runRoot 'collage-output'
$organizeOutput = Join-Path $runRoot 'organize-output'
New-Item -ItemType Directory -Force -Path $localAppData,$inputRoot,$collageOutput,$organizeOutput | Out-Null
$workspaceDotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
$dotnetPath = if (Test-Path -LiteralPath $workspaceDotnet) { $workspaceDotnet } else { Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe' }
$sourceFiles = @(Get-ChildItem (Join-Path $repoRoot 'artifacts\automated-dpi-review\2.0.4\interaction\input') -Filter 'DPI_TEST_*.png' -File | Sort-Object Name)
if ($sourceFiles.Count -ne 4) { throw "Expected four existing acceptance images, found $($sourceFiles.Count)." }
foreach ($source in $sourceFiles) { Copy-Item $source.FullName (Join-Path $inputRoot $source.Name) }
$context = [ordered]@{
    RunId=$runId; RepoRoot=$repoRoot; Installer=[IO.Path]::GetFullPath($Installer)
    EvidenceRoot=$runRoot; LocalAppData=$localAppData; InstallRoot=$installRoot
    InputRoot=$inputRoot; CollageOutput=$collageOutput; OrganizeOutput=$organizeOutput
    DotnetPath=$dotnetPath
    StartedAt=[DateTimeOffset]::Now.ToString('O')
}
$context | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $contextPath -Encoding UTF8

Add-Type @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class PixelTartIsolatedDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateDesktopW(string name, string device, IntPtr devmode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcessW(string app, StringBuilder command, IntPtr processAttrs, IntPtr threadAttrs, bool inherit, uint flags, IntPtr environment, string cwd, ref STARTUPINFO startup, out PROCESS_INFORMATION process);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    const uint GENERIC_ALL = 0x10000000; const uint CREATE_NEW_PROCESS_GROUP = 0x00000200; const uint INFINITE = 0xffffffff;
    public static uint Run(string desktopName, string commandLine, string workingDirectory) {
        var desktop = CreateDesktopW(desktopName, null, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktopW failed");
        try {
            var si = new STARTUPINFO { cb=Marshal.SizeOf<STARTUPINFO>(), lpDesktop=desktopName, dwFlags=0, wShowWindow=0 };
            PROCESS_INFORMATION pi;
            var command = new StringBuilder(commandLine);
            if (!CreateProcessW(null, command, IntPtr.Zero, IntPtr.Zero, false, CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, workingDirectory, ref si, out pi)) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessW failed");
            try { WaitForSingleObject(pi.hProcess, INFINITE); uint code; GetExitCodeProcess(pi.hProcess, out code); return code; }
            finally { CloseHandle(pi.hThread); CloseHandle(pi.hProcess); }
        } finally { CloseDesktop(desktop); }
    }
}
'@

$worker = Join-Path $PSScriptRoot 'Invoke-IsolatedDesktopWorker.ps1'
$powershell = Join-Path $PSHOME 'powershell.exe'
$command = '-NoProfile -ExecutionPolicy Bypass -File "' + $worker + '" -ContextPath "' + $contextPath + '"'
$exitCode = [PixelTartIsolatedDesktop]::Run("PixelTartAcceptance_$runId", $powershell + ' ' + $command, $repoRoot)
$resultPath = Join-Path $runRoot 'result.json'
$latestPath = Join-Path $EvidenceRoot 'latest-result.json'
if (Test-Path $resultPath) { Copy-Item $resultPath $latestPath -Force; Get-Content $resultPath -Raw } else {
    @{ Passed=$false; Error="Isolated worker exited with code $exitCode and produced no result."; RunId=$runId } | ConvertTo-Json | Set-Content $latestPath -Encoding UTF8
    Get-Content $latestPath -Raw
}
if ($exitCode -ne 0) { exit $exitCode }
