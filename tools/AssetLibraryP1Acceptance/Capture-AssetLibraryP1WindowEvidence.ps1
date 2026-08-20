[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ProcessId,

    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [Parameter(Mandatory = $true)]
    [string]$WindowTitle,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$')]
    [string]$CaptureName,

    [ValidateSet('ScreenPixels', 'PrintWindow')]
    [string]$CaptureMethod = 'ScreenPixels'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class AssetLibraryP1CaptureNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetDpiForWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool EnumDisplaySettingsExW(string deviceName, int modeNumber, ref DevMode mode, uint flags);

    [DllImport("shcore.dll")]
    public static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scaleFactor);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLengthW(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr handle, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(IntPtr handle, StringBuilder text, int maximumCount);

    public static IntPtr[] GetVisibleProcessWindows(uint processId)
    {
        var windows = new List<IntPtr>();
        EnumWindows((handle, _) =>
        {
            uint ownerProcessId;
            GetWindowThreadProcessId(handle, out ownerProcessId);
            if (ownerProcessId == processId && IsWindowVisible(handle)) windows.Add(handle);
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLengthW(handle);
        if (length <= 0) return string.Empty;
        var text = new StringBuilder(length + 1);
        GetWindowTextW(handle, text, text.Capacity);
        return text.ToString();
    }

    public static string GetWindowClass(IntPtr handle)
    {
        var text = new StringBuilder(256);
        GetClassNameW(handle, text, text.Capacity);
        return text.ToString();
    }
}
'@

# PER_MONITOR_AWARE_V2 makes GetWindowRect and CopyFromScreen use the same physical-pixel
# coordinate space. A false return is recorded because the host may already have selected
# an awareness context; GetDpiForWindow remains the source of the observed window DPI.
$dpiAwarenessRequested = [AssetLibraryP1CaptureNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$dpiAwarenessError = if ($dpiAwarenessRequested) { 0 } else { [Runtime.InteropServices.Marshal]::GetLastWin32Error() }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

function ConvertTo-HexHandle {
    param([Parameter(Mandatory = $true)][IntPtr]$Handle)
    return ('0x{0:X}' -f $Handle.ToInt64())
}

function Get-WindowObservation {
    param([Parameter(Mandatory = $true)][IntPtr]$Handle)

    $nativeRect = New-Object AssetLibraryP1CaptureNative+Rect
    if (-not [AssetLibraryP1CaptureNative]::GetWindowRect($Handle, [ref]$nativeRect)) {
        $nativeError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "GetWindowRect failed for $(ConvertTo-HexHandle -Handle $Handle) with Win32 error $nativeError."
    }

    $windowDpi = [AssetLibraryP1CaptureNative]::GetDpiForWindow($Handle)
    if ($windowDpi -eq 0) {
        $nativeError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "GetDpiForWindow failed for $(ConvertTo-HexHandle -Handle $Handle) with Win32 error $nativeError."
    }

    $windowWidth = $nativeRect.Right - $nativeRect.Left
    $windowHeight = $nativeRect.Bottom - $nativeRect.Top
    if ($windowWidth -le 0 -or $windowHeight -le 0) {
        throw "The observed Win32 window rectangle is invalid: ${windowWidth}x${windowHeight}."
    }

    return [ordered]@{
        hwnd = ConvertTo-HexHandle -Handle $Handle
        title = [AssetLibraryP1CaptureNative]::GetWindowTitle($Handle)
        is_foreground = [AssetLibraryP1CaptureNative]::GetForegroundWindow() -eq $Handle
        is_minimized = [AssetLibraryP1CaptureNative]::IsIconic($Handle)
        rect_physical_pixels = [ordered]@{
            left = $nativeRect.Left
            top = $nativeRect.Top
            right = $nativeRect.Right
            bottom = $nativeRect.Bottom
            width = $windowWidth
            height = $windowHeight
        }
        dpi = [int]$windowDpi
        dpi_scale_percent = [Math]::Round(([double]$windowDpi / 96.0) * 100.0, 2)
    }
}

function Test-SameWindowObservation {
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    return $Before.hwnd -eq $After.hwnd -and
        $Before.title -ceq $After.title -and
        $Before.is_foreground -eq $true -and
        $After.is_foreground -eq $true -and
        $Before.is_minimized -eq $false -and
        $After.is_minimized -eq $false -and
        $Before.dpi -eq $After.dpi -and
        $Before.rect_physical_pixels.left -eq $After.rect_physical_pixels.left -and
        $Before.rect_physical_pixels.top -eq $After.rect_physical_pixels.top -and
        $Before.rect_physical_pixels.right -eq $After.rect_physical_pixels.right -and
        $Before.rect_physical_pixels.bottom -eq $After.rect_physical_pixels.bottom
}

function Get-DisplayObservation {
    param([Parameter(Mandatory = $true)][IntPtr]$Handle)

    $screen = [Windows.Forms.Screen]::FromHandle($Handle)
    $mode = New-Object AssetLibraryP1CaptureNative+DevMode
    $mode.Size = [Runtime.InteropServices.Marshal]::SizeOf([type][AssetLibraryP1CaptureNative+DevMode])
    if (-not [AssetLibraryP1CaptureNative]::EnumDisplaySettingsExW($screen.DeviceName, -1, [ref]$mode, 0)) {
        $nativeError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "EnumDisplaySettingsExW failed for '$($screen.DeviceName)' with Win32 error $nativeError."
    }

    $monitorHandle = [AssetLibraryP1CaptureNative]::MonitorFromWindow($Handle, 2)
    if ($monitorHandle -eq [IntPtr]::Zero) { throw 'MonitorFromWindow returned a null monitor handle.' }
    $scaleFactor = 0
    $scaleResult = [AssetLibraryP1CaptureNative]::GetScaleFactorForMonitor($monitorHandle, [ref]$scaleFactor)
    if ($scaleResult -ne 0) { throw "GetScaleFactorForMonitor failed with HRESULT 0x$('{0:X8}' -f $scaleResult)." }

    return [ordered]@{
        monitor_device_name = $screen.DeviceName
        monitor_primary = $screen.Primary
        current_mode_source = 'EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)'
        current_width_physical_pixels = [int]$mode.PelsWidth
        current_height_physical_pixels = [int]$mode.PelsHeight
        current_bits_per_pixel = [int]$mode.BitsPerPel
        current_refresh_rate_hz = [int]$mode.DisplayFrequency
        scale_factor_source = 'GetScaleFactorForMonitor'
        scale_factor_percent = [int]$scaleFactor
        monitor_bounds_physical_pixels = [ordered]@{
            left = $screen.Bounds.Left
            top = $screen.Bounds.Top
            width = $screen.Bounds.Width
            height = $screen.Bounds.Height
        }
        monitor_working_area_physical_pixels = [ordered]@{
            left = $screen.WorkingArea.Left
            top = $screen.WorkingArea.Top
            width = $screen.WorkingArea.Width
            height = $screen.WorkingArea.Height
        }
    }
}

function Test-SameDisplayObservation {
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    return $Before.monitor_device_name -ceq $After.monitor_device_name -and
        $Before.current_width_physical_pixels -eq $After.current_width_physical_pixels -and
        $Before.current_height_physical_pixels -eq $After.current_height_physical_pixels -and
        $Before.current_refresh_rate_hz -eq $After.current_refresh_rate_hz -and
        $Before.scale_factor_percent -eq $After.scale_factor_percent
}

function Get-AuxiliaryWindowRecords {
    param(
        [Parameter(Mandatory = $true)][IntPtr[]]$VisibleHandles,
        [Parameter(Mandatory = $true)][IntPtr]$MainWindowHandle
    )

    return @($VisibleHandles | Where-Object { $_ -ne $MainWindowHandle } | ForEach-Object {
            [ordered]@{
                hwnd = ConvertTo-HexHandle -Handle $_
                title = [AssetLibraryP1CaptureNative]::GetWindowTitle($_)
                class_name = [AssetLibraryP1CaptureNative]::GetWindowClass($_)
                is_foreground = [AssetLibraryP1CaptureNative]::GetForegroundWindow() -eq $_
            }
        })
}

function Get-UnexpectedAuxiliaryWindowRecords {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$AuxiliaryWindows,
        [Parameter(Mandatory = $true)][string[]]$AllowedClasses
    )

    return @($AuxiliaryWindows | Where-Object {
            $_.is_foreground -or
            -not [string]::IsNullOrEmpty([string]$_.title) -or
            $AllowedClasses -cnotcontains [string]$_.class_name
        })
}

function Write-NewUtf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try { $writer.Write($Content) }
        finally { $writer.Dispose() }
    }
    finally { $stream.Dispose() }
}

$expectedExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $expectedExecutablePath -PathType Leaf)) {
    throw "Expected executable was not found: $expectedExecutablePath"
}

$targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
if ($targetProcess.HasExited) { throw "Target process has already exited: $ProcessId" }
$observedExecutablePath = [IO.Path]::GetFullPath($targetProcess.Path)
if (-not [string]::Equals($observedExecutablePath, $expectedExecutablePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "PID $ProcessId executable mismatch. Expected '$expectedExecutablePath'; observed '$observedExecutablePath'."
}

$expectedExecutableName = [IO.Path]::GetFileName($expectedExecutablePath)
$allRuntimeProcesses = @(Get-CimInstance Win32_Process | Select-Object ProcessId, Name, ExecutablePath)
$matchingNameProcesses = @($allRuntimeProcesses | Where-Object {
        [string]::Equals([string]$_.Name, $expectedExecutableName, [StringComparison]::OrdinalIgnoreCase)
    })
$matchingPathProcesses = @($matchingNameProcesses | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.ExecutablePath) -and
        [string]::Equals([IO.Path]::GetFullPath([string]$_.ExecutablePath), $expectedExecutablePath, [StringComparison]::OrdinalIgnoreCase)
    })
if ($matchingNameProcesses.Count -ne 1 -or $matchingPathProcesses.Count -ne 1 -or [int]$matchingPathProcesses[0].ProcessId -ne $ProcessId) {
    throw "Expected one global '$expectedExecutableName' process at the exact path and PID $ProcessId; observed name_count=$($matchingNameProcesses.Count), exact_path_count=$($matchingPathProcesses.Count)."
}

