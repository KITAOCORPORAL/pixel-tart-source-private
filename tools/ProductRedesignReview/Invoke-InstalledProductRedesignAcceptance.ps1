param(
    [string]$InstalledRoot = '',
    [string]$EvidenceRoot = '',
    [switch]$FailuresOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($InstalledRoot)) {
    $InstalledRoot = Join-Path $repoRoot 'artifacts\installed-acceptance\product-redesign-rc1'
}
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $repoRoot 'artifacts\diagnostics\2.3.0\product-redesign-installed-ui'
}

$repoRoot = [IO.Path]::GetFullPath($repoRoot)
$installedRoot = [IO.Path]::GetFullPath($InstalledRoot)
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not $installedRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed candidate must be inside this repository artifacts directory.'
}
if (-not $evidenceRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must be inside this repository artifacts directory.'
}

$appExe = Join-Path $installedRoot 'KitaoPhotoSelector.exe'
if (-not (Test-Path -LiteralPath $appExe)) { throw "Installed candidate executable not found: $appExe" }
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $evidenceRoot $runId
$runtimeRoot = Join-Path $runRoot 'runtime'
$inputRoot = Join-Path $runRoot 'synthetic-input'
$syncRoot = Join-Path $runRoot 'selection-sync'
New-Item -ItemType Directory -Force -Path $runRoot,$runtimeRoot,$inputRoot,$syncRoot | Out-Null

$contextPath = Join-Path $runRoot 'context.json'
[ordered]@{
    RunId = $runId
    RepoRoot = $repoRoot
    InstalledRoot = $installedRoot
    AppExe = $appExe
    EvidenceRoot = $runRoot
    RuntimeRoot = $runtimeRoot
    InputRoot = $inputRoot
    SyncRoot = $syncRoot
    StartedAt = [DateTimeOffset]::Now.ToString('O')
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $contextPath -Encoding UTF8

Add-Type @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class PixelTartProductRedesignDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateDesktopW(string name, string device, IntPtr devmode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcessW(string app, StringBuilder command, IntPtr processAttrs, IntPtr threadAttrs, bool inherit, uint flags, IntPtr environment, string cwd, ref STARTUPINFO startup, out PROCESS_INFORMATION process);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    const uint GENERIC_ALL = 0x10000000;
    const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
    const uint INFINITE = 0xffffffff;
    public static uint Run(string desktopName, string commandLine, string workingDirectory) {
        var desktop = CreateDesktopW(desktopName, null, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktopW failed");
        try {
            var startup = new STARTUPINFO { cb=Marshal.SizeOf<STARTUPINFO>(), lpDesktop=desktopName };
            PROCESS_INFORMATION process;
            var command = new StringBuilder(commandLine);
            if (!CreateProcessW(null, command, IntPtr.Zero, IntPtr.Zero, false, CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, workingDirectory, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessW failed");
            try { WaitForSingleObject(process.hProcess, INFINITE); uint code; GetExitCodeProcess(process.hProcess, out code); return code; }
            finally { CloseHandle(process.hThread); CloseHandle(process.hProcess); }
        }
        finally { CloseDesktop(desktop); }
    }
}
'@

$worker = Join-Path $PSScriptRoot 'Invoke-InstalledProductRedesignWorker.ps1'
$powershell = Join-Path $PSHOME 'powershell.exe'
$command = '-NoProfile -ExecutionPolicy Bypass -File "' + $worker + '" -ContextPath "' + $contextPath + '"'
if ($FailuresOnly) { $command += ' -FailuresOnly' }
$exitCode = [PixelTartProductRedesignDesktop]::Run("PixelTartProductRedesign_$runId", $powershell + ' ' + $command, $repoRoot)
$resultPath = Join-Path $runRoot 'result.json'
$latestPath = Join-Path $evidenceRoot 'latest-result.json'
if (Test-Path -LiteralPath $resultPath) {
    Copy-Item -LiteralPath $resultPath -Destination $latestPath -Force
    Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
}
else {
    [ordered]@{ Passed=$false; RunId=$runId; Error="Isolated worker exited with code $exitCode and produced no result." } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $latestPath -Encoding UTF8
    Get-Content -LiteralPath $latestPath -Raw -Encoding UTF8
}
if ($exitCode -ne 0) { exit $exitCode }
