[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$RootProcessId,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$allProcesses = @(Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId, Name)
$root = @($allProcesses | Where-Object { [int]$_.ProcessId -eq $RootProcessId })
if ($root.Count -ne 1) { throw "Root process was not found: $RootProcessId" }
if ($root[0].Name -ne 'PixelTart_ModularHarness_V1_DevPreview.exe') {
    throw "Unexpected root executable: $($root[0].Name)"
}

$descendantIds = New-Object 'System.Collections.Generic.HashSet[int]'
$pending = New-Object 'System.Collections.Generic.Queue[int]'
$pending.Enqueue($RootProcessId)
while ($pending.Count -gt 0) {
    $parentId = $pending.Dequeue()
    foreach ($child in @($allProcesses | Where-Object { [int]$_.ParentProcessId -eq $parentId })) {
        $childId = [int]$child.ProcessId
        if ($descendantIds.Add($childId)) { $pending.Enqueue($childId) }
    }
}

$capturedIds = @($RootProcessId) + @($descendantIds | Sort-Object)
$matchingExecutableProcessIds = @($allProcesses |
    Where-Object { $_.Name -eq 'PixelTart_ModularHarness_V1_DevPreview.exe' } |
    Select-Object -ExpandProperty ProcessId |
    ForEach-Object { [int]$_ } |
    Sort-Object)
$capturedProcesses = @()
$guiProcessIds = @()
foreach ($processInfo in @($allProcesses | Where-Object { $capturedIds -contains [int]$_.ProcessId } | Sort-Object ProcessId)) {
    $processId = [int]$processInfo.ProcessId
    $isGui = $false
    try {
        $runtimeProcess = Get-Process -Id $processId -ErrorAction Stop
        $isGui = -not $runtimeProcess.HasExited -and $runtimeProcess.MainWindowHandle -ne [IntPtr]::Zero
    }
    catch {
        $isGui = $false
    }
    if ($isGui) { $guiProcessIds += $processId }
    $capturedProcesses += [ordered]@{
        process_id = $processId
        parent_process_id = [int]$processInfo.ParentProcessId
        executable_name = [string]$processInfo.Name
        is_root = $processId -eq $RootProcessId
        is_gui = $isGui
    }
}

$snapshot = [ordered]@{
    schema = 'pixel-tart-modular-harness-v1-process-snapshot/v1'
    captured_at = [DateTimeOffset]::Now.ToString('O')
    root_process_id = $RootProcessId
    processes = $capturedProcesses
    descendant_process_ids = @($descendantIds | Sort-Object)
    gui_process_ids = @($guiProcessIds | Sort-Object)
    matching_executable_process_ids = $matchingExecutableProcessIds
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
}
[System.IO.File]::WriteAllText(
    $resolvedOutput,
    ($snapshot | ConvertTo-Json -Depth 6),
    [System.Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