$visibleHandles = @([AssetLibraryP1CaptureNative]::GetVisibleProcessWindows([uint32]$ProcessId))
$matchingTitleHandles = @($visibleHandles | Where-Object {
        [string]::Equals([AssetLibraryP1CaptureNative]::GetWindowTitle($_), $WindowTitle, [StringComparison]::Ordinal)
})
if ($matchingTitleHandles.Count -ne 1) {
    throw "Expected one visible top-level window with the exact title for PID $ProcessId; observed exact_title_count=$($matchingTitleHandles.Count), all_visible_count=$($visibleHandles.Count)."
}

$allowedAuxiliaryWindowClasses = @('SoPY_Status', 'SoPY_Comp', 'IME', 'MSCTFIME UI')
$windowHandle = [IntPtr]$matchingTitleHandles[0]
$auxiliaryWindows = @(Get-AuxiliaryWindowRecords -VisibleHandles $visibleHandles -MainWindowHandle $windowHandle)
$unexpectedAuxiliaryWindows = @(Get-UnexpectedAuxiliaryWindowRecords -AuxiliaryWindows $auxiliaryWindows -AllowedClasses $allowedAuxiliaryWindowClasses)
$initialUnexpectedAuxiliaryWindows = @($unexpectedAuxiliaryWindows)
$auxiliaryWindowQuietWaitStartedAt = [DateTimeOffset]::UtcNow
$auxiliaryWindowQuietWaitPollCount = 0
$auxiliaryWindowQuietWaitTimeout = [TimeSpan]::FromSeconds(15)
while ($unexpectedAuxiliaryWindows.Count -ne 0 -and
       ([DateTimeOffset]::UtcNow - $auxiliaryWindowQuietWaitStartedAt) -lt $auxiliaryWindowQuietWaitTimeout) {
    # A WPF ToolTip/Popup is a real visible top-level HWND and may appear after the
    # packet's main-window stability probe. Do not allowlist its dynamic class or
    # capture through it. Wait boundedly for every unapproved HWND to disappear,
    # while continuously requiring the exact main HWND to remain foreground.
    Start-Sleep -Milliseconds 200
    $auxiliaryWindowQuietWaitPollCount++
    $visibleHandles = @([AssetLibraryP1CaptureNative]::GetVisibleProcessWindows([uint32]$ProcessId))
    $matchingTitleHandles = @($visibleHandles | Where-Object {
            [string]::Equals([AssetLibraryP1CaptureNative]::GetWindowTitle($_), $WindowTitle, [StringComparison]::Ordinal)
        })
    if ($matchingTitleHandles.Count -ne 1 -or [IntPtr]$matchingTitleHandles[0] -ne $windowHandle) {
        throw "The exact main HWND changed while waiting for auxiliary windows to close for PID $ProcessId."
    }
    if ([AssetLibraryP1CaptureNative]::GetForegroundWindow() -ne $windowHandle) {
        throw "The exact target window lost foreground while waiting for auxiliary windows to close: $(ConvertTo-HexHandle -Handle $windowHandle)."
    }
    $auxiliaryWindows = @(Get-AuxiliaryWindowRecords -VisibleHandles $visibleHandles -MainWindowHandle $windowHandle)
    $unexpectedAuxiliaryWindows = @(Get-UnexpectedAuxiliaryWindowRecords -AuxiliaryWindows $auxiliaryWindows -AllowedClasses $allowedAuxiliaryWindowClasses)
}
$auxiliaryWindowQuietWaitMilliseconds = [Math]::Round(([DateTimeOffset]::UtcNow - $auxiliaryWindowQuietWaitStartedAt).TotalMilliseconds, 0)
if ($unexpectedAuxiliaryWindows.Count -ne 0) {
    throw "Unexpected visible auxiliary top-level window(s) remained after a bounded 15-second quiet wait for PID ${ProcessId}: $($unexpectedAuxiliaryWindows | ConvertTo-Json -Compress)."
}

