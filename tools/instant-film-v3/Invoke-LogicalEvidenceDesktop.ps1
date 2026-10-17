$ErrorActionPreference="Stop"
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

$Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tool=Join-Path $Workspace 'tools/RC12ProductVisualHarness/Invoke-RC12ProductVisualHarness.ps1'
$worker=Join-Path $Workspace 'artifacts/v3-product/logical-worker.ps1'
$log=Join-Path $Workspace 'artifacts/v3-product/07-logical-producer.log'
@('$ErrorActionPreference="Stop"',"& '$tool' -SkipBuild *> '$log'",'exit $LASTEXITCODE')|Set-Content -LiteralPath $worker -Encoding utf8
$pwsh=(Get-Process -Id $PID).Path
$command='"'+$pwsh+'" -NoProfile -File "'+$worker+'"'
$exitCode=[PixelTartIsolatedDesktop]::Run(('KitaoV3Logical_'+[Guid]::NewGuid().ToString('N')),$command,$Workspace)
"Non-input desktop logical producer exit=$exitCode; physical DPI NOT_RUN"
exit $exitCode
