[CmdletBinding()]
param(
    [ValidateSet('DryRun', 'Run', 'RecoveryTest')]
    [string]$Mode = 'DryRun',

    [string]$OutputRoot,

    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$SourceHead,

    [ValidateRange(30, 1800)]
    [int]$StepTimeoutSeconds = 300,

    [ValidateRange(500, 5000)]
    [int]$ForegroundStableMilliseconds = 1200
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$projectPath = Join-Path $repositoryRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$captureTool = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\Capture-AssetLibraryP1WindowEvidence.ps1'
$validatorSource = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\Test-AssetLibraryP1GateAEvidence.ps1'
$contractSource = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\gate-a-evidence-contract.json'
$fixtureTool = Join-Path $repositoryRoot 'tools\ModularHarnessV1Acceptance\New-ModularHarnessSyntheticFixture.ps1'
$expectedExecutableName = 'PixelTart_ModularHarness_V1_DevPreview.exe'
$expectedProcessName = 'PixelTart_ModularHarness_V1_DevPreview'
$windowTitle = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oyeIFtNb2R1bGFyIEhhcm5lc3MgRGV2XQ=='))
$buildConfiguration = 'Debug'
$baselineDisplay = [ordered]@{ width = 3840; height = 2160; refresh_rate_hz = 60; scale_percent = 150; dpi = 144 }
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$script:manifest = $null
$script:manifestPath = $null
$script:runRoot = $null
$script:resolvedExecutable = $null
$script:executableHash = $null
$script:contract = $null
$script:activeSession = $null
$script:instructionNumber = 0
$script:displayMatrixStarted = $false
$script:displayRestored = $false
$script:interopInitialized = $false
$script:automationInitialized = $false

function Write-Utf8NoBom {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Content)
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-NewUtf8NoBom {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Content)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try { $writer.Write($Content) } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-PropertyValue {
    param($Object, [Parameter(Mandatory = $true)][string]$Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-PropertyState {
    param($Object, [Parameter(Mandatory = $true)][string]$Name)
    if ($null -eq $Object) {
        return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
    }
    if ($Object -is [Collections.IDictionary]) {
        if (-not $Object.Contains($Name)) {
            return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
        }
        $value = $Object[$Name]
    }
    else {
        $property = $Object.PSObject.Properties[$Name]
        if ($null -eq $property) {
            return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
        }
        $value = $property.Value
    }
    return [pscustomobject]@{ Present = $true; IsArray = $value -is [Array]; Value = $value }
}

function Test-StrictScalarPropertyValue {
    param(
        $Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][Type]$ExpectedType,
        [Parameter(Mandatory = $true)]$ExpectedValue
    )
    $state = Get-PropertyState $Object $Name
    return $state.Present -and
        -not $state.IsArray -and
        $null -ne $state.Value -and
        $state.Value.GetType() -eq $ExpectedType -and
        [object]::Equals($state.Value, $ExpectedValue)
}

function Test-PropertyPresent {
    param($Object, [Parameter(Mandatory = $true)][string]$Name)
    if ($null -eq $Object) { return $false }
    if ($Object -is [Collections.IDictionary]) { return $Object.Contains($Name) }
    return $null -ne $Object.PSObject.Properties[$Name]
}

function Get-NestedValue {
    param($Object, [Parameter(Mandatory = $true)][string[]]$Path)
    $current = $Object
    foreach ($name in $Path) {
        $current = Get-PropertyValue $current $name
        if ($null -eq $current) { return $null }
    }
    return $current
}

function Read-JsonFileSafely {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        try {
            $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
            if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
            return $raw | ConvertFrom-Json
        } catch {
            if ($attempt -eq 3) { return $null }
            Start-Sleep -Milliseconds 50
        }
    }
    return $null
}

function Read-JsonLinesSafely {
    param([Parameter(Mandatory = $true)][string]$Path, [string]$EnvelopeProperty)
    $records = @()
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $records }
    foreach ($line in @(Get-Content -LiteralPath $Path -Encoding UTF8 -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $value = $line | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($EnvelopeProperty)) { $value = Get-PropertyValue $value $EnvelopeProperty }
            if ($null -ne $value) { $records += $value }
        } catch {
            # The writer can be between append and flush. The next poll re-reads the complete line.
        }
    }
    return $records
}

function Invoke-GitText {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Git preflight failed: $($output -join [Environment]::NewLine)" }
    return (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
}

function Get-CurrentSourceHead {
    $head = Invoke-GitText @('rev-parse', 'HEAD')
    if ($head -cnotmatch '^[0-9a-f]{40}$') { throw "Current Git HEAD is not one lowercase 40-character SHA: '$head'." }
    return $head
}

function Get-WorktreeStatus {
    return Invoke-GitText @('status', '--porcelain=v1', '--untracked-files=all')
}

function Assert-TrackedCleanAndHead {
    param([Parameter(Mandatory = $true)][string]$ExpectedHead, [Parameter(Mandatory = $true)][string]$Stage)
    $observedHead = Get-CurrentSourceHead
    if ($observedHead -cne $ExpectedHead) { throw "$Stage failed: Git HEAD changed from $ExpectedHead to $observedHead." }
    $worktreeStatus = Get-WorktreeStatus
    if (-not [string]::IsNullOrWhiteSpace($worktreeStatus)) {
        throw "$Stage failed: worktree has non-ignored tracked or untracked changes. Commit or remove them before a real Run.`n$worktreeStatus"
    }
}

function Save-Manifest {
    if ($null -eq $script:manifest -or [string]::IsNullOrWhiteSpace($script:manifestPath)) { return }
    $script:manifest['updated_at'] = [DateTimeOffset]::UtcNow.ToString('O')
    Write-Utf8NoBom $script:manifestPath ($script:manifest | ConvertTo-Json -Depth 18)
}

function Add-ManifestEvent {
    param([string]$StepId, [string]$Status, [string]$Instruction, [string]$Detail)
    $events = @($script:manifest['step_events'])
    $events += [ordered]@{
        sequence = $events.Count + 1
        step_id = $StepId
        status = $Status
        instruction = $Instruction
        detail = $Detail
        recorded_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $script:manifest['step_events'] = $events
    Save-Manifest
}

function Show-Instruction {
    param([Parameter(Mandatory = $true)][string]$StepId, [Parameter(Mandatory = $true)][string]$Instruction)
    $script:instructionNumber++
    Write-Host ''
    Write-Host (([string]::Concat([char]0x7B2C, ' {0} ', [char]0x6B65, [char]0xFF08, [char]0x53EA, [char]0x505A, [char]0x8FD9, [char]0x4E00, [char]0x4E2A, [char]0x52A8, [char]0x4F5C, [char]0xFF09)) -f $script:instructionNumber) -ForegroundColor Yellow
    Write-Host $Instruction -ForegroundColor Cyan
    Write-Host ([string]::Concat([char]0x5B8C, [char]0x6210, [char]0x540E, [char]0x4E0D, [char]0x8981, [char]0x5207, [char]0x56DE, ' PowerShell; ', [char]0x811A, [char]0x672C, [char]0x4F1A, [char]0x5728, [char]0x540E, [char]0x53F0, [char]0x81EA, [char]0x52A8, [char]0x8BC6, [char]0x522B, [char]0x5E76, [char]0x7EE7, [char]0x7EED, [char]0x3002)) -ForegroundColor DarkGray
    Add-ManifestEvent $StepId 'waiting' $Instruction ''
}

function Complete-Instruction {
    param([string]$StepId, [string]$Instruction, [string]$Detail)
    Write-Host ("PASS: {0}" -f $Detail) -ForegroundColor Green
    Add-ManifestEvent $StepId 'passed' $Instruction $Detail
}

function New-ProbeResult {
    param([bool]$Passed, [string]$Signature, [string]$Detail, [bool]$Fatal = $false)
    return [pscustomobject]@{ Passed = $Passed; Signature = $Signature; Detail = $Detail; Fatal = $Fatal }
}

function Initialize-NativeObservation {
    if ($script:interopInitialized) { return }
    Add-Type -AssemblyName System.Windows.Forms
    $nativeSource = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class PixelTartManualPacketNativeV2
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public ushort SpecVersion; public ushort DriverVersion; public ushort Size; public ushort DriverExtra;
        public uint Fields; public int PositionX; public int PositionY; public uint DisplayOrientation; public uint DisplayFixedOutput;
        public short Color; public short Duplex; public short YResolution; public short TTOption; public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public ushort LogPixels; public uint BitsPerPel; public uint PelsWidth; public uint PelsHeight; public uint DisplayFlags;
        public uint DisplayFrequency; public uint ICMMethod; public uint ICMIntent; public uint MediaType; public uint DitherType;
        public uint Reserved1; public uint Reserved2; public uint PanningWidth; public uint PanningHeight;
    }
    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] public static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scaleFactor);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool EnumDisplaySettingsExW(string deviceName, int modeNumber, ref DevMode mode, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLengthW(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr handle, StringBuilder text, int maximumCount);
    public static IntPtr[] GetVisibleProcessWindows(uint processId)
    {
        var result = new List<IntPtr>();
        EnumWindows((handle, _) => { uint owner; GetWindowThreadProcessId(handle, out owner); if (owner == processId && IsWindowVisible(handle)) result.Add(handle); return true; }, IntPtr.Zero);
        return result.ToArray();
    }
    public static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLengthW(handle); if (length <= 0) return string.Empty;
        var text = new StringBuilder(length + 1); GetWindowTextW(handle, text, text.Capacity); return text.ToString();
    }
}

public static class PixelTartManualPacketCancellationV2
{
    public static volatile bool Requested;
    private static bool installed;
    public static void Install()
    {
        Requested = false;
        if (installed) return;
        Console.CancelKeyPress += OnCancel;
        installed = true;
    }
    public static void Uninstall()
    {
        if (!installed) return;
        Console.CancelKeyPress -= OnCancel;
        installed = false;
    }
    private static void OnCancel(object sender, ConsoleCancelEventArgs args) { args.Cancel = true; Requested = true; }
}
'@
    Add-Type -TypeDefinition $nativeSource -Language CSharp
    $script:interopInitialized = $true
}

function Initialize-AutomationObservation {
    if ($script:automationInitialized) { return }
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $script:automationInitialized = $true
}

function Test-CancelRequested {
    if ($script:interopInitialized -and [PixelTartManualPacketCancellationV2]::Requested) {
        throw [OperationCanceledException]::new('用户请求安全取消人工验收。')
    }
}

function Get-WindowObservation {
    param([Parameter(Mandatory = $true)][int]$ProcessId)
    Initialize-NativeObservation
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process -or $process.HasExited) { return $null }
    $handles = @([PixelTartManualPacketNativeV2]::GetVisibleProcessWindows([uint32]$ProcessId) | Where-Object {
            [PixelTartManualPacketNativeV2]::GetWindowTitle($_) -ceq $windowTitle
        })
    if ($handles.Count -ne 1) { return $null }
    $handle = [IntPtr]$handles[0]
    $rect = New-Object PixelTartManualPacketNativeV2+Rect
    if (-not [PixelTartManualPacketNativeV2]::GetWindowRect($handle, [ref]$rect)) { return $null }
    $path = [IO.Path]::GetFullPath($process.Path)
    return [pscustomobject]@{
        ProcessId = $ProcessId
        Hwnd = ([string]::Format(([char]48 + [char]120 + '{0:X}'), $handle.ToInt64()))
        Handle = $handle
        Path = $path
        Title = [PixelTartManualPacketNativeV2]::GetWindowTitle($handle)
        Foreground = [PixelTartManualPacketNativeV2]::GetForegroundWindow() -eq $handle
        Minimized = [PixelTartManualPacketNativeV2]::IsIconic($handle)
        Maximized = [PixelTartManualPacketNativeV2]::IsZoomed($handle)
        Dpi = [int][PixelTartManualPacketNativeV2]::GetDpiForWindow($handle)
        Rect = ('{0},{1},{2},{3}' -f $rect.Left, $rect.Top, $rect.Right, $rect.Bottom)
    }
}