$before = Get-WindowObservation -Handle $windowHandle
if ($before.title -cne $WindowTitle) {
    throw "Window title mismatch. Expected '$WindowTitle'; observed '$($before.title)'."
}
if (-not $before.is_foreground) {
    throw "The exact target window is not the foreground window: $($before.hwnd)."
}
if ($before.is_minimized) {
    throw "The exact target window is minimized: $($before.hwnd)."
}

$displayBefore = Get-DisplayObservation -Handle $windowHandle
$displayControllers = @(Get-CimInstance Win32_VideoController | ForEach-Object {
        [ordered]@{
            name = [string]$_.Name
            current_horizontal_resolution = if ($null -eq $_.CurrentHorizontalResolution) { $null } else { [int]$_.CurrentHorizontalResolution }
            current_vertical_resolution = if ($null -eq $_.CurrentVerticalResolution) { $null } else { [int]$_.CurrentVerticalResolution }
            current_refresh_rate = if ($null -eq $_.CurrentRefreshRate) { $null } else { [int]$_.CurrentRefreshRate }
        }
    })

$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
[IO.Directory]::CreateDirectory($resolvedOutputRoot) | Out-Null
$screenshotPath = Join-Path $resolvedOutputRoot "$CaptureName.png"
$manifestPath = Join-Path $resolvedOutputRoot "$CaptureName.window-evidence.json"
if ((Test-Path -LiteralPath $screenshotPath) -or (Test-Path -LiteralPath $manifestPath)) {
    throw "Capture output already exists; choose a new CaptureName. PNG='$screenshotPath'; manifest='$manifestPath'."
}