function Get-DisplayObservation {
    param([IntPtr]$WindowHandle = [IntPtr]::Zero)
    Initialize-NativeObservation
    $screen = if ($WindowHandle -ne [IntPtr]::Zero) { [Windows.Forms.Screen]::FromHandle($WindowHandle) } else { [Windows.Forms.Screen]::PrimaryScreen }
    $mode = New-Object PixelTartManualPacketNativeV2+DevMode
    $mode.Size = [Runtime.InteropServices.Marshal]::SizeOf([type][PixelTartManualPacketNativeV2+DevMode])
    if (-not [PixelTartManualPacketNativeV2]::EnumDisplaySettingsExW($screen.DeviceName, -1, [ref]$mode, 0)) {
        throw "无法只读观察显示模式：$($screen.DeviceName)"
    }
    $monitor = if ($WindowHandle -ne [IntPtr]::Zero) {
        [PixelTartManualPacketNativeV2]::MonitorFromWindow($WindowHandle, 2)
    } else {
        $point = New-Object PixelTartManualPacketNativeV2+Point
        $point.X = $screen.Bounds.Left + [Math]::Max(1, [int]($screen.Bounds.Width / 2))
        $point.Y = $screen.Bounds.Top + [Math]::Max(1, [int]($screen.Bounds.Height / 2))
        [PixelTartManualPacketNativeV2]::MonitorFromPoint($point, 2)
    }
    $scale = 0
    $scaleResult = [PixelTartManualPacketNativeV2]::GetScaleFactorForMonitor($monitor, [ref]$scale)
    if ($scaleResult -ne 0) { throw "无法只读观察显示缩放，HRESULT=0x$('{0:X8}' -f $scaleResult)。" }
    return [ordered]@{
        monitor_device_name = $screen.DeviceName
        width = [int]$mode.PelsWidth
        height = [int]$mode.PelsHeight
        refresh_rate_hz = [int]$mode.DisplayFrequency
        scale_percent = [int]$scale
        observed_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
}

function Test-DisplayMatches {
    param($Observed, $Expected, [bool]$RequireRefreshRate)
    if ($null -eq $Observed) { return $false }
    if ([int]$Observed.width -ne [int]$Expected.width -or
        [int]$Observed.height -ne [int]$Expected.height -or
        [int]$Observed.scale_percent -ne [int]$Expected.scale_percent) { return $false }
    return -not $RequireRefreshRate -or [int]$Observed.refresh_rate_hz -eq [int]$Expected.refresh_rate_hz
}

function Test-WindowsAbsolutePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    # Windows PowerShell 5.1 runs on .NET Framework, so use explicit Windows path
    # syntax instead of a newer runtime-only qualification helper.
    return $Path -match '^[A-Za-z]:[\\/]' -or $Path -match '^\\\\[^\\/]+[\\/][^\\/]+'
}

function Get-FocusedAutomationObservation {
    Initialize-AutomationObservation
    try {
        $focused = [Windows.Automation.AutomationElement]::FocusedElement
        if ($null -eq $focused) { return $null }
        return [pscustomobject]@{
            ProcessId = [int]$focused.Current.ProcessId
            AutomationId = [string]$focused.Current.AutomationId
            Name = [string]$focused.Current.Name
        }
    } catch { return $null }
}

function Wait-ForStep {
    param(
        [Parameter(Mandatory = $true)][string]$StepId,
        [Parameter(Mandatory = $true)][string]$Instruction,
        [Parameter(Mandatory = $true)][scriptblock]$Probe,
        $Session,
        [bool]$RequireWindow = $true,
        [bool]$RequireMaximized = $false
    )
    Show-Instruction $StepId $Instruction
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StepTimeoutSeconds)
    $stableSince = $null
    $stableSignature = ''
    $lastDetail = '等待真实状态'
    $nextProgress = [DateTimeOffset]::UtcNow.AddSeconds(15)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Test-CancelRequested
        if ($null -ne $Session -and $Session.Process.HasExited) {
            throw [OperationCanceledException]::new("软件窗口已关闭；已在步骤 '$StepId' 安全取消。")
        }
        $result = & $Probe
        if ($null -eq $result) { $result = New-ProbeResult $false '' '状态尚未写入' }
        if ($result.Fatal) { throw "步骤 '$StepId' 局部校验失败：$($result.Detail)" }
        $lastDetail = [string]$result.Detail
        $windowReady = $true
        $windowSignature = ''
        if ($RequireWindow) {
            $window = Get-WindowObservation $Session.Process.Id
            $windowReady = $null -ne $window -and $window.Foreground -and -not $window.Minimized -and
                [string]::Equals($window.Path, $script:resolvedExecutable, [StringComparison]::OrdinalIgnoreCase) -and
                $window.Hwnd -ceq $Session.Hwnd
            if ($RequireMaximized) { $windowReady = $windowReady -and $window.Maximized }
            if ($null -ne $window) { $windowSignature = "$($window.Hwnd)|$($window.Rect)|$($window.Dpi)|$($window.Maximized)" }
        }
        if ($result.Passed -and $windowReady) {
            $signature = "$($result.Signature)|$windowSignature"
            if ($signature -cne $stableSignature) {
                $stableSignature = $signature
                $stableSince = [DateTimeOffset]::UtcNow
            } elseif ($null -ne $stableSince -and ([DateTimeOffset]::UtcNow - $stableSince).TotalMilliseconds -ge $ForegroundStableMilliseconds) {
                Complete-Instruction $StepId $Instruction $lastDetail
                return $result
            }
        } else {
            $stableSince = $null
            $stableSignature = ''
        }
        if ([DateTimeOffset]::UtcNow -ge $nextProgress) {
            Write-Host ("仍在后台等待：{0}" -f $lastDetail) -ForegroundColor DarkYellow
            $nextProgress = [DateTimeOffset]::UtcNow.AddSeconds(15)
        }
        Start-Sleep -Milliseconds 200
    }
    throw "步骤 '$StepId' 超时。目标与当前状态：$lastDetail"
}

function Wait-ForExpectedClose {
    param($Session, [string]$StepId, [string]$Instruction)
    Show-Instruction $StepId $Instruction
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StepTimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Test-CancelRequested
        if ($Session.Process.HasExited) {
            $Session.Process.WaitForExit()
            [void](Wait-ForNoExistingDevPreview -Context "关闭 PID $($Session.Process.Id) 后")
            Complete-Instruction $StepId $Instruction '进程已由用户正常关闭，且全局进程表已连续稳定清零'
            if ($script:activeSession -eq $Session) { $script:activeSession = $null }
            return
        }
        Start-Sleep -Milliseconds 200
    }
    throw "等待用户正常关闭软件超时：PID $($Session.Process.Id)。"
}

function Wait-ForDisplay {
    param([string]$StepId, [string]$Instruction, $Expected, [bool]$RequireRefreshRate)
    return Wait-ForStep -StepId $StepId -Instruction $Instruction -RequireWindow:$false -Session $null -Probe {
        $observed = Get-DisplayObservation
        $passed = Test-DisplayMatches $observed $Expected $RequireRefreshRate
        $detail = "当前 $($observed.width)x$($observed.height)@$($observed.refresh_rate_hz)Hz / $($observed.scale_percent)%；目标 $($Expected.width)x$($Expected.height) / $($Expected.scale_percent)%"
        New-ProbeResult $passed ("$($observed.width)x$($observed.height)|$($observed.refresh_rate_hz)|$($observed.scale_percent)") $detail
    }
}

function Quote-ProcessArgument {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-HiddenProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$StdOutPath,
        [Parameter(Mandatory = $true)][string]$StdErrPath
    )
    $parent = Split-Path -Parent $StdOutPath
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput $StdOutPath -RedirectStandardError $StdErrPath
    return [int]$process.ExitCode
}

function Invoke-WithEnvironment {
    param([Parameter(Mandatory = $true)][hashtable]$Environment, [Parameter(Mandatory = $true)][scriptblock]$Action)
    $previous = @{}
    foreach ($key in $Environment.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $Environment[$key], 'Process')
    }
    try { return & $Action } finally {
        foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
    }
}

function Get-DevPreviewProcessSnapshot {
    try {
        $managedIds = @(Get-Process -ErrorAction Stop | Where-Object {
                [string]::Equals([string]$_.ProcessName, $expectedProcessName, [StringComparison]::OrdinalIgnoreCase)
            } | ForEach-Object { [int]$_.Id })
        $escapedExecutableName = $expectedExecutableName.Replace("'", "''")
        $cimIds = @(Get-CimInstance -ClassName Win32_Process -Filter ("Name = '{0}'" -f $escapedExecutableName) `
                -OperationTimeoutSec 2 -ErrorAction Stop |
                ForEach-Object { [int]$_.ProcessId })
    } catch {
        throw "无法可靠枚举 $expectedProcessName 进程；为避免假通过，本轮停止：$($_.Exception.Message)"
    }
    $processIds = @($managedIds + $cimIds | Sort-Object -Unique)
    return [pscustomobject]@{
        Count = @($processIds).Count
        ProcessIds = $processIds
        ManagedProcessIds = @($managedIds | Sort-Object -Unique)
        CimProcessIds = @($cimIds | Sort-Object -Unique)
    }
}

function Wait-ForStableEmptyProcessSnapshot {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$SnapshotProvider,
        [Parameter(Mandatory = $true)][string]$Context,
        [ValidateRange(100, 60000)][int]$TimeoutMilliseconds = 10000,
        [ValidateRange(50, 10000)][int]$StableMilliseconds = 1000,
        [ValidateRange(10, 1000)][int]$PollMilliseconds = 100
    )
    if ($StableMilliseconds -ge $TimeoutMilliseconds) {
        throw 'StableMilliseconds must be smaller than TimeoutMilliseconds.'
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    [long]$zeroSince = -1
    $lastSnapshot = $null
    while ($clock.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        Test-CancelRequested
        try { $lastSnapshot = & $SnapshotProvider } catch {
            throw "$Context 无法可靠读取进程表；为避免假通过，本轮停止：$($_.Exception.Message)"
        }
        if ($null -eq $lastSnapshot -or $null -eq (Get-PropertyValue $lastSnapshot 'Count')) {
            throw "$Context 的进程快照无效；为避免假通过，本轮停止。"
        }
        if ([int](Get-PropertyValue $lastSnapshot 'Count') -eq 0) {
            if ($zeroSince -lt 0) { $zeroSince = $clock.ElapsedMilliseconds }
            $stableFor = $clock.ElapsedMilliseconds - $zeroSince
            if ($stableFor -ge $StableMilliseconds) {
                return [pscustomobject]@{
                    Snapshot = $lastSnapshot
                    ElapsedMilliseconds = [long]$clock.ElapsedMilliseconds
                    StableMilliseconds = [long]$stableFor
                }
            }
        } else {
            $zeroSince = -1
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    }
    $lastIds = if ($null -eq $lastSnapshot) { @() } else { @((Get-PropertyValue $lastSnapshot 'ProcessIds')) }
    $lastIdText = if (@($lastIds).Count -eq 0) { '无（但未达到连续稳定窗口）' } else { $lastIds -join ',' }
    throw "$Context 未在 $TimeoutMilliseconds ms 内达到连续 $StableMilliseconds ms 的进程表稳定清零；最后 PID=$lastIdText。"
}

function Wait-ForNoExistingDevPreview {
    param(
        [Parameter(Mandatory = $true)][string]$Context,
        [int]$TimeoutMilliseconds = 10000,
        [int]$StableMilliseconds = 1000,
        [int]$PollMilliseconds = 100
    )
    return Wait-ForStableEmptyProcessSnapshot -Context $Context -TimeoutMilliseconds $TimeoutMilliseconds `
        -StableMilliseconds $StableMilliseconds -PollMilliseconds $PollMilliseconds -SnapshotProvider {
            Get-DevPreviewProcessSnapshot
        }
}

function Assert-NoExistingDevPreview {
    [void](Wait-ForNoExistingDevPreview -Context "启动 $expectedProcessName 前")
}

function Get-DotnetHost {
    $candidates = [Collections.Generic.List[string]]::new()
    for ($directory = [IO.DirectoryInfo]::new($repositoryRoot); $null -ne $directory; $directory = $directory.Parent) {
        $candidates.Add((Join-Path $directory.FullName '.dotnet\dotnet.exe'))
    }
    $dotnetRoot = [Environment]::GetEnvironmentVariable('DOTNET_ROOT', 'Process')
    if (-not [string]::IsNullOrWhiteSpace($dotnetRoot)) { $candidates.Add((Join-Path $dotnetRoot 'dotnet.exe')) }
    $pathCommand = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $pathCommand) { $candidates.Add([string]$pathCommand.Source) }

    foreach ($candidate in @($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $sdks = @(& $candidate --list-sdks 2>$null)
        if ($LASTEXITCODE -eq 0 -and @($sdks | Where-Object { [string]$_ -match '^10\.0\.' }).Count -gt 0) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'A .NET 10 SDK is required. No repository-bundled, DOTNET_ROOT, or PATH dotnet host exposes a 10.0 SDK.'
}

function Stop-SessionForCleanup {
    param($Session)
    if ($null -eq $Session) { return }
    try {
        if (-not $Session.Process.HasExited) {
            Stop-Process -Id $Session.Process.Id -ErrorAction SilentlyContinue
            Wait-Process -Id $Session.Process.Id -Timeout 10 -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Warning "清理 PID $($Session.Process.Id) 时发生错误：$($_.Exception.Message)"
    }
    if ($script:activeSession -eq $Session) { $script:activeSession = $null }
}

function Add-SessionIdentity {
    param([string]$SessionId, [int]$ProcessId, [string]$Hwnd, [string]$RuntimeRoot)
    $sessions = @($script:manifest['sessions'])
    if (@($sessions | Where-Object { [string](Get-PropertyValue $_ 'id') -ceq $SessionId }).Count -ne 0) {
        throw "Duplicate manual session identity: $SessionId"
    }
    $sessions += [ordered]@{
        id = $SessionId
        process_id = $ProcessId
        window_hwnd = $Hwnd
        source_head = $SourceHead
        build_configuration = $buildConfiguration
        executable_path = $script:resolvedExecutable
        executable_sha256 = $script:executableHash
        runtime_root = $RuntimeRoot
        started_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $script:manifest['sessions'] = $sessions
    Save-Manifest
}

function Start-AcceptanceSession {
    param(
        [Parameter(Mandatory = $true)][string]$SessionId,
        [Parameter(Mandatory = $true)][string]$RuntimeRoot,
        [string]$Scenario,
        [string]$DemoDirectory,
        [bool]$AllowExistingRoot = $false
    )
    Assert-NoExistingDevPreview
    if (Test-Path -LiteralPath $RuntimeRoot) {
        if (-not $AllowExistingRoot) { throw "验收会话目录必须全新：$RuntimeRoot" }
    } else { [IO.Directory]::CreateDirectory($RuntimeRoot) | Out-Null }
    $environment = @{
        PIXEL_TART_ACCEPTANCE_ROOT = $RuntimeRoot
        PIXEL_TART_ASSET_LIBRARY_DEMO_DIR = $(if ([string]::IsNullOrWhiteSpace($DemoDirectory)) { $null } else { $DemoDirectory })
        PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE = $(if ([string]::IsNullOrWhiteSpace($Scenario)) { $null } else { $Scenario })
        PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE = 'asset-library'
        PIXEL_TART_ASSET_LIBRARY_P1_HEAD = $SourceHead
        PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS = '1'
    }
    $process = Invoke-WithEnvironment $environment {
        Start-Process -FilePath $script:resolvedExecutable -PassThru
    }
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
        $window = $null
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            if ($process.HasExited) { throw "DevPreview 在显示主窗口前退出，exit=$($process.ExitCode)。" }
            $window = Get-WindowObservation $process.Id
            if ($null -ne $window) { break }
            Start-Sleep -Milliseconds 200
        }
        if ($null -eq $window) { throw '未在 45 秒内观察到唯一、精确标题的 DevPreview 主窗口。' }
        if (-not [string]::Equals($window.Path, $script:resolvedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
            throw "DevPreview 路径不一致：$($window.Path)"
        }
        $observedHash = (Get-FileHash -LiteralPath $window.Path -Algorithm SHA256).Hash
        if ($observedHash -cne $script:executableHash) { throw 'DevPreview EXE 哈希与本轮专属构建不一致。' }
        $global = @(Get-CimInstance Win32_Process | Where-Object { [string]::Equals([string]$_.Name, $expectedExecutableName, [StringComparison]::OrdinalIgnoreCase) })
        if ($global.Count -ne 1 -or [int]$global[0].ProcessId -ne $process.Id -or
            -not [string]::Equals([IO.Path]::GetFullPath([string]$global[0].ExecutablePath), $script:resolvedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'DevPreview 全局进程唯一性、PID 或绝对路径校验失败。'
        }
        $session = [pscustomobject]@{ Id = $SessionId; Root = $RuntimeRoot; Process = $process; Hwnd = $window.Hwnd }
        $script:activeSession = $session
        Add-SessionIdentity $SessionId $process.Id $window.Hwnd $RuntimeRoot
        return $session
    } catch {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
        throw
    }
}

function Invoke-DedicatedBuild {
    Assert-TrackedCleanAndHead $SourceHead '专属构建前'
    $publishRoot = Join-Path $script:runRoot 'build\publish'
    $logsRoot = Join-Path $script:runRoot 'logs'
    $expectedExecutable = Join-Path $publishRoot $expectedExecutableName
    $dotnet = Get-DotnetHost
    $arguments = @(
        'publish', (Quote-ProcessArgument $projectPath),
        '-c', $buildConfiguration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:ModularHarnessDevPreview=true',
        '-p:AssetLibraryP1StateAcceptance=true',
        '-p:InputRoutingDiagnostics=true',
        '-p:TreatWarningsAsErrors=true',
        '--output', (Quote-ProcessArgument $publishRoot),
        '--nologo'
    )
    $startedAt = [DateTimeOffset]::UtcNow
    $exitCode = Invoke-WithEnvironment @{ MSBUILDDISABLENODEREUSE = '1' } {
        Invoke-HiddenProcess $dotnet $arguments (Join-Path $logsRoot 'dedicated-build.stdout.txt') (Join-Path $logsRoot 'dedicated-build.stderr.txt')
    }
    if ($exitCode -ne 0) { throw "专属 warnings-as-errors 构建失败，exit=$exitCode；查看 logs\dedicated-build.*.txt。" }
    Assert-TrackedCleanAndHead $SourceHead '专属构建后'
    if (-not (Test-Path -LiteralPath $expectedExecutable -PathType Leaf)) { throw "专属构建未生成目标 EXE：$expectedExecutable" }
    $matchingExecutables = @(Get-ChildItem -LiteralPath $publishRoot -File -Filter $expectedExecutableName)
    if ($matchingExecutables.Count -ne 1) { throw "专属构建目录中目标 EXE 数量不是 1：$($matchingExecutables.Count)。" }
    $script:resolvedExecutable = [IO.Path]::GetFullPath($expectedExecutable)
    $script:executableHash = (Get-FileHash -LiteralPath $script:resolvedExecutable -Algorithm SHA256).Hash
    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($script:resolvedExecutable).ProductVersion
    if ([string]::IsNullOrWhiteSpace($productVersion) -or $productVersion.IndexOf($SourceHead, [StringComparison]::Ordinal) -lt 0) {
        throw "专属 EXE 未通过嵌入源码 HEAD 校验；ProductVersion='$productVersion'。"
    }
    $buildManifest = [ordered]@{
        schema = 'pixel-tart-p1-gate-a-build-manifest/v1'
        source_head = $SourceHead
        repository_tracked_clean = $true
        source_head_is_current_head = $true
        dedicated_build_succeeded = $true
        build_configuration = $buildConfiguration
        executable_path = $script:resolvedExecutable
        executable_sha256 = $script:executableHash
        informational_version = $productVersion
        warnings_as_errors = $true
        modular_harness_dev_preview = $true
        asset_library_p1_acceptance = $true
        input_routing_diagnostics = $true
        msbuild_node_reuse_disabled = $true
        created_at = [DateTimeOffset]::UtcNow.ToString('O')
        build_started_at = $startedAt.ToString('O')
    }
    Write-Utf8NoBom (Join-Path $script:runRoot 'build-manifest.json') ($buildManifest | ConvertTo-Json -Depth 8)
    $script:manifest['build_manifest_file'] = 'build-manifest.json'
    $script:manifest['build_configuration'] = $buildConfiguration
    $script:manifest['executable_path'] = $script:resolvedExecutable
    $script:manifest['executable_sha256'] = $script:executableHash
    $script:manifest['dedicated_build_succeeded'] = $true
    Save-Manifest
}

function Get-StateSessionData {
    param([Parameter(Mandatory = $true)][string]$RuntimeRoot)
    $directory = Join-Path $RuntimeRoot 'InputDiagnostics\AssetLibraryP1StateAcceptance'
    $manifest = Read-JsonFileSafely (Join-Path $directory 'scenario-manifest.json')
    $snapshots = @(Read-JsonLinesSafely (Join-Path $directory 'view-model-snapshots.jsonl') 'snapshot')
    $controller = @(Read-JsonLinesSafely (Join-Path $directory 'controller-events.jsonl') '')
    return [pscustomobject]@{ Directory = $directory; Manifest = $manifest; Snapshots = $snapshots; Controller = $controller }
}

function Test-StateManifestIdentity {
    param($Data, $Session, [string]$Scenario)
    $manifest = $Data.Manifest
    return $null -ne $manifest -and
        [string](Get-PropertyValue $manifest 'scenario') -ceq $Scenario -and
        [int](Get-PropertyValue $manifest 'processId') -eq $Session.Process.Id -and
        [string](Get-PropertyValue $manifest 'startRouteHead') -ceq $SourceHead -and
        [string](Get-PropertyValue $manifest 'startRoute') -ceq 'asset-library' -and
        [string](Get-PropertyValue $manifest 'startRouteCurrentPage') -ceq 'AssetLibrary'
}

function Test-StateTimelineComplete {
    param($Data, [string]$SessionContractId)
    $sessionContract = @($script:contract.state_chain.sessions | Where-Object { [string]$_.id -ceq $SessionContractId }) | Select-Object -First 1
    if ($null -eq $sessionContract) { return $false }
    $previousRecordedAt = [DateTimeOffset]::MinValue
    foreach ($required in @($sessionContract.required_timeline)) {
        $source = if ([string]$required.stream -ceq 'snapshot') { $Data.Snapshots } else { $Data.Controller }
        $matches = @($source | Where-Object {
                [string](Get-PropertyValue $_ 'stage') -ceq [string]$required.stage -and
                [int](Get-PropertyValue $_ 'attempt') -eq [int]$required.attempt
            })
        if ($matches.Count -ne 1) { return $false }
        $recordedAt = [DateTimeOffset]::Parse([string](Get-PropertyValue $matches[0] 'recordedAt'))
        if ($recordedAt -lt $previousRecordedAt) { return $false }
        $previousRecordedAt = $recordedAt
    }
    return $true
}

function Test-ReadySnapshot {
    param($Data, [int]$Attempt)
    $ready = @($Data.Snapshots | Where-Object { [string](Get-PropertyValue $_ 'stage') -ceq 'ready' -and [int](Get-PropertyValue $_ 'attempt') -eq $Attempt })
    if ($ready.Count -ne 1) { return $false }
    $value = $ready[0]
    return [bool](Get-PropertyValue $value 'isReady') -and
        -not [bool](Get-PropertyValue $value 'isLoading') -and
        -not [bool](Get-PropertyValue $value 'hasLoadError') -and
        [int](Get-PropertyValue $value 'visibleAssetCount') -eq 0 -and
        [int](Get-PropertyValue $value 'repositoryAssetCount') -eq 0
}

function Get-PhysicalDocument {
    param([Parameter(Mandatory = $true)][string]$RuntimeRoot)
    return Read-JsonFileSafely (Join-Path $RuntimeRoot 'InputDiagnostics\physical-pointer-session.json')
}

function Test-KeyLayersLocal {
    param($Attempt, [string]$ControlId, [string]$Key, [bool]$AllowKeyDownCompletedActivation = $false)
    if ($null -eq $Attempt -or [string](Get-PropertyValue $Attempt 'origin') -cne 'Win32' -or [string](Get-PropertyValue $Attempt 'key') -cne $Key) { return $false }
    $virtualKey = switch ($Key) { 'Enter' { 13 } 'Space' { 32 } 'Left' { 37 } 'Right' { 39 } default { -1 } }
    if ([int](Get-PropertyValue $Attempt 'virtual_key') -ne $virtualKey) { return $false }
    $layer1 = Get-PropertyValue $Attempt 'layer1_win32'
    if (-not [bool](Get-PropertyValue $layer1 'key_down_received') -or -not [bool](Get-PropertyValue $layer1 'key_up_received')) { return $false }
    $nativeEvents = @(Get-PropertyValue $layer1 'events')
    if ($nativeEvents.Count -ne 2 -or
        @($nativeEvents | Where-Object { [string](Get-PropertyValue $_ 'message') -ceq 'WM_KEYDOWN' }).Count -ne 1 -or
        @($nativeEvents | Where-Object { [string](Get-PropertyValue $_ 'message') -ceq 'WM_KEYUP' }).Count -ne 1) { return $false }
    $layer2 = Get-PropertyValue $Attempt 'layer2_wpf'
    $layer4 = Get-PropertyValue $Attempt 'layer4_action'
    $isKeyDownCompletedActivation = $AllowKeyDownCompletedActivation -and $ControlId -ceq 'RetryAssetLibraryLoad' -and $Key -ceq 'Enter'
    foreach ($flag in @('preview_key_down_received', 'key_down_received')) {
        if (-not [bool](Get-PropertyValue $layer2 $flag)) { return $false }
    }
    foreach ($flag in @('preview_key_up_received', 'key_up_received')) {
        if ($isKeyDownCompletedActivation -eq [bool](Get-PropertyValue $layer2 $flag)) { return $false }
    }
    $expectedWpfEvents = @($(if ($isKeyDownCompletedActivation) { 'PreviewKeyDown'; 'KeyDown' } else { 'PreviewKeyDown'; 'KeyDown'; 'PreviewKeyUp'; 'KeyUp' }))
    if (@(Get-PropertyValue $layer2 'events').Count -ne $expectedWpfEvents.Count) { return $false }
    foreach ($eventName in $expectedWpfEvents) {
        if (@(@(Get-PropertyValue $layer2 'events') | Where-Object { [string](Get-PropertyValue $_ 'event_name') -ceq $eventName }).Count -ne 1) { return $false }
    }
    $layer3 = Get-PropertyValue $Attempt 'layer3_target'
    if ([string](Get-PropertyValue $layer3 'control_automation_id') -cne $ControlId -or
        [string](Get-PropertyValue $layer3 'focused_automation_id_at_down') -cne $ControlId) { return $false }
    if (-not $isKeyDownCompletedActivation) {
        return [string](Get-PropertyValue $layer3 'focused_automation_id_at_up') -ceq $ControlId
    }

    $focusedElementSnapshotStateAtUp = Get-PropertyState $layer3 'actual_focused_element_at_native_key_up'
    $focusedElementSnapshotAtUp = $focusedElementSnapshotStateAtUp.Value
    $focusedElementIdStateAtUp = Get-PropertyState $focusedElementSnapshotAtUp 'automation_id'
    $focusedIdStateAtUp = Get-PropertyState $layer3 'actual_focused_automation_id_at_native_key_up'
    $focusedElementAtUpIsString = $focusedElementIdStateAtUp.Present -and
        -not $focusedElementIdStateAtUp.IsArray -and
        $null -ne $focusedElementIdStateAtUp.Value -and
        $focusedElementIdStateAtUp.Value.GetType() -eq [string]
    $focusedAtUpIsString = $focusedIdStateAtUp.Present -and
        -not $focusedIdStateAtUp.IsArray -and
        $null -ne $focusedIdStateAtUp.Value -and
        $focusedIdStateAtUp.Value.GetType() -eq [string]
    $nearestFocusMatchesControlAtUp = $focusedAtUpIsString -and $focusedIdStateAtUp.Value -ceq $ControlId
    $directFocusMatchesControlAtUp = $focusedElementAtUpIsString -and $focusedElementIdStateAtUp.Value -ceq $ControlId
    $focusedElementIsOriginalTargetAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_is_original_target_at_native_key_up' ([bool]) $true
    $focusedElementIsDifferentTargetAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_is_original_target_at_native_key_up' ([bool]) $false
    $focusedElementAvailableAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_available_at_native_key_up' ([bool]) $true
    $focusedElementUnavailableAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_available_at_native_key_up' ([bool]) $false
    $focusStateAtUpIsValid = if ($focusedElementIsOriginalTargetAtUp) {
        $nearestFocusMatchesControlAtUp -and
        $directFocusMatchesControlAtUp -and
        $focusedElementUnavailableAtUp
    }
    else {
        $focusedAtUpIsString -and
        $focusedElementAtUpIsString -and
        -not $nearestFocusMatchesControlAtUp -and
        -not $directFocusMatchesControlAtUp -and
        $focusedElementIsDifferentTargetAtUp -and
        $focusedElementAvailableAtUp
    }
    $clickEvents = @(@(Get-PropertyValue $layer4 'events') | Where-Object { [string](Get-PropertyValue $_ 'event_name') -ceq 'ButtonClick' })
    return $ControlId -ceq 'RetryAssetLibraryLoad' -and $Key -ceq 'Enter' -and
        (Test-StrictScalarPropertyValue $layer4 'button_click_received' ([bool]) $true) -and
        (Test-StrictScalarPropertyValue $layer4 'physical_target_confirmed' ([bool]) $true) -and
        (Test-StrictScalarPropertyValue $layer4 'activation_completed_on_key_down' ([bool]) $true) -and
        (Test-StrictScalarPropertyValue $layer4 'activation_finalized_at_native_key_up' ([bool]) $true) -and
        $null -ne (Get-PropertyValue $layer4 'activation_finalized_at') -and
        (Test-StrictScalarPropertyValue $layer3 'target_available_at_native_key_up' ([bool]) $false) -and
        $focusedElementSnapshotStateAtUp.Present -and
        -not $focusedElementSnapshotStateAtUp.IsArray -and
        $null -ne $focusedElementSnapshotAtUp -and
        $focusedElementAtUpIsString -and
        $focusedAtUpIsString -and
        $focusStateAtUpIsValid -and
        $clickEvents.Count -eq 1 -and
        [string](Get-NestedValue $layer4 @('button', 'automation_id')) -ceq $ControlId
}

function Get-QualifiedRetryActivations {
    param($Document)
    $qualified = @()
    if ($null -eq $Document) { return $qualified }
    $keyboardContract = Get-PropertyValue (Get-PropertyValue $script:contract 'retry_physical_activation') 'keyboard'
    $allowedKeys = @(Get-PropertyValue $keyboardContract 'allowed_keys')
    $requiredWpfEvents = @(Get-PropertyValue $keyboardContract 'required_layer2_events')
    $forbiddenWpfEvents = @(Get-PropertyValue $keyboardContract 'forbidden_layer2_events')
    $usesKeyDownNativeUpFinalization =
        $allowedKeys.Count -eq 1 -and
        [string]$allowedKeys[0] -ceq 'Enter' -and
        [string](Get-PropertyValue $keyboardContract 'completion_phase') -ceq 'KeyDown' -and
        (Test-StrictScalarPropertyValue $keyboardContract 'native_key_up_finalization_required' ([bool]) $true) -and
        (Test-StrictScalarPropertyValue $keyboardContract 'native_key_up_focus_policy' ([string]) 'different-focus-or-unavailable-original-target') -and
        $requiredWpfEvents.Count -eq 2 -and
        [string]$requiredWpfEvents[0] -ceq 'PreviewKeyDown' -and
        [string]$requiredWpfEvents[1] -ceq 'KeyDown' -and
        $forbiddenWpfEvents.Count -eq 2 -and
        [string]$forbiddenWpfEvents[0] -ceq 'PreviewKeyUp' -and
        [string]$forbiddenWpfEvents[1] -ceq 'KeyUp'
    if ($usesKeyDownNativeUpFinalization) {
        foreach ($attempt in @(Get-PropertyValue $Document 'key_attempts')) {
            $key = [string](Get-PropertyValue $attempt 'key')
            if ($key -cne 'Enter' -or -not (Test-KeyLayersLocal $attempt 'RetryAssetLibraryLoad' $key $true)) { continue }
            $layer4 = Get-PropertyValue $attempt 'layer4_action'
            if ([bool](Get-PropertyValue $layer4 'button_click_received') -and
                [bool](Get-PropertyValue $layer4 'physical_target_confirmed') -and
                [string](Get-NestedValue $layer4 @('button', 'automation_id')) -ceq 'RetryAssetLibraryLoad') { $qualified += $attempt }
        }
    }
    foreach ($attempt in @(Get-PropertyValue $Document 'attempts')) {
        if ([string](Get-PropertyValue $attempt 'down_target_automation_id') -ceq 'RetryAssetLibraryLoad' -and
            [string](Get-PropertyValue $attempt 'up_target_automation_id') -ceq 'RetryAssetLibraryLoad' -and
            [bool](Get-NestedValue $attempt @('layer4_action', 'button_click_received')) -and
            [bool](Get-NestedValue $attempt @('layer4_action', 'physical_target_confirmed'))) { $qualified += $attempt }
    }
    return $qualified
}

function Get-RetrySessionContamination {
    param(
        [Parameter(Mandatory = $true)]$Data,
        [Parameter(Mandatory = $true)]$Document,
        [Parameter(Mandatory = $true)][string]$RuntimeRoot,
        [string[]]$AllowedActivationIds = @()
    )

    $attemptTwo = @($Data.Snapshots | Where-Object { [int](Get-PropertyValue $_ 'attempt') -ge 2 })
    $unexpectedActivations = @(Get-QualifiedRetryActivations $Document | Where-Object {
            [string](Get-PropertyValue $_ 'attempt_id') -notin $AllowedActivationIds
        })
    $import = Read-JsonFileSafely (Join-Path $RuntimeRoot 'InputDiagnostics\asset-library-import.json')
    $importEntered = $null -ne $import -and (
        [bool](Get-PropertyValue $import 'picker_accepted') -or
        [bool](Get-PropertyValue $import 'import_command_entered') -or
        [bool](Get-PropertyValue $import 'import_service_entered') -or
        [int](Get-PropertyValue $import 'selected_file_count') -gt 0 -or
        [int](Get-PropertyValue $import 'imported_count') -gt 0 -or
        -not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $import 'source_kind')))

    if ($importEntered) {
        return '检测到文件选择或导入；retry 会话只允许空库错误与重试，不得导入任何素材。请保留本轮 run root 并重新运行。'
    }
    if ($unexpectedActivations.Count -gt 0) {
        return '“重试”已在允许步骤之前被点击或按键触发。请保留本轮 run root 并重新运行；下次先等脚本自动捕获错误态。'
    }
    if ($attemptTwo.Count -gt 0) {
        return '状态已经提前离开 attempt 1 错误态并进入 attempt 2，但没有对应的本步骤合格输入证据。请保留本轮 run root 并重新运行。'
    }
    return ''
}

function Get-RouteSession {
    param([Parameter(Mandatory = $true)][string]$RuntimeRoot)
    return Read-JsonFileSafely (Join-Path $RuntimeRoot 'InputDiagnostics\AssetLibraryP1RouteAcceptance\current-route-session.json')
}

function Test-RouteSessionApplied {
    param($Route, $Session, [int]$MinimumSessionIndex)
    if ($null -eq $Route) { return $false }
    $fresh = Get-PropertyValue $Route 'freshAssetLibraryDatabaseVerified'
    if ($null -eq $fresh) { $fresh = Get-PropertyValue $Route 'fresh_asset_library_database_verified' }
    return [string](Get-PropertyValue $Route 'status') -ceq 'applied' -and
        [string](Get-PropertyValue $Route 'sourceHead') -ceq $SourceHead -and
        [string](Get-PropertyValue $Route 'route') -ceq 'asset-library' -and
        [string](Get-PropertyValue $Route 'currentPage') -ceq 'AssetLibrary' -and
        [int](Get-PropertyValue $Route 'processId') -eq $Session.Process.Id -and
        [int](Get-PropertyValue $Route 'sessionIndex') -ge $MinimumSessionIndex -and [bool]$fresh
}

function Test-SyntheticImportComplete {
    param([Parameter(Mandatory = $true)][string]$RuntimeRoot)
    $diagnostic = Read-JsonFileSafely (Join-Path $RuntimeRoot 'InputDiagnostics\asset-library-import.json')
    if ($null -eq $diagnostic) { return $false }
    return [string](Get-PropertyValue $diagnostic 'source_kind') -ceq 'synthetic-directory-recursive' -and
        [int](Get-PropertyValue $diagnostic 'selected_file_count') -eq 12 -and
        [int](Get-PropertyValue $diagnostic 'imported_count') -eq 12 -and
        [int](Get-PropertyValue $diagnostic 'failed_count') -eq 0 -and
        [int](Get-PropertyValue $diagnostic 'repository_asset_count_before') -eq 0 -and
        [int](Get-PropertyValue $diagnostic 'repository_asset_count_after') -eq 12 -and
        [int](Get-PropertyValue $diagnostic 'asset_grid_item_count') -eq 12
}

function Get-WorkspaceSnapshots {
    param($Document)
    if ($null -eq $Document) { return @() }
    return @(Get-PropertyValue $Document 'workspace_restore_snapshots')
}

function Get-NewKeyTransitionMatches {
    param($Document, [string[]]$BaselineAttemptIds, [string]$ControlId, [string]$Key, [string]$Kind, [double]$Boundary, [string]$Direction)
    $matches = @()
    if ($null -eq $Document) { return $matches }
    $attempts = @(Get-PropertyValue $Document 'key_attempts')
    $transitions = @(Get-PropertyValue $Document 'control_state_transitions')
    foreach ($attempt in $attempts) {
        $attemptId = [string](Get-PropertyValue $attempt 'attempt_id')
        if ($attemptId -in $BaselineAttemptIds -or -not (Test-KeyLayersLocal $attempt $ControlId $Key)) { continue }
        $linked = @($transitions | Where-Object {
                [string](Get-PropertyValue $_ 'correlated_key_attempt_id') -ceq $attemptId -and
                [string](Get-NestedValue $_ @('control', 'automation_id')) -ceq $ControlId -and
                [string](Get-PropertyValue $_ 'input_key') -ceq $Key
            })
        foreach ($transition in $linked) {
            $before = [double](Get-PropertyValue $transition 'before_actual_value')
            $after = [double](Get-PropertyValue $transition 'after_actual_value')
            $persisted = [double](Get-PropertyValue $transition 'after_persisted_value')
            $layers = [bool](Get-PropertyValue $transition 'layer1_win32_confirmed') -and
                [bool](Get-PropertyValue $transition 'layer2_wpf_confirmed') -and
                [bool](Get-PropertyValue $transition 'layer3_target_confirmed') -and
                [bool](Get-PropertyValue $transition 'layer4_action_confirmed') -and
                [bool](Get-PropertyValue $transition 'settings_write_back_confirmed')
            if (-not $layers -or [Math]::Abs($after - $persisted) -gt 0.5) { continue }
            $accepted = if ($Kind -ceq 'boundary') {
                [string](Get-PropertyValue $transition 'result') -ceq 'BoundaryNoOpConfirmed' -and
                [bool](Get-PropertyValue $transition 'boundary_no_op_confirmed') -and
                -not [bool](Get-PropertyValue $transition 'state_changed') -and
                [Math]::Abs($before - $Boundary) -le 0.5 -and [Math]::Abs($after - $Boundary) -le 0.5
            } else {
                [string](Get-PropertyValue $transition 'result') -ceq 'Confirmed' -and
                [bool](Get-PropertyValue $transition 'state_changed') -and
                [bool](Get-PropertyValue $transition 'settings_state_changed') -and
                (($Direction -ceq 'increase' -and $after -gt $before) -or ($Direction -ceq 'decrease' -and $after -lt $before))
            }
            if ($accepted) { $matches += [pscustomobject]@{ Attempt = $attempt; Transition = $transition } }
        }
    }
    return $matches
}

function Wait-KeyTransitionStep {
    param($Session, [string]$StepId, [string]$Instruction, [string]$ControlId, [string]$Key, [string]$Kind, [double]$Boundary = 0, [string]$Direction = '')
    $beforeDocument = Get-PhysicalDocument $Session.Root
    $baselineAttemptIds = @(@(Get-PropertyValue $beforeDocument 'key_attempts') | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') })
    $result = Wait-ForStep $StepId $Instruction -Session $Session -Probe {
        $document = Get-PhysicalDocument $Session.Root
        $matches = @(Get-NewKeyTransitionMatches $document $baselineAttemptIds $ControlId $Key $Kind $Boundary $Direction)
        if ($matches.Count -gt 1) { return New-ProbeResult $false '' "$ControlId 收到超过一次 $Key；本轮不能继续" $true }
        if ($matches.Count -eq 0) { return New-ProbeResult $false '' "等待 $ControlId 的一次真实 $Key 四层证据" }
        $transition = $matches[0].Transition
        $after = [double](Get-PropertyValue $transition 'after_actual_value')
        New-ProbeResult $true ([string](Get-PropertyValue $transition 'transition_id')) "$ControlId：$Key 已通过，当前值 $after"
    }
    return $result
}

function Wait-DragStep {
    param($Session, [string]$StepId, [string]$Instruction, [string]$ControlId, [double]$Minimum, [double]$Maximum, [string]$Target)
    $before = Get-PhysicalDocument $Session.Root
    $baselineIds = @(@(Get-PropertyValue $before 'control_state_transitions') | ForEach-Object { [string](Get-PropertyValue $_ 'transition_id') })
    return Wait-ForStep $StepId $Instruction -Session $Session -Probe {
        $document = Get-PhysicalDocument $Session.Root
        $matches = @(@(Get-PropertyValue $document 'control_state_transitions') | Where-Object {
                [string](Get-PropertyValue $_ 'transition_id') -notin $baselineIds -and
                [string](Get-PropertyValue $_ 'input_kind') -ceq 'MouseDrag' -and
                [string](Get-NestedValue $_ @('control', 'automation_id')) -ceq $ControlId -and
                $null -ne (Get-PropertyValue $_ 'completed_at')
            })
        if ($matches.Count -eq 0) { return New-ProbeResult $false '' "等待真实拖动 $ControlId；允许范围 $Minimum–$Maximum" }
        $latest = $matches[-1]
        $actual = [double](Get-PropertyValue $latest 'after_actual_value')
        $persisted = [double](Get-PropertyValue $latest 'after_persisted_value')
        $atTarget = switch ($Target) {
            'minimum' { [Math]::Abs($actual - $Minimum) -le 0.5 }
            'maximum' { [Math]::Abs($actual - $Maximum) -le 0.5 }
            default { $actual -gt ($Minimum + 20) -and $actual -lt ($Maximum - 20) }
        }
        $passed = $atTarget -and [Math]::Abs($actual - $persisted) -le 0.5
        New-ProbeResult $passed ([string](Get-PropertyValue $latest 'transition_id')) "目标 $Target；当前实际/持久值 $actual/$persisted；允许范围 $Minimum–$Maximum"
    }
}

function Wait-PaneToggleStep {
    param($Session, [string]$StepId, [string]$Instruction, [string]$Prefix, [bool]$Collapsed, [double]$ExpectedWidth)
    $before = Get-PhysicalDocument $Session.Root
    $baselineCount = @(Get-WorkspaceSnapshots $before).Count
    return Wait-ForStep $StepId $Instruction -Session $Session -Probe {
        $document = Get-PhysicalDocument $Session.Root
        $snapshots = @(Get-WorkspaceSnapshots $document)
        if ($snapshots.Count -le $baselineCount) { return New-ProbeResult $false '' "等待 $Prefix 栏布局快照" }
        $matches = @($snapshots | Select-Object -Skip $baselineCount | Where-Object {
                [bool](Get-PropertyValue $_ "${Prefix}_collapsed") -eq $Collapsed
            })
        if ($matches.Count -eq 0) { return New-ProbeResult $false '' "等待 $Prefix collapsed=$Collapsed" }
        $latest = $matches[-1]
        $persisted = [double](Get-PropertyValue $latest "${Prefix}_persisted_width")
        $actual = [double](Get-PropertyValue $latest "${Prefix}_actual_width")
        $visible = [bool](Get-PropertyValue $latest "${Prefix}_visible")
        $passed = [Math]::Abs($persisted - $ExpectedWidth) -le 0.5 -and
            (($Collapsed -and -not $visible -and $actual -le 0.5) -or (-not $Collapsed -and $visible -and [Math]::Abs($actual - $persisted) -le 0.5))
        New-ProbeResult $passed ([string](Get-PropertyValue $latest 'timestamp')) "$Prefix collapsed=$Collapsed，实际/持久宽度 $actual/$persisted"
    }
}

function Capture-WindowEvidence {
    param($Session, [string]$CaptureName, [string]$OutputDirectory)
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    $logs = Join-Path $script:runRoot 'logs\captures'
    [IO.Directory]::CreateDirectory($logs) | Out-Null
    $shellPath = (Get-Process -Id $PID).Path
    $arguments = @(
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', (Quote-ProcessArgument $captureTool),
        '-ProcessId', [string]$Session.Process.Id,
        '-ExecutablePath', (Quote-ProcessArgument $script:resolvedExecutable),
        '-WindowTitle', (Quote-ProcessArgument $windowTitle),
        '-ExpectedWindowHwnd', $Session.Hwnd,
        '-OutputRoot', (Quote-ProcessArgument $OutputDirectory),
        '-CaptureName', $CaptureName,
        '-CaptureMethod', 'ScreenPixels',
        '-PreCaptureTimeoutSeconds', [string]$StepTimeoutSeconds,
        '-PreCaptureStableMilliseconds', [string]$ForegroundStableMilliseconds
    )
    Write-Host ("自动捕获 {0}：只读等待同一窗口恢复前台并连续稳定；不会自动抢焦点。" -f $CaptureName) -ForegroundColor DarkYellow
    $exitCode = Invoke-HiddenProcess $shellPath $arguments (Join-Path $logs "$CaptureName.stdout.txt") (Join-Path $logs "$CaptureName.stderr.txt")
    if ($exitCode -ne 0) { throw "自动捕获 '$CaptureName' 失败，exit=$exitCode。软件必须保持前台且窗口稳定。" }
    $recordPath = Join-Path $OutputDirectory "$CaptureName.window-evidence.json"
    $record = Read-JsonFileSafely $recordPath
    if ($null -eq $record) { throw "捕获没有生成窗口证据：$recordPath" }
    $preCaptureGate = Get-PropertyValue $record 'pre_capture_gate'
    if ([int](Get-NestedValue $record @('process', 'process_id')) -ne $Session.Process.Id -or
        -not (Test-PropertyPresent $record 'ui_input_generated') -or
        -not [object]::Equals((Get-PropertyValue $record 'ui_input_generated'), $false) -or
        -not (Test-PropertyPresent $record 'synthetic_ui_events_generated') -or
        -not [object]::Equals((Get-PropertyValue $record 'synthetic_ui_events_generated'), $false) -or
        $null -eq $preCaptureGate -or
        -not (Test-PropertyPresent $preCaptureGate 'passed') -or
        -not [object]::Equals((Get-PropertyValue $preCaptureGate 'passed'), $true) -or
        -not (Test-PropertyPresent $preCaptureGate 'exact_original_hwnd_required') -or
        -not [object]::Equals((Get-PropertyValue $preCaptureGate 'exact_original_hwnd_required'), $true) -or
        -not (Test-PropertyPresent $preCaptureGate 'ui_activation_attempted') -or
        -not [object]::Equals((Get-PropertyValue $preCaptureGate 'ui_activation_attempted'), $false) -or
        [int](Get-PropertyValue $preCaptureGate 'timeout_seconds') -ne $StepTimeoutSeconds -or
        [int](Get-PropertyValue $preCaptureGate 'required_stable_milliseconds') -ne $ForegroundStableMilliseconds -or
        [string](Get-NestedValue $record @('expected', 'window_hwnd')) -cne $Session.Hwnd -or
        [string](Get-NestedValue $record @('window_before_capture', 'hwnd')) -cne $Session.Hwnd -or
        [string](Get-NestedValue $record @('window_after_capture', 'hwnd')) -cne $Session.Hwnd -or
        -not [bool](Get-NestedValue $record @('window_before_capture', 'is_foreground')) -or
        -not [bool](Get-NestedValue $record @('window_after_capture', 'is_foreground')) -or
        [bool](Get-NestedValue $record @('window_before_capture', 'is_minimized')) -or
        [bool](Get-NestedValue $record @('window_after_capture', 'is_minimized')) -or
        -not [bool](Get-NestedValue $record @('verification', 'passed')) -or
        -not [string]::Equals([string](Get-NestedValue $record @('process', 'executable_path')), $script:resolvedExecutable, [StringComparison]::OrdinalIgnoreCase) -or
        [string](Get-NestedValue $record @('process', 'executable_sha256')) -cne $script:executableHash) {
        throw "捕获 '$CaptureName' 的稳定门/零输入声明/PID/HWND/前台状态/路径/哈希/验证结果与本会话不一致。"
    }
    Add-ManifestEvent "capture-$CaptureName" 'captured' '后台自动捕获' $recordPath
    return $record
}

function Get-NewTransitionWidth {
    param($WaitResult)
    return [double](Get-PropertyValue $WaitResult.Transition 'after_persisted_value')
}

function Invoke-RecoveryCheck {
    param([string]$Reason)
    $observed = Get-DisplayObservation
    if (Test-DisplayMatches $observed $baselineDisplay $true) {
        $script:displayRestored = $true
        return $true
    }
    Write-Warning "$Reason。请只在 Windows 显示设置中恢复 3840x2160@60Hz / 150%；脚本只读观察，不会自动修改显示。"
    try {
        Wait-ForDisplay 'recovery-display-baseline' '请在 Windows 显示设置中恢复 3840x2160@60Hz / 150%。' $baselineDisplay $true | Out-Null
        $script:displayRestored = $true
        return $true
    } catch {
        $script:displayRestored = $false
        Write-Warning "仍未真实观察到基线显示，display_restored 保持 false：$($_.Exception.Message)"
        return $false
    }
}

function Invoke-DryRun {
    $packetFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -File)
    $onlyEntry = $packetFiles.Count -eq 1 -and $packetFiles[0].Name -ceq 'Invoke-P1AssetLibraryGateAManualAcceptance.ps1'
    if (-not $onlyEntry -or @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.bat').Count -ne 0) {
        throw '人工包目录必须且只能包含唯一 PowerShell 入口，且不得包含 BAT。'
    }
    $script:manifest['status'] = 'dry-run-passed'
    $script:manifest['ui_started'] = $false
    $script:manifest['display_changed'] = $false
    $script:manifest['validator_started'] = $false
    $script:manifest['build_started'] = $false
    $script:manifest['repository_tracked_clean_observed'] = [string]::IsNullOrWhiteSpace((Get-WorktreeStatus))
    $script:manifest['dry_run_checks'] = [ordered]@{
        unique_ps1_entry = $true
        bat_absent = $true
        required_tools_present = $true
        contract_json_parsed = $true
        run_requires_dynamic_head_match = $true
        run_requires_tracked_clean = $true
        run_builds_dedicated_executable = $true
        display_observation_only = $true
        formal_validator_not_started = $true
    }
    Save-Manifest
    Write-Output $script:manifestPath
}

function Invoke-RecoveryTest {
    Initialize-NativeObservation
    $beforeDevPreview = @(Get-Process -Name $expectedProcessName -ErrorAction SilentlyContinue).Count
    $residualQueue = [Collections.Queue]::new()
    $residualQueue.Enqueue([pscustomobject]@{ Count = 1; ProcessIds = @(41001) })
    $residualQueue.Enqueue([pscustomobject]@{ Count = 0; ProcessIds = @() })
    $residualThenZero = Wait-ForStableEmptyProcessSnapshot -Context 'RecoveryTest residual-then-zero' `
        -TimeoutMilliseconds 600 -StableMilliseconds 80 -PollMilliseconds 20 -SnapshotProvider {
            if ($residualQueue.Count -gt 0) { return $residualQueue.Dequeue() }
            return [pscustomobject]@{ Count = 0; ProcessIds = @() }
        }
    $reappearanceQueue = [Collections.Queue]::new()
    $reappearanceQueue.Enqueue([pscustomobject]@{ Count = 0; ProcessIds = @() })
    $reappearanceQueue.Enqueue([pscustomobject]@{ Count = 0; ProcessIds = @() })
    $reappearanceQueue.Enqueue([pscustomobject]@{ Count = 1; ProcessIds = @(41002) })
    $reappearanceQueue.Enqueue([pscustomobject]@{ Count = 0; ProcessIds = @() })
    $reappearanceReset = Wait-ForStableEmptyProcessSnapshot -Context 'RecoveryTest zero-reappears-zero' `
        -TimeoutMilliseconds 700 -StableMilliseconds 80 -PollMilliseconds 20 -SnapshotProvider {
            if ($reappearanceQueue.Count -gt 0) { return $reappearanceQueue.Dequeue() }
            return [pscustomobject]@{ Count = 0; ProcessIds = @() }
        }
    $persistentNonzeroRejected = $false
    $persistentNonzeroError = ''
    try {
        [void](Wait-ForStableEmptyProcessSnapshot -Context 'RecoveryTest persistent-nonzero' `
                -TimeoutMilliseconds 180 -StableMilliseconds 80 -PollMilliseconds 20 -SnapshotProvider {
                [pscustomobject]@{ Count = 1; ProcessIds = @(41003) }
            })
    } catch {
        $persistentNonzeroError = $_.Exception.Message
        $persistentNonzeroRejected = $persistentNonzeroError -like '*41003*'
    }
    $realDevPreviewStableZero = Wait-ForNoExistingDevPreview -Context 'RecoveryTest live DevPreview preflight' `
        -TimeoutMilliseconds 3000 -StableMilliseconds 300 -PollMilliseconds 50
    $environmentKey = 'PIXEL_TART_ASSET_LIBRARY_P1_HEAD'
    $sentinel = [Environment]::GetEnvironmentVariable($environmentKey, 'Process')
    $environmentRestored = $false
    try {
        try {
            Invoke-WithEnvironment @{ $environmentKey = '0000000000000000000000000000000000000000' } { throw 'recovery-test-sentinel' } | Out-Null
        } catch {
            if ($_.Exception.Message -cne 'recovery-test-sentinel') { throw }
        }
        $environmentRestored = [string]::Equals([Environment]::GetEnvironmentVariable($environmentKey, 'Process'), $sentinel, [StringComparison]::Ordinal)
    } finally {
        [Environment]::SetEnvironmentVariable($environmentKey, $sentinel, 'Process')
    }
    $nodeReuseKey = 'MSBUILDDISABLENODEREUSE'
    $nodeReuseSentinel = [Environment]::GetEnvironmentVariable($nodeReuseKey, 'Process')
    $nodeReuseOverrideObserved = $false
    $nodeReuseEnvironmentRestored = $false
    try {
        $nodeReuseOverrideObserved = [bool](Invoke-WithEnvironment @{ $nodeReuseKey = '1' } {
                [string]::Equals([Environment]::GetEnvironmentVariable($nodeReuseKey, 'Process'), '1', [StringComparison]::Ordinal)
            })
        $nodeReuseEnvironmentRestored = [string]::Equals(
            [Environment]::GetEnvironmentVariable($nodeReuseKey, 'Process'),
            $nodeReuseSentinel,
            [StringComparison]::Ordinal)
    } finally {
        [Environment]::SetEnvironmentVariable($nodeReuseKey, $nodeReuseSentinel, 'Process')
    }
    $shellPath = (Get-Process -Id $PID).Path
    $helper = Start-Process -FilePath $shellPath -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', (Quote-ProcessArgument 'Start-Sleep -Seconds 30')) -PassThru -WindowStyle Hidden
    Stop-SessionForCleanup ([pscustomobject]@{ Process = $helper })
    $processCleanup = $helper.HasExited
    $trueCase = Test-DisplayMatches ([pscustomobject]@{ width=3840; height=2160; refresh_rate_hz=60; scale_percent=150 }) $baselineDisplay $true
    $falseCase = Test-DisplayMatches ([pscustomobject]@{ width=1920; height=1080; refresh_rate_hz=60; scale_percent=150 }) $baselineDisplay $true
    $retryGuardRoot = Join-Path $script:runRoot 'recovery-test-retry-guard'
    [IO.Directory]::CreateDirectory((Join-Path $retryGuardRoot 'InputDiagnostics')) | Out-Null
    $emptyPhysical = [pscustomobject]@{ attempts = @(); key_attempts = @() }
    $attemptOneData = [pscustomobject]@{ Snapshots = @([pscustomobject]@{ attempt = 1; stage = 'error-visible' }) }
    $attemptTwoData = [pscustomobject]@{ Snapshots = @([pscustomobject]@{ attempt = 1; stage = 'error-visible' }, [pscustomobject]@{ attempt = 2; stage = 'loading-entered' }) }
    $retryGuardCleanCase = [string]::IsNullOrWhiteSpace((Get-RetrySessionContamination $attemptOneData $emptyPhysical $retryGuardRoot))
    $retryGuardAttemptTwoCase = -not [string]::IsNullOrWhiteSpace((Get-RetrySessionContamination $attemptTwoData $emptyPhysical $retryGuardRoot))
    Write-Utf8NoBom (Join-Path $retryGuardRoot 'InputDiagnostics\asset-library-import.json') (([ordered]@{
                picker_accepted = $true
                selected_file_count = 1
                import_command_entered = $true
                import_service_entered = $true
                imported_count = 1
                source_kind = 'file-picker'
            }) | ConvertTo-Json -Depth 4)
    $retryGuardImportCase = -not [string]::IsNullOrWhiteSpace((Get-RetrySessionContamination $attemptOneData $emptyPhysical $retryGuardRoot))
    $observed = Get-DisplayObservation
    $afterDevPreview = @(Get-Process -Name $expectedProcessName -ErrorAction SilentlyContinue).Count
    $recoveryFailures = [Collections.Generic.List[string]]::new()
    if ($residualThenZero.StableMilliseconds -lt 80) { $recoveryFailures.Add('residual-then-zero stability') }
    if ($reappearanceReset.StableMilliseconds -lt 80 -or $reappearanceReset.ElapsedMilliseconds -lt 120) {
        $recoveryFailures.Add('reappearance stability reset')
    }
    if (-not $persistentNonzeroRejected) { $recoveryFailures.Add("persistent nonzero rejection [$persistentNonzeroError]") }
    if ($realDevPreviewStableZero.StableMilliseconds -lt 300) { $recoveryFailures.Add('live process-table stable zero') }
    if (-not $environmentRestored) { $recoveryFailures.Add('environment restoration') }
    if (-not $nodeReuseOverrideObserved -or -not $nodeReuseEnvironmentRestored) { $recoveryFailures.Add('MSBuild node-reuse environment isolation') }
    if (-not $processCleanup) { $recoveryFailures.Add('helper process cleanup') }
    if (-not $trueCase -or $falseCase) { $recoveryFailures.Add('display evaluator') }
    if (-not $retryGuardCleanCase -or -not $retryGuardAttemptTwoCase -or -not $retryGuardImportCase) { $recoveryFailures.Add('retry contamination guard') }
    if ($beforeDevPreview -ne $afterDevPreview) { $recoveryFailures.Add('DevPreview process count unchanged') }
    if ($recoveryFailures.Count -ne 0) {
        throw "RecoveryTest failed: $($recoveryFailures -join '; ')."
    }
    $script:manifest['status'] = 'recovery-test-passed'
    $script:manifest['ui_started'] = $false
    $script:manifest['display_changed'] = $false
    $script:manifest['validator_started'] = $false
    $script:manifest['build_started'] = $false
    $script:manifest['display_restored'] = Test-DisplayMatches $observed $baselineDisplay $true
    $script:manifest['recovery_test'] = [ordered]@{
        environment_restored = $environmentRestored
        msbuild_node_reuse_override_observed = $nodeReuseOverrideObserved
        msbuild_node_reuse_environment_restored = $nodeReuseEnvironmentRestored
        helper_process_cleanup_verified = $processCleanup
        display_baseline_true_case = $trueCase
        display_nonbaseline_false_case = -not $falseCase
        display_observer_read_only = $true
        devpreview_process_count_unchanged = $true
        devpreview_residual_then_stable_zero_verified = $true
        devpreview_reappearance_resets_stability_verified = $true
        devpreview_persistent_nonzero_rejected = $persistentNonzeroRejected
        devpreview_live_process_tables_stable_zero_verified = $true
        retry_guard_clean_attempt_one_allowed = $retryGuardCleanCase
        retry_guard_early_attempt_two_rejected = $retryGuardAttemptTwoCase
        retry_guard_file_picker_import_rejected = $retryGuardImportCase
        observed_display = $observed
    }
    Save-Manifest
    Write-Output $script:manifestPath
}

function Invoke-RealRun {
    Assert-TrackedCleanAndHead $SourceHead 'Run 启动前'
    Assert-NoExistingDevPreview
    [PixelTartManualPacketCancellationV2]::Install()
    $script:manifest['status'] = 'building'
    $script:manifest['repository_tracked_clean'] = $true
    Save-Manifest
    Invoke-DedicatedBuild
    $script:manifest['status'] = 'running'
    $script:manifest['ui_started'] = $false
    $displayAtStart = Get-DisplayObservation
    $script:manifest['display_before'] = $displayAtStart
    Save-Manifest

    if (-not (Test-DisplayMatches $displayAtStart $baselineDisplay $true)) {
        Wait-ForDisplay 'initial-display-baseline' '请在 Windows 显示设置中恢复 3840x2160@60Hz / 150%。' $baselineDisplay $true | Out-Null
    }
    $script:displayRestored = $true

    $stateEvidenceRoot = Join-Path $script:runRoot 'evidence\states'
    $firstRoot = Join-Path $script:runRoot 'sessions\first-empty'
    $first = Start-AcceptanceSession 'first-empty-session' $firstRoot 'first-empty/v1' ''
    $script:manifest['ui_started'] = $true; Save-Manifest
    Wait-ForStep 'first-empty-ready' '将像素蛋挞置于前台并保持不动。首次空库准备好后会自动捕获 08。' -Session $first -Probe {
        $data = Get-StateSessionData $first.Root
        $passed = (Test-StateManifestIdentity $data $first 'first-empty/v1') -and
            (Test-StateTimelineComplete $data 'first-empty-session') -and (Test-ReadySnapshot $data 1)
        New-ProbeResult $passed 'first-empty-ready-attempt-1' '首次空库：attempt 1 完整真实仓储 timeline + ready/0 项'
    } | Out-Null
    Capture-WindowEvidence $first '08-first-empty-manual-v2' $stateEvidenceRoot | Out-Null
    Wait-ForExpectedClose $first 'close-first-empty' '请关闭当前像素蛋挞窗口。'

    $retryRoot = Join-Path $script:runRoot 'sessions\retry'
    $retry = Start-AcceptanceSession 'retry-session' $retryRoot 'loading-error-retry-empty/v1' ''
    Wait-ForStep 'retry-loading-held' '将像素蛋挞置于前台并保持不动。真实 Loading 屏障稳定后会自动捕获 09。' -Session $retry -Probe {
        $data = Get-StateSessionData $retry.Root
        $waiting = @($data.Controller | Where-Object { [string](Get-PropertyValue $_ 'stage') -ceq 'loading-barrier-waiting' -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
        $loading = @($data.Snapshots | Where-Object { [string](Get-PropertyValue $_ 'stage') -ceq 'loading-entered' -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
        $passed = (Test-StateManifestIdentity $data $retry 'loading-error-retry-empty/v1') -and $waiting.Count -eq 1 -and $loading.Count -eq 1
        New-ProbeResult $passed 'loading-barrier-waiting-1' 'Loading：attempt 1 正在真实 release gate 前等待'
    } | Out-Null
    Capture-WindowEvidence $retry '09-loading-manual-v2' $stateEvidenceRoot | Out-Null
    $scenarioManifest = (Get-StateSessionData $retry.Root).Manifest
    $releaseFile = [string](Get-PropertyValue $scenarioManifest 'releaseFile')
    if (-not (Test-WindowsAbsolutePath $releaseFile) -or
        -not [IO.Path]::GetFullPath($releaseFile).StartsWith(([IO.Path]::GetFullPath($retry.Root).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw '状态控制器 release gate 路径缺失或越出 retry 会话根。'
    }
    Write-NewUtf8NoBom $releaseFile 'release'
    Add-ManifestEvent 'release-loading-gate' 'passed' '脚本创建契约指定 release gate；不生成 UI 输入' $releaseFile

    $retryBeforeError = Get-PhysicalDocument $retry.Root
    $retryPreErrorActivationIds = @(@(Get-QualifiedRetryActivations $retryBeforeError) | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') })
    Wait-ForStep 'retry-error-ready' '将像素蛋挞保持在前台且不要点击任何控件。真实错误态稳定后会自动捕获 10。' -Session $retry -Probe {
        $data = Get-StateSessionData $retry.Root
        $document = Get-PhysicalDocument $retry.Root
        $contamination = Get-RetrySessionContamination $data $document $retry.Root $retryPreErrorActivationIds
        if (-not [string]::IsNullOrWhiteSpace($contamination)) {
            return New-ProbeResult $false '' $contamination $true
        }
        $errors = @($data.Snapshots | Where-Object { [string](Get-PropertyValue $_ 'stage') -ceq 'error-visible' -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
        $failed = @($data.Snapshots | Where-Object { [string](Get-PropertyValue $_ 'stage') -ceq 'initial-query-failed' -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
        $passed = $errors.Count -eq 1 -and $failed.Count -eq 1
        New-ProbeResult $passed 'error-visible-attempt-1' '真实可恢复错误态：attempt 1；尚未触发 Retry，尚未导入素材'
    } | Out-Null
    Capture-WindowEvidence $retry '10-recoverable-error-manual-v2' $stateEvidenceRoot | Out-Null

    $retryBefore = Get-PhysicalDocument $retry.Root
    $retryBaselineIds = @(@(Get-PropertyValue $retryBefore 'key_attempts') | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') }) +
        @(@(Get-PropertyValue $retryBefore 'attempts') | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') })
    Wait-ForStep 'retry-activate-once' '优先键盘：用 Tab/Shift+Tab 聚焦“重试”后，只按一次 Enter；若焦点难以判断，可改用鼠标只单击一次“重试”。两种方式只能选一种，完成后停手。' -Session $retry -Probe {
        $data = Get-StateSessionData $retry.Root
        $document = Get-PhysicalDocument $retry.Root
        $import = Read-JsonFileSafely (Join-Path $retry.Root 'InputDiagnostics\asset-library-import.json')
        if ($null -ne $import -and (
            [bool](Get-PropertyValue $import 'picker_accepted') -or
            [bool](Get-PropertyValue $import 'import_command_entered') -or
            [bool](Get-PropertyValue $import 'import_service_entered') -or
            [int](Get-PropertyValue $import 'selected_file_count') -gt 0 -or
            [int](Get-PropertyValue $import 'imported_count') -gt 0 -or
            -not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $import 'source_kind')))) {
            return New-ProbeResult $false '' '检测到文件选择或导入；本轮已失去 synthetic-only 资格，立即停止。' $true
        }
        $activations = @(Get-QualifiedRetryActivations $document | Where-Object { [string](Get-PropertyValue $_ 'attempt_id') -notin $retryBaselineIds })
        if ($activations.Count -gt 1) { return New-ProbeResult $false '' '重试收到超过一次合格真实激活；本轮必须失败' $true }
        $attemptThreeOrLater = @($data.Snapshots | Where-Object { [int](Get-PropertyValue $_ 'attempt') -ge 3 })
        if ($attemptThreeOrLater.Count -gt 0) { return New-ProbeResult $false '' '检测到 attempt 3 或更晚重试；本轮必须失败' $true }
        $attemptTwo = @($data.Snapshots | Where-Object { [int](Get-PropertyValue $_ 'attempt') -ge 2 })
        if ($attemptTwo.Count -gt 0 -and $activations.Count -eq 0) {
            $attemptTwoAt = [DateTimeOffset]::MaxValue
            foreach ($snapshot in $attemptTwo) {
                $candidate = [DateTimeOffset]::MinValue
                if ([DateTimeOffset]::TryParse([string](Get-PropertyValue $snapshot 'recordedAt'), [ref]$candidate) -and $candidate -lt $attemptTwoAt) {
                    $attemptTwoAt = $candidate
                }
            }
            if ($attemptTwoAt -ne [DateTimeOffset]::MaxValue -and ([DateTimeOffset]::Now - $attemptTwoAt).TotalSeconds -le 3) {
                return New-ProbeResult $false '' 'attempt 2 已真实开始；在固定 3 秒诊断落盘窗口内等待同一物理按键的 WM_KEYUP 收尾'
            }
            return New-ProbeResult $false '' 'attempt 2 已开始，但没有本步骤唯一真实 Retry 四层激活；立即停止。' $true
        }
        $passed = $activations.Count -eq 1 -and (Test-StateTimelineComplete $data 'retry-session') -and (Test-ReadySnapshot $data 2)
        New-ProbeResult $passed $(if ($activations.Count -eq 1) { [string](Get-PropertyValue $activations[0] 'attempt_id') } else { '' }) '唯一真实 Retry 四层激活 + attempt 2 真实仓储 ready/0 项'
    } | Out-Null
    Capture-WindowEvidence $retry '11-retry-recovered-manual-v2' $stateEvidenceRoot | Out-Null
    Wait-ForExpectedClose $retry 'close-retry-session' '请关闭当前像素蛋挞窗口。'

    $fixtureRoot = Join-Path $script:runRoot 'synthetic-fixture'
    & $fixtureTool -OutputRoot $fixtureRoot | Out-Null
    $regularRoot = Join-Path $script:runRoot 'sessions\regular'
    $regular = Start-AcceptanceSession 'keyboard-session' $regularRoot '' (Join-Path $fixtureRoot 'images')
    Wait-ForStep 'regular-direct-import' '将像素蛋挞置于前台并最大化；不要点击一级导航，验收直达会自动进入素材库。' -Session $regular -RequireMaximized:$true -Probe {
        $route = Get-RouteSession $regular.Root
        $passed = (Test-RouteSessionApplied $route $regular 1) -and (Test-SyntheticImportComplete $regular.Root)
        New-ProbeResult $passed 'route-1-import-12' '普通验收直达已 applied，真实 synthetic import 0→12，网格 12 项'
    } | Out-Null
    $keyboardEvidenceRoot = Join-Path $script:runRoot 'evidence\keyboard'
    Capture-WindowEvidence $regular 'keyboard-splitters-start-v2' $keyboardEvidenceRoot | Out-Null

    Wait-DragStep $regular 'org-drag-min' '把组织栏分隔条拖到最左边，直到最小宽度 180。' 'AssetOrganizationSplitter' 180 420 'minimum' | Out-Null
    Wait-KeyTransitionStep $regular 'org-min-left' '不要再拖动；在组织栏分隔条上只按一次 Left。' 'AssetOrganizationSplitter' 'Left' 'boundary' 180 | Out-Null
    Wait-DragStep $regular 'org-drag-max' '把组织栏分隔条拖到最右边，直到最大宽度 420。' 'AssetOrganizationSplitter' 180 420 'maximum' | Out-Null
    Wait-KeyTransitionStep $regular 'org-max-right' '不要再拖动；在组织栏分隔条上只按一次 Right。' 'AssetOrganizationSplitter' 'Right' 'boundary' 420 | Out-Null
    Wait-DragStep $regular 'org-drag-middle' '把组织栏分隔条拖到 180–420 之间的中间位置。' 'AssetOrganizationSplitter' 180 420 'middle' | Out-Null
    $orgRight = Wait-KeyTransitionStep $regular 'org-middle-right' '在组织栏分隔条上只按一次 Right。' 'AssetOrganizationSplitter' 'Right' 'regular' 0 'increase'
    $orgLeft = Wait-KeyTransitionStep $regular 'org-middle-left' '在组织栏分隔条上只按一次 Left。' 'AssetOrganizationSplitter' 'Left' 'regular' 0 'decrease'
    $orgWidth = Get-NewTransitionWidth $orgLeft
    Wait-PaneToggleStep $regular 'org-collapse' '只点击一次“收起组织栏”。' 'organization' $true $orgWidth | Out-Null
    Wait-PaneToggleStep $regular 'org-expand' '只点击一次“展开组织栏”。' 'organization' $false $orgWidth | Out-Null

    Wait-DragStep $regular 'inspector-drag-min' '把检查器分隔条拖到最右边，直到检查器最小宽度 260。' 'AssetInspectorSplitter' 260 520 'minimum' | Out-Null
    Wait-KeyTransitionStep $regular 'inspector-min-right' '不要再拖动；在检查器分隔条上只按一次 Right。' 'AssetInspectorSplitter' 'Right' 'boundary' 260 | Out-Null
    Wait-DragStep $regular 'inspector-drag-max' '把检查器分隔条拖到最左边，直到检查器最大宽度 520。' 'AssetInspectorSplitter' 260 520 'maximum' | Out-Null
    Wait-KeyTransitionStep $regular 'inspector-max-left' '不要再拖动；在检查器分隔条上只按一次 Left。' 'AssetInspectorSplitter' 'Left' 'boundary' 520 | Out-Null
    Wait-DragStep $regular 'inspector-drag-middle' '把检查器分隔条拖到 260–520 之间的中间位置。' 'AssetInspectorSplitter' 260 520 'middle' | Out-Null
    $inspectorLeft = Wait-KeyTransitionStep $regular 'inspector-middle-left' '在检查器分隔条上只按一次 Left。' 'AssetInspectorSplitter' 'Left' 'regular' 0 'increase'
    $inspectorRight = Wait-KeyTransitionStep $regular 'inspector-middle-right' '在检查器分隔条上只按一次 Right。' 'AssetInspectorSplitter' 'Right' 'regular' 0 'decrease'
    $inspectorWidth = Get-NewTransitionWidth $inspectorRight
    Wait-PaneToggleStep $regular 'inspector-collapse' '只点击一次“收起检查器”。' 'inspector' $true $inspectorWidth | Out-Null
    Wait-PaneToggleStep $regular 'inspector-expand' '只点击一次“展开检查器”。' 'inspector' $false $inspectorWidth | Out-Null

    Wait-ForStep 'thumbnail-focus' '仅用真实 Tab 或 Shift+Tab，把焦点移动到“缩略图大小”滑块，然后停止。' -Session $regular -Probe {
        $focused = Get-FocusedAutomationObservation
        $passed = $null -ne $focused -and $focused.ProcessId -eq $regular.Process.Id -and $focused.AutomationId -ceq 'AssetThumbnailSizeSlider'
        New-ProbeResult $passed 'thumbnail-slider-focused' "当前焦点 AutomationId=$([string](Get-PropertyValue $focused 'AutomationId'))"
    } | Out-Null
    $sliderBefore = Get-PhysicalDocument $regular.Root
    $sliderBaseline = @(@(Get-PropertyValue $sliderBefore 'key_attempts') | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') })
    Wait-ForStep 'thumbnail-right' '焦点保持在缩略图滑块，只按一次 Right。' -Session $regular -Probe {
        $document = Get-PhysicalDocument $regular.Root
        $matches = @(Get-NewKeyTransitionMatches $document $sliderBaseline 'AssetThumbnailSizeSlider' 'Right' 'regular' 0 'increase')
        if ($matches.Count -gt 1) { return New-ProbeResult $false '' '缩略图滑块收到超过一次 Right' $true }
        if ($matches.Count -eq 0) { return New-ProbeResult $false '' '等待滑块一次 Right 的四层写回证据；范围 120–280' }
        New-ProbeResult $true ([string](Get-PropertyValue $matches[0].Transition 'transition_id')) ("缩略图实际/持久值 {0}/{1}" -f (Get-PropertyValue $matches[0].Transition 'after_actual_value'), (Get-PropertyValue $matches[0].Transition 'after_persisted_value'))
    } | Out-Null
    Capture-WindowEvidence $regular 'keyboard-splitters-complete-v2' $keyboardEvidenceRoot | Out-Null
    Wait-ForExpectedClose $regular 'close-keyboard-session' '请关闭当前像素蛋挞窗口；这一步用于真实保存布局。'

    $physicalRoot = Join-Path $script:runRoot 'evidence\physical'
    $fixtureEvidenceRoot = Join-Path $script:runRoot 'evidence\fixture'
    [IO.Directory]::CreateDirectory($physicalRoot) | Out-Null
    [IO.Directory]::CreateDirectory($fixtureEvidenceRoot) | Out-Null
    $firstDiagnostic = Join-Path $regularRoot 'InputDiagnostics\physical-pointer-session.json'
    $importDiagnostic = Join-Path $regularRoot 'InputDiagnostics\asset-library-import.json'
    if (-not (Test-Path $firstDiagnostic) -or -not (Test-Path $importDiagnostic)) { throw '普通会话缺少 physical-pointer 或 0→12 import 原始诊断。' }
    Copy-Item -LiteralPath $firstDiagnostic -Destination (Join-Path $physicalRoot 'physical-pointer-keyboard-session.json')
    Copy-Item -LiteralPath $importDiagnostic -Destination (Join-Path $fixtureEvidenceRoot 'initial-import-0-to-12.json')

    $restart = Start-AcceptanceSession 'restart-dpi-session' $regularRoot '' '' $true
    Wait-ForStep 'restart-restored' '将重启后的像素蛋挞置于前台并最大化；不要点击一级导航。' -Session $restart -RequireMaximized:$true -Probe {
        $route = Get-RouteSession $restart.Root
        $document = Get-PhysicalDocument $restart.Root
        $previous = Get-PropertyValue $document 'previous_session'
        $restore = @(Get-WorkspaceSnapshots $document | Where-Object {
                [bool](Get-PropertyValue $_ 'restore_confirmed') -and
                [bool](Get-PropertyValue $_ 'restart_comparison_performed') -and
                [bool](Get-PropertyValue $_ 'restart_settings_match_previous_session')
            })
        $passed = (Test-RouteSessionApplied $route $restart 2) -and $null -ne $previous -and [bool](Get-PropertyValue $previous 'has_workspace_state') -and $restore.Count -ge 1
        New-ProbeResult $passed 'restart-route-2-restored' '新 PID 已恢复两栏宽度、折叠状态与缩略图尺寸'
    } | Out-Null
    Capture-WindowEvidence $restart 'keyboard-splitters-restart-restored-v2' $keyboardEvidenceRoot | Out-Null

    $dpiEvidenceRoot = Join-Path $script:runRoot 'evidence\dpi'
    $tuples = @(
        [ordered]@{ token='1366x768-100pct'; width=1366; height=768; scale_percent=100; dpi=96 },
        [ordered]@{ token='1920x1080-125pct'; width=1920; height=1080; scale_percent=125; dpi=120 },
        [ordered]@{ token='1920x1080-150pct'; width=1920; height=1080; scale_percent=150; dpi=144 },
        [ordered]@{ token='2560x1440-175pct'; width=2560; height=1440; scale_percent=175; dpi=168 }
    )
    $script:displayMatrixStarted = $true
    $tupleIndex = 0
    foreach ($tuple in $tuples) {
        $tupleIndex++
        $displayInstruction = "请在 Windows 显示设置中只设置 $($tuple.width)x$($tuple.height) / $($tuple.scale_percent)%；完成后返回像素蛋挞并最大化。"
        Wait-ForStep "dpi-$tupleIndex-observe" $displayInstruction -Session $restart -RequireMaximized:$true -Probe {
            $window = Get-WindowObservation $restart.Process.Id
            if ($null -eq $window) { return New-ProbeResult $false '' '等待精确 DevPreview 窗口' }
            $display = Get-DisplayObservation $window.Handle
            $passed = (Test-DisplayMatches $display $tuple $false) -and $window.Dpi -eq [int]$tuple.dpi
            New-ProbeResult $passed ("$($display.width)x$($display.height)|$($display.scale_percent)|$($window.Dpi)") "当前 $($display.width)x$($display.height) / $($display.scale_percent)% / DPI $($window.Dpi)；脚本未修改显示"
        } | Out-Null
        $defaultRecord = Capture-WindowEvidence $restart ("dpi-{0}-default" -f $tuple.token) $dpiEvidenceRoot
        $document = Get-PhysicalDocument $restart.Root
        $snapshots = @(Get-WorkspaceSnapshots $document)
        if ($snapshots.Count -eq 0) { throw 'DPI 步骤缺少工作区布局快照。' }
        $latestWorkspace = $snapshots[-1]
        $controlId = if ([bool](Get-PropertyValue $latestWorkspace 'organization_visible')) { 'AssetOrganizationSplitter' } else { 'AssetInspectorSplitter' }
        $actualField = if ($controlId -ceq 'AssetOrganizationSplitter') { 'organization_actual_width' } else { 'inspector_actual_width' }
        $minimum = if ($controlId -ceq 'AssetOrganizationSplitter') { 180d } else { 260d }
        $maximum = if ($controlId -ceq 'AssetOrganizationSplitter') { 420d } else { 520d }
        $actual = [double](Get-PropertyValue $latestWorkspace $actualField)
        $key = if ($controlId -ceq 'AssetOrganizationSplitter') {
            if ($actual -ge $maximum - 0.5) { 'Left' } else { 'Right' }
        } else {
            if ($actual -ge $maximum - 0.5) { 'Right' } else { 'Left' }
        }
        $direction = if (($controlId -ceq 'AssetOrganizationSplitter' -and $key -ceq 'Right') -or ($controlId -ceq 'AssetInspectorSplitter' -and $key -ceq 'Left')) { 'increase' } else { 'decrease' }
        Wait-ForStep "dpi-$tupleIndex-focus" "仅用真实 Tab 或 Shift+Tab，把焦点移动到 $controlId，然后停止。" -Session $restart -Probe {
            $focused = Get-FocusedAutomationObservation
            $passed = $null -ne $focused -and $focused.ProcessId -eq $restart.Process.Id -and $focused.AutomationId -ceq $controlId
            New-ProbeResult $passed "focused-$controlId" "当前焦点 $([string](Get-PropertyValue $focused 'AutomationId'))"
        } | Out-Null
        $dpiBefore = Get-PhysicalDocument $restart.Root
        $dpiBaselineIds = @(@(Get-PropertyValue $dpiBefore 'key_attempts') | ForEach-Object { [string](Get-PropertyValue $_ 'attempt_id') })
        Wait-ForStep "dpi-$tupleIndex-key" "焦点保持在 $controlId，只按一次 $key。" -Session $restart -Probe {
            $current = Get-PhysicalDocument $restart.Root
            $matches = @(Get-NewKeyTransitionMatches $current $dpiBaselineIds $controlId $key 'regular' 0 $direction)
            if ($matches.Count -gt 1) { return New-ProbeResult $false '' "该 tuple 收到超过一次 $key" $true }
            if ($matches.Count -eq 0) { return New-ProbeResult $false '' "等待一次真实 $key：实际值必须在 $minimum–$maximum 内变化并写回" }
            $transition = $matches[0].Transition
            $defaultAt = [DateTimeOffset]::Parse([string](Get-PropertyValue $defaultRecord 'captured_at_utc'))
            $startedAt = [DateTimeOffset]::Parse([string](Get-PropertyValue $transition 'started_at'))
            $completedAt = [DateTimeOffset]::Parse([string](Get-PropertyValue $transition 'completed_at'))
            $passed = $startedAt -gt $defaultAt -and $completedAt -gt $startedAt
            New-ProbeResult $passed ([string](Get-PropertyValue $transition 'transition_id')) "真实 $key 四层 transition 位于 default capture 之后"
        } | Out-Null
        Capture-WindowEvidence $restart ("dpi-{0}-interaction" -f $tuple.token) $dpiEvidenceRoot | Out-Null
    }

    Wait-ForDisplay 'restore-final-display' '请在 Windows 显示设置中恢复 3840x2160@60Hz / 150%。' $baselineDisplay $true | Out-Null
    $script:displayRestored = $true
    Wait-ForStep 'restore-final-foreground' '返回像素蛋挞，将窗口最大化并保持不动。' -Session $restart -RequireMaximized:$true -Probe {
        $window = Get-WindowObservation $restart.Process.Id
        if ($null -eq $window) { return New-ProbeResult $false '' '等待 DevPreview 前台窗口' }
        $display = Get-DisplayObservation $window.Handle
        $passed = (Test-DisplayMatches $display $baselineDisplay $true) -and $window.Dpi -eq 144
        New-ProbeResult $passed 'baseline-3840x2160-150pct-60hz' '真实基线 3840x2160@60Hz / 150% / DPI 144'
    } | Out-Null
    Capture-WindowEvidence $restart 'restore-baseline-3840x2160-150pct-final' $dpiEvidenceRoot | Out-Null
    $script:displayMatrixStarted = $false
    Wait-ForExpectedClose $restart 'close-restart-dpi-session' '请关闭当前像素蛋挞窗口。'

    Assert-TrackedCleanAndHead $SourceHead '正式校验前'
    if ((Get-FileHash -LiteralPath $script:resolvedExecutable -Algorithm SHA256).Hash -cne $script:executableHash) { throw '正式校验前 EXE 哈希发生变化。' }
    $validationRoot = Join-Path $script:runRoot 'validation'
    $validationToolRoot = Join-Path $validationRoot 'tool'
    [IO.Directory]::CreateDirectory($validationToolRoot) | Out-Null
    Copy-Item -LiteralPath $validatorSource -Destination (Join-Path $validationToolRoot 'Test-AssetLibraryP1GateAEvidence.ps1')
    Copy-Item -LiteralPath $contractSource -Destination (Join-Path $validationToolRoot 'gate-a-evidence-contract.json')
    $validationContractPath = Join-Path $validationToolRoot 'gate-a-evidence-contract.json'
    $validationContract = Read-JsonFileSafely $validationContractPath
    $validationContract.capture_status = 'captured'
    Write-Utf8NoBom $validationContractPath ($validationContract | ConvertTo-Json -Depth 24)
    $script:manifest['status'] = 'validating'
    $script:manifest['display_restored'] = $true
    $script:manifest['validator_started'] = $true
    Save-Manifest
    $shellPath = (Get-Process -Id $PID).Path
    $validatorExit = Invoke-HiddenProcess $shellPath @(
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', (Quote-ProcessArgument (Join-Path $validationToolRoot 'Test-AssetLibraryP1GateAEvidence.ps1')),
        '-RunRoot', (Quote-ProcessArgument $script:runRoot)
    ) (Join-Path $validationRoot 'validator.stdout.txt') (Join-Path $validationRoot 'validator.stderr.txt')
    $script:manifest['validator_exit_code'] = $validatorExit
    $script:manifest['validation_summary'] = 'validation\validator.stdout.txt'
    $script:manifest['status'] = if ($validatorExit -eq 0) { 'validation-passed' } else { 'validation-failed' }
    Save-Manifest
    if ($validatorExit -ne 0) { throw "严格 Gate A validator 退出 $validatorExit；回传本轮 run root：$script:runRoot" }
}

$requiredFiles = @($projectPath, $captureTool, $validatorSource, $contractSource, $fixtureTool)
foreach ($required in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required manual acceptance input is missing: $required" }
}
$currentHead = Get-CurrentSourceHead
if ($PSBoundParameters.ContainsKey('SourceHead') -and $SourceHead -cne $currentHead) {
    throw "-SourceHead must exactly equal current lowercase Git HEAD. Supplied='$SourceHead'; current='$currentHead'."
}
$SourceHead = $currentHead
$script:contract = Get-Content -LiteralPath $contractSource -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $temporaryRoot ("PixelTart-P1-GateA-Manual-V2-{0}-{1}" -f [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N'))
}
$script:runRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $script:runRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $script:runRoot.TrimEnd('\') -eq $temporaryRoot.TrimEnd('\')) {
    throw "OutputRoot must be a new child directory below the Windows temporary root: $temporaryRoot"
}
if (Test-Path -LiteralPath $script:runRoot) { throw "OutputRoot already exists; refusing to overwrite evidence: $($script:runRoot)" }
[IO.Directory]::CreateDirectory($script:runRoot) | Out-Null
$script:manifestPath = Join-Path $script:runRoot 'manual-run-manifest.json'
$script:manifest = [ordered]@{
    schema = 'pixel-tart-p1-gate-a-manual-packet/v1'
    packet_version = 2
    status = 'initializing'
    mode = $Mode
    source_head = $SourceHead
    repository_root = $repositoryRoot
    run_root = $script:runRoot
    build_manifest_file = 'build-manifest.json'
    build_configuration = $buildConfiguration
    executable_path = $null
    executable_sha256 = $null
    synthetic_fixture_only = $true
    customer_media_allowed = $false
    eagle_library_write_allowed = $false
    ui_input_generated_by_script = $false
    display_modified_by_script = $false
    ui_started = $false
    validator_started = $false
    display_restored = $false
    sessions = @()
    step_events = @()
    created_at = [DateTimeOffset]::UtcNow.ToString('O')
    updated_at = [DateTimeOffset]::UtcNow.ToString('O')
}
Save-Manifest

if ($Mode -ceq 'DryRun') { Invoke-DryRun; return }
if ($Mode -ceq 'RecoveryTest') { Invoke-RecoveryTest; return }

Initialize-NativeObservation
$terminalError = $null
$terminalCanceled = $false
try {
    Invoke-RealRun
} catch [OperationCanceledException] {
    $terminalCanceled = $true
    $terminalError = $_.Exception
} catch {
    $terminalError = $_.Exception
} finally {
    Stop-SessionForCleanup $script:activeSession
    if ($script:displayMatrixStarted -or -not (Test-DisplayMatches (Get-DisplayObservation) $baselineDisplay $true)) {
        [void](Invoke-RecoveryCheck $(if ($terminalCanceled) { '本轮已取消，正在执行显示恢复检查' } else { '本轮异常，正在执行显示恢复检查' }))
    } else { $script:displayRestored = $true }
    if ($null -ne $terminalError) {
        $script:manifest['status'] = if ($terminalCanceled) { 'canceled' } else { 'failed' }
        $script:manifest['error'] = $terminalError.Message
    }
    $script:manifest['display_restored'] = $script:displayRestored
    $script:manifest['display_after'] = Get-DisplayObservation
    Save-Manifest
    [PixelTartManualPacketCancellationV2]::Uninstall()
}
if ($null -ne $terminalError) {
    throw "$($terminalError.Message) Run root 已保留：$($script:runRoot)"
}
Write-Output $script:manifestPath