$captureWidth = [int]$before.rect_physical_pixels.width
$captureHeight = [int]$before.rect_physical_pixels.height
$bitmap = [Drawing.Bitmap]::new($captureWidth, $captureHeight)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$imageStream = $null
try {
    if ($CaptureMethod -eq 'ScreenPixels') {
        $graphics.CopyFromScreen(
            [int]$before.rect_physical_pixels.left,
            [int]$before.rect_physical_pixels.top,
            0,
            0,
            $bitmap.Size,
            [Drawing.CopyPixelOperation]::SourceCopy)
    }
    else {
        $deviceContext = $graphics.GetHdc()
        try {
            # PW_RENDERFULLCONTENT asks the exact foreground WPF window to render its
            # current pixels without unrelated desktop cursor/highlight overlays.
            if (-not [AssetLibraryP1CaptureNative]::PrintWindow($windowHandle, $deviceContext, 2)) {
                $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
                throw "PrintWindow(PW_RENDERFULLCONTENT) failed with Win32 error $errorCode."
            }
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }
    }
    $imageStream = [IO.File]::Open($screenshotPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $bitmap.Save($imageStream, [Drawing.Imaging.ImageFormat]::Png)
}
finally {
    if ($null -ne $imageStream) { $imageStream.Dispose() }
    $graphics.Dispose()
    $bitmap.Dispose()
}

$afterHandles = @([AssetLibraryP1CaptureNative]::GetVisibleProcessWindows([uint32]$ProcessId))
$matchingTitleHandlesAfter = @($afterHandles | Where-Object {
        [string]::Equals([AssetLibraryP1CaptureNative]::GetWindowTitle($_), $WindowTitle, [StringComparison]::Ordinal)
    })
$after = if ($matchingTitleHandlesAfter.Count -eq 1) {
    Get-WindowObservation -Handle ([IntPtr]$matchingTitleHandlesAfter[0])
}
else {
    $null
}
$auxiliaryWindowsAfter = @()
if ($matchingTitleHandlesAfter.Count -eq 1) {
    $auxiliaryWindowsAfter = @(Get-AuxiliaryWindowRecords -VisibleHandles $afterHandles -MainWindowHandle ([IntPtr]$matchingTitleHandlesAfter[0]))
}
$unexpectedAuxiliaryWindowsAfter = @(Get-UnexpectedAuxiliaryWindowRecords -AuxiliaryWindows $auxiliaryWindowsAfter -AllowedClasses $allowedAuxiliaryWindowClasses)
$stableWindow = $null -ne $after -and (Test-SameWindowObservation -Before $before -After $after)
$displayAfter = if ($null -ne $after) { Get-DisplayObservation -Handle $windowHandle } else { $null }
$stableDisplay = $null -ne $displayAfter -and (Test-SameDisplayObservation -Before $displayBefore -After $displayAfter)
$allRuntimeProcessesAfter = @(Get-CimInstance Win32_Process | Select-Object ProcessId, Name, ExecutablePath)
$matchingNameProcessesAfter = @($allRuntimeProcessesAfter | Where-Object {
        [string]::Equals([string]$_.Name, $expectedExecutableName, [StringComparison]::OrdinalIgnoreCase)
    })
$matchingPathProcessesAfter = @($matchingNameProcessesAfter | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.ExecutablePath) -and
        [string]::Equals([IO.Path]::GetFullPath([string]$_.ExecutablePath), $expectedExecutablePath, [StringComparison]::OrdinalIgnoreCase)
    })
$stableProcessPopulation = $matchingNameProcessesAfter.Count -eq 1 -and
    $matchingPathProcessesAfter.Count -eq 1 -and
    [int]$matchingPathProcessesAfter[0].ProcessId -eq $ProcessId

$screenshotInfo = Get-Item -LiteralPath $screenshotPath
$screenshotSha256 = (Get-FileHash -LiteralPath $screenshotPath -Algorithm SHA256).Hash
$executableSha256 = (Get-FileHash -LiteralPath $expectedExecutablePath -Algorithm SHA256).Hash
$pngHeader = [IO.File]::ReadAllBytes($screenshotPath)
$validPngSignature = $pngHeader.Length -ge 8 -and
    $pngHeader[0] -eq 137 -and $pngHeader[1] -eq 80 -and $pngHeader[2] -eq 78 -and $pngHeader[3] -eq 71 -and
    $pngHeader[4] -eq 13 -and $pngHeader[5] -eq 10 -and $pngHeader[6] -eq 26 -and $pngHeader[7] -eq 10

$verified = $stableWindow -and $stableDisplay -and $stableProcessPopulation -and
    $unexpectedAuxiliaryWindowsAfter.Count -eq 0 -and $validPngSignature -and $screenshotInfo.Length -gt 8
$captureMethodDescription = if ($CaptureMethod -eq 'ScreenPixels') {
    'System.Drawing.Graphics.CopyFromScreen'
}
else {
    'Win32.PrintWindow(PW_RENDERFULLCONTENT)'
}
$manifest = [ordered]@{
    schema = 'pixel-tart-asset-library-p1-window-evidence/v1'
    capture_name = $CaptureName
    captured_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    capture_method = $captureMethodDescription
    coordinate_space = if ($CaptureMethod -eq 'ScreenPixels') { 'physical_screen_pixels' } else { 'physical_window_pixels' }
    ui_input_generated = $false
    synthetic_ui_events_generated = $false
    expected = [ordered]@{
        process_id = $ProcessId
        executable_path = $expectedExecutablePath
        window_title = $WindowTitle
        global_matching_executable_process_count = 1
    }
    process = [ordered]@{
        process_id = $ProcessId
        executable_name = $expectedExecutableName
        executable_path = $observedExecutablePath
        executable_sha256 = $executableSha256
        global_matching_name_process_count = $matchingNameProcesses.Count
        global_matching_name_process_ids = @($matchingNameProcesses | ForEach-Object { [int]$_.ProcessId } | Sort-Object)
        global_matching_exact_path_process_count = $matchingPathProcesses.Count
        global_matching_exact_path_process_ids = @($matchingPathProcesses | ForEach-Object { [int]$_.ProcessId } | Sort-Object)
        global_matching_name_process_count_after_capture = $matchingNameProcessesAfter.Count
        global_matching_name_process_ids_after_capture = @($matchingNameProcessesAfter | ForEach-Object { [int]$_.ProcessId } | Sort-Object)
        global_matching_exact_path_process_count_after_capture = $matchingPathProcessesAfter.Count
        global_matching_exact_path_process_ids_after_capture = @($matchingPathProcessesAfter | ForEach-Object { [int]$_.ProcessId } | Sort-Object)
    }
    dpi_awareness = [ordered]@{
        requested_context = 'PER_MONITOR_AWARE_V2'
        request_succeeded = [bool]$dpiAwarenessRequested
        request_win32_error = $dpiAwarenessError
        observed_dpi_source = 'GetDpiForWindow'
    }
    display = [ordered]@{
        before_capture = $displayBefore
        after_capture = $displayAfter
        video_controllers = $displayControllers
    }
    visible_top_level_window_count_before_capture = $visibleHandles.Count
    exact_title_main_window_count_before_capture = $matchingTitleHandles.Count
    allowed_nonforeground_ime_auxiliary_windows = $auxiliaryWindows
    transient_unexpected_auxiliary_windows_before_capture = $initialUnexpectedAuxiliaryWindows
    auxiliary_window_quiet_wait_milliseconds = $auxiliaryWindowQuietWaitMilliseconds
    auxiliary_window_quiet_wait_poll_count = $auxiliaryWindowQuietWaitPollCount
    unexpected_auxiliary_window_count = $unexpectedAuxiliaryWindows.Count
    window_before_capture = $before
    window_after_capture = $after
    visible_top_level_window_count_after_capture = $afterHandles.Count
    exact_title_main_window_count_after_capture = $matchingTitleHandlesAfter.Count
    auxiliary_windows_after_capture = $auxiliaryWindowsAfter
    unexpected_auxiliary_window_count_after_capture = $unexpectedAuxiliaryWindowsAfter.Count
    screenshot = [ordered]@{
        file_name = [IO.Path]::GetFileName($screenshotPath)
        absolute_path = $screenshotPath
        width_physical_pixels = $captureWidth
        height_physical_pixels = $captureHeight
        bytes = $screenshotInfo.Length
        sha256 = $screenshotSha256
        png_signature_verified = [bool]$validPngSignature
    }
    verification = [ordered]@{
        exact_pid_path_title_verified = $true
        single_product_main_window_verified = $matchingTitleHandles.Count -eq 1 -and $matchingTitleHandlesAfter.Count -eq 1
        single_global_matching_process_verified = [bool]$stableProcessPopulation
        exact_window_foreground_verified = $before.is_foreground -eq $true
        no_unapproved_auxiliary_window_during_capture = $unexpectedAuxiliaryWindows.Count -eq 0 -and $unexpectedAuxiliaryWindowsAfter.Count -eq 0
        window_stable_during_capture = [bool]$stableWindow
        display_mode_and_scale_stable_during_capture = [bool]$stableDisplay
        passed = [bool]$verified
    }
}

Write-NewUtf8File -Path $manifestPath -Content ($manifest | ConvertTo-Json -Depth 10)
Write-Output $manifestPath

if (-not $verified) {
    throw "The real-screen capture was written, but the before/after evidence did not remain stable. Manifest: $manifestPath"
}
