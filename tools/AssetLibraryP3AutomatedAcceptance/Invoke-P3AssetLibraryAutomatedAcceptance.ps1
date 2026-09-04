[CmdletBinding()]
param(
    [ValidateSet('Run', 'DryRun', 'ValidateExistingRun', 'RecoveryTest')]
    [string]$Mode = 'Run',
    [string]$OutputRoot,
    [string]$RunRoot,
    [int]$TimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:expectedBranch = 'feature/asset-library-eagle-parity-p3-query-metadata'
$script:expectedProcessName = 'PixelTart_ModularHarness_V1_DevPreview'
$script:requiredAcceptanceInputFiles = @(
    'Invoke-P3AssetLibraryAutomatedAcceptance.ps1',
    'Test-P3AssetLibraryAutomatedEvidence.ps1',
    'Test-P3AssetLibraryAutomatedRunSet.ps1',
    'New-P3SyntheticFixture.py',
    'Invoke-P3NegativeEvidenceProofs.py',
    'automated-acceptance-contract.json',
    'README.md'
)
$script:environmentKeys = @(
    'PIXEL_TART_ACCEPTANCE_ROOT',
    'PIXEL_TART_ASSET_LIBRARY_DEMO_DIR',
    'PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE',
    'PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE',
    'PIXEL_TART_ASSET_LIBRARY_P1_HEAD',
    'PIXEL_TART_P3_AUTOMATED_HEAD',
    'PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS',
    'PIXEL_TART_P3_AUTOMATED_ACCEPTANCE',
    'PIXEL_TART_P3_AUTOMATED_RUN_ROOT',
    'PIXEL_TART_P3_AUTOMATED_PLAN_PATH',
    'PIXEL_TART_P3_AUTOMATED_SOURCE_HEAD',
    'PIXEL_TART_P3_AUTOMATED_FIXTURE_ROOT',
    'MSBUILDDISABLENODEREUSE'
)
$script:p3LifecycleEvents = @(
    'plan-completed','completion-ack-written','shutdown-requested','shutdown-dispatch-started',
    'page-dispose-start','page-dispose-completed','application-async-dispose-start',
    'application-async-dispose-completed','shutdown-preparation-complete','window-close-start',
    'application-shutdown-start','window-close-completed','application-on-exit-enter',
    'summary-commit-start','phase-summary-written','summary-commit-end','application-on-exit-completed'
)
$script:p3LifecycleResults = @(
    'passed','completed','accepted','started','started','completed','started','completed','completed',
    'started','started','completed','prepared','passed','passed','passed','completed'
)
$script:p3LifecyclePending = @(
    $false,$false,$true,$true,$true,$false,$true,$false,$false,$true,$true,$false,$false,$true,$false,$false,$false
)
$script:p3LifecyclePendingOperationCountRules = @(
    'zero','zero','one','one','observed-nonnegative','zero','one','zero','zero',
    'one','one','zero','zero','one','zero','zero','zero'
)
$script:p3ProcessStageTimeoutSeconds = [ordered]@{
    completion_handshake = 235
    shutdown_preparation = 20
    application_on_exit_enter = 10
    phase_summary_commit = 10
    application_on_exit_completed = 5
    process_exit = 10
    process_table_convergence = 10
}
$script:p3ProcessStageTotalTimeoutSeconds = 300

function Get-RepositoryRoot {
    $candidate = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    if (-not (Test-Path -LiteralPath (Join-Path $candidate '.git'))) {
        $probe = $candidate
        while ($probe -and -not (Test-Path -LiteralPath (Join-Path $probe '.git'))) {
            $parent = Split-Path -Parent $probe
            if ($parent -eq $probe) { break }
            $probe = $parent
        }
        $candidate = $probe
    }
    if ([string]::IsNullOrWhiteSpace($candidate) -or -not (Test-Path -LiteralPath (Join-Path $candidate '.git'))) {
        throw 'Git repository root could not be resolved from the automated acceptance entry.'
    }
    return [IO.Path]::GetFullPath($candidate)
}

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & git -C $script:repo @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)" }
    return @($output)
}

function Assert-CleanCommit {
    $branch = ((Invoke-Git @('branch', '--show-current')) -join '').Trim()
    if ($branch -cne $script:expectedBranch) { throw "Expected branch '$script:expectedBranch'; actual '$branch'." }
    $status = ((Invoke-Git @('status', '--short')) -join [Environment]::NewLine).Trim()
    if (-not [string]::IsNullOrWhiteSpace($status)) { throw "Worktree changes are present:`n$status" }
    $head = ((Invoke-Git @('rev-parse', 'HEAD')) -join '').Trim()
    if ($head -cnotmatch '^[0-9a-f]{40}$') { throw "Invalid source HEAD '$head'." }
    return $head
}

function Assert-TrackedCleanAndHead {
    param([string]$ExpectedHead, [string]$Context)
    $head = ((Invoke-Git @('rev-parse', 'HEAD')) -join '').Trim()
    if ($head -cne $ExpectedHead) { throw "$Context changed HEAD from $ExpectedHead to $head." }
    $tracked = ((Invoke-Git @('status', '--short', '--untracked-files=no')) -join [Environment]::NewLine).Trim()
    if (-not [string]::IsNullOrWhiteSpace($tracked)) { throw "$Context found tracked worktree changes:`n$tracked" }
}

function Get-DotNetPath {
    $probe = $script:repo
    while (-not [string]::IsNullOrWhiteSpace($probe)) {
        $workspaceDotNet = Join-Path $probe '.dotnet\dotnet.exe'
        if (Test-Path -LiteralPath $workspaceDotNet -PathType Leaf) { return $workspaceDotNet }
        $parent = Split-Path -Parent $probe
        if ($parent -eq $probe) { break }
        $probe = $parent
    }
    $command = Get-Command dotnet.exe -ErrorAction Stop
    return $command.Source
}

if (-not ('PixelTartP3ProcessTableSnapshot' -as [type])) {
    Add-Type -TypeDefinition @'
public sealed class PixelTartP3ProcessIdentitySnapshotRow
{
    public int Pid { get; set; }
    public string ProcessName { get; set; }
    public string StartTimeUtc { get; set; }
    public string ExecutablePath { get; set; }
    public string ExecutableSha256 { get; set; }
    public string RunId { get; set; }
    public string ProcessSessionId { get; set; }
    public string WindowHandle { get; set; }
    public bool OwnedByRun { get; set; }
    public bool HasExited { get; set; }
    public string ObservationError { get; set; }
}

public sealed class PixelTartP3ProcessTableSnapshot
{
    public string Schema { get; set; }
    public string Source { get; set; }
    public string CapturedAtUtc { get; set; }
    public string ObservationError { get; set; }
    public PixelTartP3ProcessIdentitySnapshotRow[] Items { get; set; }
}
'@
}

function ConvertTo-NormalizedProcessPath {
    param([AllowNull()][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return '' }
    try { return [IO.Path]::GetFullPath($Path) }
    catch { return $Path }
}

function New-ProcessIdentitySnapshotRow {
    param(
        [int]$ProcessId,
        [string]$ProcessName,
        [AllowNull()][string]$StartTimeUtc,
        [AllowNull()][string]$ExecutablePath,
        [AllowNull()][string]$ExecutableSha256,
        [AllowNull()][string]$RunId,
        [AllowNull()][string]$ProcessSessionId,
        [AllowNull()][string]$WindowHandle,
        [bool]$OwnedByRun,
        [bool]$HasExited,
        [AllowNull()][string]$ObservationError
    )
    $row = [PixelTartP3ProcessIdentitySnapshotRow]::new()
    $row.Pid = $ProcessId
    $row.ProcessName = $ProcessName
    $row.StartTimeUtc = [string]$StartTimeUtc
    $row.ExecutablePath = ConvertTo-NormalizedProcessPath $ExecutablePath
    $row.ExecutableSha256 = [string]$ExecutableSha256
    $row.RunId = [string]$RunId
    $row.ProcessSessionId = [string]$ProcessSessionId
    $row.WindowHandle = [string]$WindowHandle
    $row.OwnedByRun = $OwnedByRun
    $row.HasExited = $HasExited
    $row.ObservationError = [string]$ObservationError
    return $row
}

function New-ProcessTableSnapshot {
    param(
        [ValidateSet('Get-Process', 'CIM')]
        [string]$Source,
        [AllowEmptyCollection()][PixelTartP3ProcessIdentitySnapshotRow[]]$Items = @(),
        [AllowNull()][string]$ObservationError = ''
    )
    # This CLR table is intentionally not IEnumerable. Windows PowerShell 5.1
    # enumerates function output arrays, while StrictMode rejects member access
    # such as $emptyArray.Pid on a zero-length array.
    $snapshot = [PixelTartP3ProcessTableSnapshot]::new()
    $snapshot.Schema = 'pixel-tart-p3-process-table-snapshot/v1'
    $snapshot.Source = $Source
    $snapshot.CapturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $snapshot.ObservationError = [string]$ObservationError
    $snapshot.Items = [PixelTartP3ProcessIdentitySnapshotRow[]]@($Items)
    return $snapshot
}

function Test-ProcessOwnerTokenValues {
    param(
        [AllowNull()]$OwnerToken,
        [int]$ProcessId,
        [AllowNull()][string]$ExecutablePath,
        [AllowNull()][string]$StartTimeUtc
    )
    if ($null -eq $OwnerToken) { return $false }
    if ($ProcessId -ne [int]$OwnerToken.Pid) { return $false }
    if (-not [string]::Equals(
        (ConvertTo-NormalizedProcessPath $ExecutablePath),
        (ConvertTo-NormalizedProcessPath ([string]$OwnerToken.ExecutablePath)),
        [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ([string]::IsNullOrWhiteSpace($StartTimeUtc)) { return $false }
    try {
        $actualStart = [DateTimeOffset]::Parse($StartTimeUtc, [Globalization.CultureInfo]::InvariantCulture)
        $ownerStart = [DateTimeOffset]::Parse([string]$OwnerToken.StartTimeUtc, [Globalization.CultureInfo]::InvariantCulture)
        return [Math]::Abs(($actualStart - $ownerStart).TotalMilliseconds) -lt 1000
    } catch { return $false }
}

function Get-ObservedExecutableSha256 {
    param([AllowNull()][string]$ExecutablePath, [Collections.Generic.List[string]]$Errors)
    if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
        $Errors.Add('ExecutablePath unavailable.')
        return ''
    }
    try {
        if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
            $Errors.Add('ExecutablePath is not a readable file.')
            return ''
        }
        return Get-FileSha256 $ExecutablePath
    } catch {
        $Errors.Add("ExecutableSha256 observation failed: $($_.Exception.Message)")
        return ''
    }
}

function Get-ProcessSnapshot {
    param([AllowNull()]$OwnerToken)
    $rows = [Collections.Generic.List[PixelTartP3ProcessIdentitySnapshotRow]]::new()
    $processes = [Collections.Generic.List[object]]::new()
    try {
        foreach ($candidate in @(Get-Process -ErrorAction Stop)) {
            if ($null -eq $candidate) { continue }
            $nameProperty = $candidate.PSObject.Properties['ProcessName']
            if ($null -eq $nameProperty) {
                throw 'Get-Process returned an object without the required ProcessName field.'
            }
            if ([string]$nameProperty.Value -ceq $script:expectedProcessName) { $processes.Add($candidate) }
        }
    } catch {
        throw "Get-Process DevPreview observation failed closed: $($_.Exception.Message)"
    }
    foreach ($process in $processes) {
        $errors = [Collections.Generic.List[string]]::new()
        $path = ''
        $startTimeUtc = ''
        $windowHandle = ''
        $hasExited = $false
        try { $path = ConvertTo-NormalizedProcessPath ([string]$process.Path) }
        catch { $errors.Add("ExecutablePath observation failed: $($_.Exception.Message)") }
        try { $startTimeUtc = $process.StartTime.ToUniversalTime().ToString('O') }
        catch { $errors.Add("StartTimeUtc observation failed: $($_.Exception.Message)") }
        try { $windowHandle = ('0x{0:x}' -f $process.MainWindowHandle.ToInt64()) }
        catch { $errors.Add("WindowHandle observation failed: $($_.Exception.Message)") }
        try { $hasExited = [bool]$process.HasExited }
        catch { $errors.Add("HasExited observation failed: $($_.Exception.Message)") }
        $sha256 = Get-ObservedExecutableSha256 $path $errors
        $ownedByRun = Test-ProcessOwnerTokenValues $OwnerToken ([int]$process.Id) $path $startTimeUtc
        $rows.Add((New-ProcessIdentitySnapshotRow `
            ([int]$process.Id) ([string]$process.ProcessName) $startTimeUtc $path $sha256 `
            $(if ($ownedByRun) { [string]$OwnerToken.RunId } else { '' }) `
            $(if ($ownedByRun) { [string]$OwnerToken.ProcessSessionId } else { '' }) `
            $windowHandle $ownedByRun $hasExited ($errors -join ' | ')))
    }
    return New-ProcessTableSnapshot 'Get-Process' ([PixelTartP3ProcessIdentitySnapshotRow[]]$rows.ToArray())
}

function Get-CimProcessSnapshot {
    param([AllowNull()]$OwnerToken)
    $rows = [Collections.Generic.List[PixelTartP3ProcessIdentitySnapshotRow]]::new()
    $processes = [Collections.Generic.List[object]]::new()
    $executableName = "$($script:expectedProcessName).exe"
    try {
        foreach ($candidate in @(Get-CimInstance -ClassName Win32_Process -Filter "Name='$executableName'" -ErrorAction Stop)) {
            if ($null -eq $candidate) { continue }
            foreach ($requiredField in 'ProcessId','Name','ExecutablePath','CreationDate') {
                if ($null -eq $candidate.PSObject.Properties[$requiredField]) {
                    throw "CIM returned an object without the required $requiredField field."
                }
            }
            $processes.Add($candidate)
        }
    } catch {
        throw "CIM DevPreview observation failed closed: $($_.Exception.Message)"
    }
    foreach ($process in $processes) {
        $errors = [Collections.Generic.List[string]]::new()
        $path = ConvertTo-NormalizedProcessPath ([string]$process.ExecutablePath)
        $startTimeUtc = ''
        try {
            $creationDate = $process.CreationDate
            if ($creationDate -is [DateTimeOffset]) {
                $startTimeUtc = ([DateTimeOffset]$creationDate).ToUniversalTime().ToString('O')
            } elseif ($creationDate -is [DateTime]) {
                $startTimeUtc = ([DateTime]$creationDate).ToUniversalTime().ToString('O')
            } else {
                $startTimeUtc = [Management.ManagementDateTimeConverter]::ToDateTime(
                    [string]$creationDate).ToUniversalTime().ToString('O')
            }
        } catch { $errors.Add("StartTimeUtc observation failed: $($_.Exception.Message)") }
        $sha256 = Get-ObservedExecutableSha256 $path $errors
        $ownedByRun = Test-ProcessOwnerTokenValues $OwnerToken ([int]$process.ProcessId) $path $startTimeUtc
        $rows.Add((New-ProcessIdentitySnapshotRow `
            ([int]$process.ProcessId) ([IO.Path]::GetFileNameWithoutExtension([string]$process.Name)) $startTimeUtc $path $sha256 `
            $(if ($ownedByRun) { [string]$OwnerToken.RunId } else { '' }) `
            $(if ($ownedByRun) { [string]$OwnerToken.ProcessSessionId } else { '' }) `
            '' $ownedByRun $false ($errors -join ' | ')))
    }
    return New-ProcessTableSnapshot 'CIM' ([PixelTartP3ProcessIdentitySnapshotRow[]]$rows.ToArray())
}

function Assert-ProcessTableSnapshot {
    param($Snapshot, [string]$ExpectedSource)
    if ($null -eq $Snapshot -or
        -not ($Snapshot -is [PixelTartP3ProcessTableSnapshot]) -or
        [string]$Snapshot.Schema -cne 'pixel-tart-p3-process-table-snapshot/v1' -or
        [string]$Snapshot.Source -cne $ExpectedSource -or
        $null -eq $Snapshot.Items) {
        throw "The $ExpectedSource DevPreview process table snapshot is malformed."
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$Snapshot.ObservationError)) {
        throw "The $ExpectedSource DevPreview process table query failed closed: $($Snapshot.ObservationError)"
    }
    foreach ($row in [PixelTartP3ProcessIdentitySnapshotRow[]]$Snapshot.Items) {
        if ($null -eq $row -or [int]$row.Pid -le 0) {
            throw "The $ExpectedSource DevPreview process table contains a malformed identity row."
        }
    }
}

function New-DevPreviewProcessObservation {
    param(
        [AllowNull()]$GetProcessSnapshot,
        [AllowNull()]$CimProcessSnapshot
    )
    if (-not $PSBoundParameters.ContainsKey('GetProcessSnapshot')) { $GetProcessSnapshot = Get-ProcessSnapshot }
    if (-not $PSBoundParameters.ContainsKey('CimProcessSnapshot')) { $CimProcessSnapshot = Get-CimProcessSnapshot }
    Assert-ProcessTableSnapshot $GetProcessSnapshot 'Get-Process'
    Assert-ProcessTableSnapshot $CimProcessSnapshot 'CIM'
    $observation = [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-devpreview-process-observation/v1'
        captured_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        GetProcess = $GetProcessSnapshot
        Cim = $CimProcessSnapshot
    }
    $observation.PSObject.TypeNames.Insert(0, 'PixelTart.P3.DevPreviewProcessObservation')
    return $observation
}

function Assert-DevPreviewProcessObservation {
    param($Observation)
    if ($null -eq $Observation -or
        [string]$Observation.schema -cne 'pixel-tart-p3-devpreview-process-observation/v1' -or
        $null -eq $Observation.PSObject.Properties['GetProcess'] -or
        $null -eq $Observation.PSObject.Properties['Cim']) {
        throw 'The combined DevPreview process observation is malformed.'
    }
    Assert-ProcessTableSnapshot $Observation.GetProcess 'Get-Process'
    Assert-ProcessTableSnapshot $Observation.Cim 'CIM'
}

function Get-DevPreviewProcessPids {
    param($Observation)
    Assert-DevPreviewProcessObservation $Observation
    $pids = [Collections.Generic.List[int]]::new()
    foreach ($row in [PixelTartP3ProcessIdentitySnapshotRow[]]$Observation.GetProcess.Items) { $pids.Add([int]$row.Pid) }
    foreach ($row in [PixelTartP3ProcessIdentitySnapshotRow[]]$Observation.Cim.Items) { $pids.Add([int]$row.Pid) }
    return @($pids.ToArray() | Sort-Object -Unique)
}

function Assert-NoDevPreview {
    param([AllowNull()]$Observation)
    if (-not $PSBoundParameters.ContainsKey('Observation')) { $Observation = New-DevPreviewProcessObservation }
    Assert-DevPreviewProcessObservation $Observation
    if ($Observation.GetProcess.Items.Count -ne 0 -or $Observation.Cim.Items.Count -ne 0) {
        $pids = @(Get-DevPreviewProcessPids $Observation)
        throw "Automated acceptance requires both DevPreview process tables to be empty; found PID(s): $($pids -join ', ')."
    }
}

function ConvertTo-StructuredFailure {
    param(
        [string]$Category,
        [string]$Stage,
        [AllowNull()]$Failure
    )
    $exception = if ($Failure -is [Management.Automation.ErrorRecord]) { $Failure.Exception }
        elseif ($Failure -is [Exception]) { $Failure }
        else { $null }
    $isApplicationReportedFailure = $null -ne $exception -and
        $exception.Data.Contains('p3_application_reported_failure') -and
        [bool]$exception.Data['p3_application_reported_failure']
    $message = if ($isApplicationReportedFailure) { [string]$exception.Data['p3_application_exception_message'] }
        elseif ($null -ne $exception) { [string]$exception.Message }
        else { [string]$Failure }
    $type = if ($isApplicationReportedFailure) { [string]$exception.Data['p3_application_exception_type'] }
        elseif ($null -ne $exception) { [string]$exception.GetType().FullName }
        else { [string]$Failure.GetType().FullName }
    $fullyQualifiedErrorId = if ($isApplicationReportedFailure) { 'application-reported-failure' }
        elseif ($Failure -is [Management.Automation.ErrorRecord]) { [string]$Failure.FullyQualifiedErrorId }
        else { '' }
    $scriptStackTrace = if ($isApplicationReportedFailure) { [string]$exception.Data['p3_application_exception_stack_trace'] }
        elseif ($Failure -is [Management.Automation.ErrorRecord]) { [string]$Failure.ScriptStackTrace }
        else { '' }
    return [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-structured-failure/v1'
        category = $Category
        stage = $Stage
        type = $type
        message = $message
        fully_qualified_error_id = $fullyQualifiedErrorId
        script_stack_trace = $scriptStackTrace
        observed_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    }
}

function New-P3ApplicationReportedFailureException {
    param($Failure)
    $exception = [InvalidOperationException]::new([string]$Failure.message)
    $exception.Data['p3_stage'] = 'application-reported-failure'
    $exception.Data['p3_application_reported_failure'] = $true
    $exception.Data['p3_application_exception_type'] = [string]$Failure.type
    $exception.Data['p3_application_exception_message'] = [string]$Failure.message
    $exception.Data['p3_application_exception_stack_trace'] = [string]$Failure.stack_trace
    $exception.Data['p3_application_lifecycle_record_sha256'] = [string]$Failure.record_sha256
    return $exception
}

function Add-ProcessExitTimelineEvent {
    param(
        [Collections.Generic.List[object]]$Timeline,
        [string]$Event,
        [string]$Outcome,
        [AllowNull()]$Detail
    )
    $Timeline.Add([pscustomobject][ordered]@{
        sequence = $Timeline.Count + 1
        timestamp_utc = [DateTimeOffset]::UtcNow.ToString('O')
        event = $Event
        outcome = $Outcome
        detail = $Detail
    })
}

function Assert-RunOwnedNonReparseFile {
    param(
        [string]$Path,
        [string]$OwnedRoot
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($OwnedRoot).TrimEnd('\', '/')
    if (-not (Test-PathWithin $fullPath $fullRoot) -or
        -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Runner-owned executable must be a file below the sealed binary root: $fullPath"
    }
    $cursor = $fullPath
    while ($true) {
        $item = Get-Item -LiteralPath $cursor -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Runner-owned executable identity traverses a reparse point: $cursor"
        }
        if ([string]::Equals($cursor.TrimEnd('\', '/'), $fullRoot, [StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or
            -not (Test-PathWithin $parent $fullRoot)) {
            throw "Runner-owned executable traversal escaped the sealed binary root: $cursor"
        }
        $cursor = $parent
    }
    return $fullPath
}

function Get-RunnerProcessIdentityObservation {
    param(
        [Diagnostics.Process]$Process,
        [AllowNull()][scriptblock]$ObservationProvider = $null,
        [ValidateRange(1, 200)][int]$MaxAttempts = 40,
        [ValidateRange(0, 1000)][int]$RetryDelayMilliseconds = 25
    )
    $lastObservationError = ''
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $Process.Refresh()
            if ($Process.HasExited) { throw 'The process exited before its owner token could be captured.' }
            if ($null -eq $ObservationProvider) {
                $observedPath = [string]$Process.MainModule.FileName
                $observedStart = [DateTimeOffset]$Process.StartTime.ToUniversalTime()
            } else {
                $provided = & $ObservationProvider $Process
                if ($null -eq $provided) { throw 'The process identity observation provider returned no observation.' }
                $observedPath = [string]$provided.ExecutablePath
                $observedStart = [DateTimeOffset]::Parse(
                    [string]$provided.StartTimeUtc,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [Globalization.DateTimeStyles]::RoundtripKind)
            }
            if ([string]::IsNullOrWhiteSpace($observedPath)) {
                throw 'MainModule.FileName was temporarily empty.'
            }
            $actualPath = ConvertTo-NormalizedProcessPath $observedPath
            if ([string]::IsNullOrWhiteSpace($actualPath)) {
                throw 'The normalized process executable path was temporarily empty.'
            }
            $actualSha256 = Get-FileSha256 $actualPath
            return [pscustomobject][ordered]@{
                ExecutablePath = $actualPath
                StartTime = $observedStart
                StartTimeUtc = $observedStart.ToString('O')
                ExecutableSha256 = $actualSha256
            }
        } catch {
            $lastObservationError = $_.Exception.Message
            $processExited = $false
            try {
                $Process.Refresh()
                $processExited = [bool]$Process.HasExited
            } catch {
                $lastObservationError = "$lastObservationError Process state refresh failed: $($_.Exception.Message)"
            }
            if ($processExited) { throw 'The process exited before its owner token could be captured.' }
            if ($attempt -lt $MaxAttempts -and $RetryDelayMilliseconds -gt 0) {
                Start-Sleep -Milliseconds $RetryDelayMilliseconds
            }
        }
    }
    throw "Runner-owned process identity observation did not stabilize after $MaxAttempts attempts. Last error: $lastObservationError"
}

function New-RunnerProcessOwnerToken {
    param(
        [Diagnostics.Process]$Process,
        [string]$ExpectedExecutablePath,
        [string]$ExpectedExecutableSha256,
        [string]$RunId,
        [string]$ProcessSessionId,
        [string]$RunOwnedBinaryRoot,
        [string]$RunStartedAtUtc,
        [AllowNull()][scriptblock]$IdentityObservationProvider = $null,
        [ValidateRange(1, 200)][int]$IdentityCaptureMaxAttempts = 40,
        [ValidateRange(0, 1000)][int]$IdentityCaptureRetryDelayMilliseconds = 25
    )
    if ($ProcessSessionId -cnotmatch '^[0-9a-f]{32}$') {
        throw 'Runner-owned process identity requires a preassigned lowercase hex32 process session id.'
    }
    $expectedOwnedPath = Assert-RunOwnedNonReparseFile $ExpectedExecutablePath $RunOwnedBinaryRoot
    try {
        $runStarted = [DateTimeOffset]::Parse(
            $RunStartedAtUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
        $identityObservation = Get-RunnerProcessIdentityObservation $Process $IdentityObservationProvider `
            $IdentityCaptureMaxAttempts $IdentityCaptureRetryDelayMilliseconds
        $actualPath = [string]$identityObservation.ExecutablePath
        $actualStart = [DateTimeOffset]$identityObservation.StartTime
        $actualStartTimeUtc = [string]$identityObservation.StartTimeUtc
        $actualSha256 = [string]$identityObservation.ExecutableSha256
    } catch {
        throw "Runner-owned process identity capture failed closed: $($_.Exception.Message)"
    }
    if ($actualStart -le $runStarted) {
        throw 'The launched process start time is not later than the sealed run creation time.'
    }
    if (-not [string]::Equals($actualPath, $expectedOwnedPath, [StringComparison]::OrdinalIgnoreCase) -or
        $actualSha256 -cne $ExpectedExecutableSha256) {
        throw 'The launched process identity differs from the sealed executable.'
    }
    return [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-runner-process-owner/v1'
        Pid = [int]$Process.Id
        ProcessName = [string]$Process.ProcessName
        StartTimeUtc = $actualStartTimeUtc
        ExecutablePath = $actualPath
        ExecutableSha256 = $actualSha256
        RunId = $RunId
        ProcessSessionId = $ProcessSessionId
        WindowHandle = ''
        OwnedByRun = $true
        HasExited = $false
        ObservationError = ''
    }
}

function Test-RunnerOwnedProcessIdentity {
    param(
        [Diagnostics.Process]$Process,
        $OwnerToken
    )
    $observation = New-DevPreviewProcessObservation `
        (Get-ProcessSnapshot $OwnerToken) (Get-CimProcessSnapshot $OwnerToken)
    $getProcessOwnerRows = [Collections.Generic.List[PixelTartP3ProcessIdentitySnapshotRow]]::new()
    $cimOwnerRows = [Collections.Generic.List[PixelTartP3ProcessIdentitySnapshotRow]]::new()
    $extraPids = [Collections.Generic.HashSet[int]]::new()
    foreach ($row in [PixelTartP3ProcessIdentitySnapshotRow[]]$observation.GetProcess.Items) {
        if ($row.Pid -eq [int]$OwnerToken.Pid) { $getProcessOwnerRows.Add($row) }
        else { [void]$extraPids.Add($row.Pid) }
    }
    foreach ($row in [PixelTartP3ProcessIdentitySnapshotRow[]]$observation.Cim.Items) {
        if ($row.Pid -eq [int]$OwnerToken.Pid) { $cimOwnerRows.Add($row) }
        else { [void]$extraPids.Add($row.Pid) }
    }
    $handlePath = ''
    $handleStartTimeUtc = ''
    $handleHasExited = $true
    $handleObservationError = ''
    try {
        $Process.Refresh()
        $handleHasExited = [bool]$Process.HasExited
        if (-not $handleHasExited) {
            $handlePath = ConvertTo-NormalizedProcessPath ([string]$Process.MainModule.FileName)
            $handleStartTimeUtc = $Process.StartTime.ToUniversalTime().ToString('O')
        }
    } catch { $handleObservationError = $_.Exception.Message }
    $getProcessOwner = if ($getProcessOwnerRows.Count -eq 1) { $getProcessOwnerRows[0] } else { $null }
    $cimOwner = if ($cimOwnerRows.Count -eq 1) { $cimOwnerRows[0] } else { $null }
    $valid = -not $handleHasExited -and [string]::IsNullOrWhiteSpace($handleObservationError) -and
        (Test-ProcessOwnerTokenValues $OwnerToken ([int]$Process.Id) $handlePath $handleStartTimeUtc) -and
        $getProcessOwnerRows.Count -eq 1 -and $cimOwnerRows.Count -eq 1 -and
        $getProcessOwner.OwnedByRun -and $cimOwner.OwnedByRun -and
        [string]::IsNullOrWhiteSpace($getProcessOwner.ObservationError) -and
        [string]::IsNullOrWhiteSpace($cimOwner.ObservationError) -and
        $getProcessOwner.ExecutableSha256 -ceq [string]$OwnerToken.ExecutableSha256 -and
        $cimOwner.ExecutableSha256 -ceq [string]$OwnerToken.ExecutableSha256
    return [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-runner-process-ownership-validation/v1'
        valid = [bool]$valid
        owner_pid = [int]$OwnerToken.Pid
        retained_handle_pid = [int]$Process.Id
        retained_handle_path = $handlePath
        retained_handle_start_time_utc = $handleStartTimeUtc
        retained_handle_has_exited = $handleHasExited
        retained_handle_observation_error = $handleObservationError
        get_process_owner_row_count = $getProcessOwnerRows.Count
        cim_owner_row_count = $cimOwnerRows.Count
        extra_same_name_pids = @($extraPids | Sort-Object)
        observation = $observation
    }
}

function New-DevPreviewProcessObservationSample {
    param($Observation, [int]$Sequence)
    $pids = @(Get-DevPreviewProcessPids $Observation)
    return [pscustomobject][ordered]@{
        sequence = $Sequence
        timestamp_utc = [DateTimeOffset]::UtcNow.ToString('O')
        get_process_count = $Observation.GetProcess.Items.Count
        cim_count = $Observation.Cim.Items.Count
        pids = $pids
    }
}

function Wait-DevPreviewProcessTableConvergence {
    param(
        [AllowNull()]$OwnerToken,
        [int]$TimeoutMilliseconds = 10000,
        [int]$PollIntervalMilliseconds = 100,
        [int]$RequiredConsecutiveEmptyObservations = 2,
        [AllowNull()][scriptblock]$SnapshotProvider
    )
    if ($TimeoutMilliseconds -le 0 -or $PollIntervalMilliseconds -le 0 -or $RequiredConsecutiveEmptyObservations -lt 2) {
        throw 'Process table convergence bounds are invalid.'
    }
    if ($null -eq $SnapshotProvider) {
        $SnapshotProvider = {
            param($Token)
            New-DevPreviewProcessObservation (Get-ProcessSnapshot $Token) (Get-CimProcessSnapshot $Token)
        }
    }
    $started = [Diagnostics.Stopwatch]::StartNew()
    $samples = [Collections.Generic.List[object]]::new()
    $observationFailures = [Collections.Generic.List[object]]::new()
    $consecutiveEmpty = 0
    $sequence = 0
    while ($started.ElapsedMilliseconds -le $TimeoutMilliseconds) {
        $sequence++
        try {
            $observation = & $SnapshotProvider $OwnerToken
            Assert-DevPreviewProcessObservation $observation
            $sample = New-DevPreviewProcessObservationSample $observation $sequence
            $samples.Add($sample)
            if ($sample.get_process_count -eq 0 -and $sample.cim_count -eq 0) { $consecutiveEmpty++ }
            else { $consecutiveEmpty = 0 }
            if ($consecutiveEmpty -ge $RequiredConsecutiveEmptyObservations) { break }
        } catch {
            $observationFailures.Add((ConvertTo-StructuredFailure 'observation' 'process-table-convergence' $_))
            break
        }
        $remaining = $TimeoutMilliseconds - [int]$started.ElapsedMilliseconds
        if ($remaining -le 0) { break }
        [Threading.Thread]::Sleep([Math]::Min($PollIntervalMilliseconds, $remaining))
    }
    $started.Stop()
    $lastPids = @()
    if ($samples.Count -ne 0) { $lastPids = @($samples[$samples.Count - 1].pids) }
    return [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-process-table-convergence/v1'
        timeout_milliseconds = $TimeoutMilliseconds
        poll_interval_milliseconds = $PollIntervalMilliseconds
        required_consecutive_empty_observations = $RequiredConsecutiveEmptyObservations
        observed_consecutive_empty_observations = $consecutiveEmpty
        converged = $consecutiveEmpty -ge $RequiredConsecutiveEmptyObservations
        elapsed_milliseconds = [int64]$started.ElapsedMilliseconds
        samples = @($samples)
        final_pids = $lastPids
        observation_failures = @($observationFailures)
    }
}

function Get-RequiredPropertyValue {
    param($Object, [string]$Name, [string]$Context)
    if ($null -eq $Object) { throw "$Context is null." }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "$Context is missing required property '$Name'."
    }
    return $property.Value
}

function Get-RequiredJsonIntegerValue {
    param($Object, [string]$Name, [string]$Context)
    $value = Get-RequiredPropertyValue $Object $Name $Context
    if (-not ($value -is [sbyte] -or $value -is [byte] -or $value -is [int16] -or $value -is [uint16] -or
        $value -is [int32] -or $value -is [uint32] -or $value -is [int64] -or $value -is [uint64])) {
        throw "$Context property '$Name' must be a JSON integer number."
    }
    return [int64]$value
}

function Get-RequiredJsonNumberValue {
    param($Object, [string]$Name, [string]$Context)
    $value = Get-RequiredPropertyValue $Object $Name $Context
    if (-not ($value -is [sbyte] -or $value -is [byte] -or $value -is [int16] -or $value -is [uint16] -or
        $value -is [int32] -or $value -is [uint32] -or $value -is [int64] -or $value -is [uint64] -or
        $value -is [single] -or $value -is [double] -or $value -is [decimal])) {
        throw "$Context property '$Name' must be a JSON number."
    }
    $number = [double]$value
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) {
        throw "$Context property '$Name' must be a finite JSON number."
    }
    return $number
}

function Assert-P3LifecyclePendingOperationCount {
    param($Record, [int]$Index)
    if ($Index -lt 0 -or $Index -ge $script:p3LifecyclePendingOperationCountRules.Count) {
        throw "P3 lifecycle pending-operation rule index is invalid: $Index."
    }
    $count = Get-RequiredJsonIntegerValue $Record 'pending_operation_count' "P3 lifecycle record[$Index]"
    $rule = $script:p3LifecyclePendingOperationCountRules[$Index]
    if (($rule -ceq 'zero' -and $count -ne 0) -or
        ($rule -ceq 'one' -and $count -ne 1) -or
        ($rule -ceq 'observed-nonnegative' -and $count -lt 0) -or
        ($rule -cnotin @('zero','one','observed-nonnegative'))) {
        throw "P3 lifecycle record[$Index] pending_operation_count violates rule '$rule' (actual=$count)."
    }
    return $count
}

function Read-SharedUtf8Text {
    param([string]$Path)
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.UTF8Encoding]::new($false), $true)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-P3TransientFileContention {
    param([Exception]$Exception)
    $candidate = $Exception
    while ($null -ne $candidate) {
        if ($candidate -is [IO.IOException]) {
            $win32ErrorCode = [int]($candidate.HResult -band 0xffff)
            if ($win32ErrorCode -eq 32 -or $win32ErrorCode -eq 33) {
                return [pscustomobject][ordered]@{
                    win32_error_code = $win32ErrorCode
                    hresult = [int]$candidate.HResult
                    message = [string]$candidate.Message
                }
            }
        }
        $candidate = $candidate.InnerException
    }
    return $null
}

function Get-P3LifecycleCanonicalText {
    param([string]$Line)
    $match = [regex]::Match(
        $Line,
        '^(?<prefix>\{.*),"record_sha256":"(?<hash>[0-9a-f]{64})"\}$',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'P3 lifecycle record does not use the terminal record_sha256 layout.'
    }
    return [pscustomobject][ordered]@{
        canonical = $match.Groups['prefix'].Value + '}'
        claimed_hash = $match.Groups['hash'].Value
    }
}

function Get-P3SummaryJournalCanonicalText {
    param([string]$Line)
    $match = [regex]::Match(
        $Line,
        '^(?<prefix>\{.*),"summary_hash":"(?<primary>[0-9a-f]{64})","record_sha256":"(?<alias>[0-9a-f]{64})"\}$',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'P3 summary journal record does not use the production terminal hash layout.'
    }
    if ($match.Groups['primary'].Value -cne $match.Groups['alias'].Value) {
        throw 'P3 summary journal terminal hash aliases differ.'
    }
    return [pscustomobject][ordered]@{
        canonical = $match.Groups['prefix'].Value + '}'
        claimed_hash = $match.Groups['primary'].Value
    }
}

function Get-P3EmbeddedPhaseSummaryCanonicalText {
    param([string]$Line)
    $match = [regex]::Match(
        $Line,
        '"summary":(?<prefix>\{.*),"record_sha256":"(?<hash>[0-9a-f]{64})"\},"previous_summary_hash"',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'P3 summary journal does not embed a terminal-hashed immutable phase summary.'
    }
    return [pscustomobject][ordered]@{
        canonical = $match.Groups['prefix'].Value + '}'
        claimed_hash = $match.Groups['hash'].Value
    }
}

function Read-P3LifecycleObservation {
    param(
        [string]$Path,
        $ExpectedIdentity
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject][ordered]@{
            exists = $false
            complete_record_count = 0
            has_partial_tail = $false
            events = @()
            hwnd = ''
            last_record_sha256 = ''
            application_failure = $null
            failure_tail_observation_failure = $null
        }
    }
    $text = Read-SharedUtf8Text $Path
    $parts = [regex]::Split($text, "`r?`n")
    $hasTrailingNewline = $text.EndsWith("`n", [StringComparison]::Ordinal)
    $completeCount = if ($hasTrailingNewline) { [Math]::Max(0, $parts.Count - 1) }
        else { [Math]::Max(0, $parts.Count - 1) }
    $hasPartialTail = -not $hasTrailingNewline -and
        $parts.Count -ne 0 -and -not [string]::IsNullOrWhiteSpace($parts[$parts.Count - 1])
    $records = [Collections.Generic.List[object]]::new()
    $events = [Collections.Generic.List[string]]::new()
    $previousHash = '0' * 64
    $previousTimestamp = [DateTimeOffset]::MinValue
    $previousElapsed = -1.0
    $observedHwnd = ''
    $applicationFailure = $null
    $lastLifecycleStage = 0
    $failureTailObservationFailure = $null
    try {
        for ($index = 0; $index -lt $completeCount; $index++) {
            $context = "P3 lifecycle record[$index]"
            $line = $parts[$index]
            if ([string]::IsNullOrWhiteSpace($line)) { throw "$context is empty." }
            try { $record = $line | ConvertFrom-Json -ErrorAction Stop }
            catch { throw "$context is invalid JSON: $($_.Exception.Message)" }
            $canonical = Get-P3LifecycleCanonicalText $line
            $recordHash = Get-RequiredPropertyValue $record 'record_sha256' $context
            if (-not ($recordHash -is [string]) -or (Get-TextSha256 $canonical.canonical) -cne $canonical.claimed_hash -or
                [string]$recordHash -cne $canonical.claimed_hash) {
                throw "$context hash is invalid."
            }
            $claimedPreviousHash = Get-RequiredPropertyValue $record 'previous_record_sha256' $context
            if (-not ($claimedPreviousHash -is [string]) -or [string]$claimedPreviousHash -cne $previousHash) {
                throw "$context hash chain is invalid."
            }
            $sequence = Get-RequiredJsonIntegerValue $record 'sequence' $context
            if ($sequence -ne ($index + 1)) { throw "$context sequence is invalid." }
            $eventValue = Get-RequiredPropertyValue $record 'event' $context
            if (-not ($eventValue -is [string]) -or [string]::IsNullOrWhiteSpace([string]$eventValue)) {
                throw "$context event must be a non-empty JSON string."
            }
            $event = [string]$eventValue
            $isApplicationFailure = $event -ceq 'failure-observed'
            $isFailureTailTransition = $null -ne $applicationFailure -and -not $isApplicationFailure
            $eventIndex = [Array]::IndexOf([string[]]$script:p3LifecycleEvents, $event)
            if (-not $isApplicationFailure -and -not $isFailureTailTransition -and
                ($index -ge $script:p3LifecycleEvents.Count -or $event -cne $script:p3LifecycleEvents[$index])) {
                throw "$context sequence or transition is invalid."
            }
            if ($isFailureTailTransition -and ($eventIndex -lt 0 -or (($eventIndex + 1) * 10) -le $lastLifecycleStage)) {
                throw "$context failure-tail transition is not a production-monotonic lifecycle stage."
            }
            if ((Get-RequiredJsonIntegerValue $record 'managed_thread_id' $context) -le 0 -or
                (Get-RequiredJsonIntegerValue $record 'dispatcher_thread_id' $context) -le 0) {
                throw "$context thread identity is invalid."
            }
            foreach ($binding in @(
                @('schema','schema'), @('run_id','run_id'), @('scenario_id','scenario_id'),
                @('phase','phase'), @('process_session_id','process_session_id'), @('source_head','source_head'),
                @('executable_sha256','executable_sha256'), @('application_sha256','application_sha256'),
                @('asset_module_sha256','asset_module_sha256'))) {
                $identityValue = Get-RequiredPropertyValue $record $binding[0] $context
                if (-not ($identityValue -is [string]) -or [string]$identityValue -cne
                    [string](Get-RequiredPropertyValue $ExpectedIdentity $binding[1] 'P3 expected lifecycle identity')) {
                    throw "$context identity field '$($binding[0])' is invalid."
                }
            }
            if ((Get-RequiredJsonIntegerValue $record 'pid' $context) -ne [int]$ExpectedIdentity.pid) {
                throw "$context PID is invalid."
            }
            $hwndValue = Get-RequiredPropertyValue $record 'hwnd' $context
            if (-not ($hwndValue -is [string])) { throw "$context HWND identity is invalid." }
            $hwnd = [string]$hwndValue
            if ($hwnd -cnotmatch '^0x[0-9a-fA-F]+$' -or
                (-not [string]::IsNullOrWhiteSpace($observedHwnd) -and $hwnd -cne $observedHwnd)) {
                throw "$context HWND identity is invalid."
            }
            $observedHwnd = $hwnd
            $timestampValue = Get-RequiredPropertyValue $record 'timestamp_utc' $context
            $timestamp = [DateTimeOffset]::MinValue
            if (-not ($timestampValue -is [string]) -or -not [DateTimeOffset]::TryParse(
                [string]$timestampValue,
                [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::RoundtripKind,
                [ref]$timestamp) -or $timestamp -lt $previousTimestamp) {
                throw "$context wall-clock time is invalid or moved backwards."
            }
            $elapsed = Get-RequiredJsonNumberValue $record 'stopwatch_elapsed_ms' $context
            if ($elapsed -lt $previousElapsed) { throw "$context stopwatch moved backwards." }
            if ($isApplicationFailure) {
                $resultValue = Get-RequiredPropertyValue $record 'result' $context
                $pendingValue = Get-RequiredPropertyValue $record 'pending' $context
                if (-not ($resultValue -is [string]) -or [string]$resultValue -cne 'failed' -or
                    -not ($pendingValue -is [bool]) -or [bool]$pendingValue) {
                    throw "$context application failure result or pending-state is invalid."
                }
                $pendingOperationCount = Get-RequiredJsonIntegerValue $record 'pending_operation_count' $context
                if ($pendingOperationCount -ne 0) {
                    throw "$context application failure pending_operation_count must be zero."
                }
                $exceptionProperty = $record.PSObject.Properties['exception']
                if ($null -eq $exceptionProperty -or $null -eq $exceptionProperty.Value) {
                    throw "$context application failure exception is missing."
                }
                $exceptionTypeValue = Get-RequiredPropertyValue $exceptionProperty.Value 'type' "$context application failure exception"
                $exceptionMessageValue = Get-RequiredPropertyValue $exceptionProperty.Value 'message' "$context application failure exception"
                $stackTraceProperty = $exceptionProperty.Value.PSObject.Properties['stackTrace']
                if (-not ($exceptionTypeValue -is [string]) -or -not ($exceptionMessageValue -is [string]) -or
                    [string]::IsNullOrWhiteSpace([string]$exceptionTypeValue) -or
                    [string]::IsNullOrWhiteSpace([string]$exceptionMessageValue) -or
                    $null -eq $stackTraceProperty -or
                    ($null -ne $stackTraceProperty.Value -and -not ($stackTraceProperty.Value -is [string]))) {
                    throw "$context application failure exception fields are invalid."
                }
                if ($null -eq $applicationFailure) {
                    $applicationFailure = [pscustomobject][ordered]@{
                        schema = 'pixel-tart-p3-application-reported-failure/v1'
                        lifecycle_path = $Path
                        record_index = $index
                        sequence = $sequence
                        type = [string]$exceptionTypeValue
                        message = [string]$exceptionMessageValue
                        stack_trace = $(if ($null -eq $stackTraceProperty.Value) { '' } else { [string]$stackTraceProperty.Value })
                        record_sha256 = $canonical.claimed_hash
                    }
                }
            } elseif (-not $isFailureTailTransition) {
                $resultValue = Get-RequiredPropertyValue $record 'result' $context
                $pendingValue = Get-RequiredPropertyValue $record 'pending' $context
                if (-not ($resultValue -is [string]) -or [string]$resultValue -cne $script:p3LifecycleResults[$index] -or
                    -not ($pendingValue -is [bool]) -or [bool]$pendingValue -ne [bool]$script:p3LifecyclePending[$index]) {
                    throw "$context result or pending-state contract is invalid."
                }
                [void](Assert-P3LifecyclePendingOperationCount $record $index)
                $exceptionProperty = $record.PSObject.Properties['exception']
                if ($null -eq $exceptionProperty -or $null -ne $exceptionProperty.Value) {
                    throw "$context exception state is invalid for a successful transition."
                }
                $lastLifecycleStage = ($eventIndex + 1) * 10
            } else {
                $resultValue = Get-RequiredPropertyValue $record 'result' $context
                $pendingValue = Get-RequiredPropertyValue $record 'pending' $context
                if (-not ($resultValue -is [string]) -or -not ($pendingValue -is [bool])) {
                    throw "$context failure-tail result and pending must retain their production JSON types."
                }
                $result = [string]$resultValue
                $pendingOperationCount = Get-RequiredJsonIntegerValue $record 'pending_operation_count' $context
                $rule = $script:p3LifecyclePendingOperationCountRules[$eventIndex]
                $stateIsValid = if ($event -ceq 'application-on-exit-enter') {
                    ($result -ceq 'prepared' -and -not [bool]$pendingValue -and $pendingOperationCount -eq 0) -or
                    ($result -ceq 'not-prepared' -and [bool]$pendingValue -and $pendingOperationCount -eq 1)
                } elseif ($event -ceq 'summary-commit-start') {
                    $result -ceq 'failed' -and [bool]$pendingValue -and $pendingOperationCount -eq 1
                } elseif ($event -cin @('phase-summary-written','summary-commit-end')) {
                    $result -ceq 'failed' -and -not [bool]$pendingValue -and $pendingOperationCount -eq 0
                } else {
                    $result -ceq $script:p3LifecycleResults[$eventIndex] -and
                    [bool]$pendingValue -eq [bool]$script:p3LifecyclePending[$eventIndex] -and
                    (($rule -ceq 'zero' -and $pendingOperationCount -eq 0) -or
                     ($rule -ceq 'one' -and $pendingOperationCount -eq 1) -or
                     ($rule -ceq 'observed-nonnegative' -and $pendingOperationCount -ge 0))
                }
                $exceptionProperty = $record.PSObject.Properties['exception']
                if (-not $stateIsValid -or $null -eq $exceptionProperty -or $null -ne $exceptionProperty.Value) {
                    throw "$context failure-tail state is invalid."
                }
                $lastLifecycleStage = ($eventIndex + 1) * 10
            }
            $previousTimestamp = $timestamp
            $previousElapsed = $elapsed
            $previousHash = $canonical.claimed_hash
            $records.Add($record)
            $events.Add($event)
        }
    } catch {
        if ($null -eq $applicationFailure) { throw }
        $failureTailObservationFailure = ConvertTo-StructuredFailure 'observation' 'failure-tail-validation' $_
    }
    return [pscustomobject][ordered]@{
        exists = $true
        complete_record_count = $records.Count
        has_partial_tail = $hasPartialTail
        events = @($events)
        hwnd = $observedHwnd
        last_record_sha256 = $previousHash
        records = @($records)
        application_failure = $applicationFailure
        failure_tail_observation_failure = $failureTailObservationFailure
    }
}

function Read-P3PhaseSummaryObservation {
    param(
        [string]$Path,
        $ExpectedIdentity,
        [string]$LifecycleHwnd
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject][ordered]@{
            exists = $false; payload = $null; sha256 = ''; record_sha256 = ''; hwnd = ''
        }
    }
    try { $payload = (Read-SharedUtf8Text $Path) | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "P3 immutable phase summary is invalid JSON: $($_.Exception.Message)" }
    if ([string](Get-RequiredPropertyValue $payload 'schema' 'P3 immutable phase summary') -cne
            'pixel-tart-p3-automated-summary/v1' -or
        [string](Get-RequiredPropertyValue $payload 'status' 'P3 immutable phase summary') -cne 'completed') {
        throw 'P3 immutable phase summary schema or completion status is invalid.'
    }
    foreach ($binding in @(
        @('run_id','run_id'), @('phase','phase'), @('process_session_id','process_session_id'),
        @('source_head','source_head'), @('executable_sha256','executable_sha256'),
        @('application_sha256','application_sha256'), @('asset_module_sha256','asset_module_sha256'))) {
        if ([string](Get-RequiredPropertyValue $payload $binding[0] 'P3 immutable phase summary') -cne
            [string](Get-RequiredPropertyValue $ExpectedIdentity $binding[1] 'P3 expected summary identity')) {
            throw "P3 immutable phase summary identity field '$($binding[0])' is invalid."
        }
    }
    $recordSha256 = [string](Get-RequiredPropertyValue $payload 'record_sha256' 'P3 immutable phase summary')
    if ($recordSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'P3 immutable phase summary record_sha256 is invalid.' }
    $scenario = @((Get-RequiredPropertyValue $payload 'scenarios' 'P3 immutable phase summary') |
        Where-Object { [string]$_.id -ceq [string]$ExpectedIdentity.scenario_id })
    if ($scenario.Count -ne 1) { throw 'P3 immutable phase summary has no unique expected scenario.' }
    $summaryPid = if ([string]$ExpectedIdentity.phase -ceq 'primary') { [int]$scenario[0].pid } else { [int]$scenario[0].restart_pid }
    $summaryHwnd = if ([string]$ExpectedIdentity.phase -ceq 'primary') { [string]$scenario[0].hwnd } else { [string]$scenario[0].restart_hwnd }
    if ($summaryPid -ne [int]$ExpectedIdentity.pid -or $summaryHwnd -cnotmatch '^0x[0-9a-fA-F]+$' -or
        (-not [string]::IsNullOrWhiteSpace($LifecycleHwnd) -and $summaryHwnd -cne $LifecycleHwnd)) {
        throw 'P3 immutable phase summary process identity does not match the lifecycle handshake.'
    }
    return [pscustomobject][ordered]@{
        exists = $true
        payload = $payload
        sha256 = Get-FileSha256 $Path
        record_sha256 = $recordSha256
        hwnd = $summaryHwnd
    }
}

function Read-P3SummaryJournalBindingObservation {
    param(
        [string]$Path,
        $ExpectedIdentity,
        $PhaseSummaryPayload,
        [string]$PhaseSummaryRecordSha256
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject][ordered]@{
            exists = $false
            matched = $false
            complete_record_count = 0
            sha256 = ''
            record_sha256 = ''
        }
    }
    $text = Read-SharedUtf8Text $Path
    $parts = [regex]::Split($text, "`r?`n")
    $hasTrailingNewline = $text.EndsWith("`n", [StringComparison]::Ordinal)
    $completeCount = [Math]::Max(0, $parts.Count - 1)
    $hasPartialTail = -not $hasTrailingNewline -and
        $parts.Count -ne 0 -and -not [string]::IsNullOrWhiteSpace($parts[$parts.Count - 1])
    if ($hasPartialTail) { throw 'P3 summary journal has a partial trailing record.' }

    $previousHash = '0' * 64
    $matchCount = 0
    $matchedRecordSha256 = ''
    for ($index = 0; $index -lt $completeCount; $index++) {
        $line = $parts[$index]
        if ([string]::IsNullOrWhiteSpace($line)) { throw "P3 summary journal record[$index] is empty." }
        try { $record = $line | ConvertFrom-Json -ErrorAction Stop }
        catch { throw "P3 summary journal record[$index] is invalid JSON: $($_.Exception.Message)" }
        $canonical = Get-P3SummaryJournalCanonicalText $line
        if ((Get-TextSha256 $canonical.canonical) -cne $canonical.claimed_hash -or
            [string](Get-RequiredPropertyValue $record 'summary_hash' "P3 summary journal record[$index]") -cne $canonical.claimed_hash -or
            [string](Get-RequiredPropertyValue $record 'record_sha256' "P3 summary journal record[$index]") -cne $canonical.claimed_hash) {
            throw "P3 summary journal record[$index] terminal hash is invalid."
        }
        if ([string](Get-RequiredPropertyValue $record 'previous_summary_hash' "P3 summary journal record[$index]") -cne $previousHash -or
            [string](Get-RequiredPropertyValue $record 'previous_record_sha256' "P3 summary journal record[$index]") -cne $previousHash) {
            throw "P3 summary journal record[$index] hash chain is invalid."
        }
        $embedded = Get-P3EmbeddedPhaseSummaryCanonicalText $line
        $embeddedPayload = Get-RequiredPropertyValue $record 'summary' "P3 summary journal record[$index]"
        if ((Get-TextSha256 $embedded.canonical) -cne $embedded.claimed_hash -or
            [string](Get-RequiredPropertyValue $embeddedPayload 'record_sha256' "P3 summary journal record[$index] embedded summary") -cne $embedded.claimed_hash) {
            throw "P3 summary journal record[$index] embedded phase-summary hash is invalid."
        }

        if ([string](Get-RequiredPropertyValue $record 'process_session_id' "P3 summary journal record[$index]") -ceq
                [string]$ExpectedIdentity.process_session_id) {
            $matchCount++
            foreach ($binding in @(
                @('run_id','run_id'), @('scenario_id','scenario_id'), @('phase','phase'),
                @('process_session_id','process_session_id'), @('source_head','source_head'),
                @('executable_sha256','executable_sha256'), @('application_sha256','application_sha256'),
                @('asset_module_sha256','asset_module_sha256'))) {
                if ([string](Get-RequiredPropertyValue $record $binding[0] "P3 summary journal record[$index]") -cne
                    [string](Get-RequiredPropertyValue $ExpectedIdentity $binding[1] 'P3 expected summary journal identity')) {
                    throw "P3 summary journal record[$index] identity field '$($binding[0])' is invalid."
                }
            }
            if ([string](Get-RequiredPropertyValue $record 'schema' "P3 summary journal record[$index]") -cne
                    'pixel-tart-p3-automated-summary/v1' -or
                [int](Get-RequiredPropertyValue $record 'pid' "P3 summary journal record[$index]") -ne [int]$ExpectedIdentity.pid) {
                throw "P3 summary journal record[$index] schema or PID is invalid."
            }
            $hwnd = [string](Get-RequiredPropertyValue $record 'hwnd' "P3 summary journal record[$index]")
            if ($hwnd -cnotmatch '^0x[0-9a-fA-F]+$') {
                throw "P3 summary journal record[$index] HWND is invalid."
            }
            if ($embedded.claimed_hash -cne $PhaseSummaryRecordSha256) {
                throw 'P3 immutable phase summary record hash is not bound to its summary journal record.'
            }
            $journalSemanticHash = Get-TextSha256 ($embeddedPayload | ConvertTo-Json -Depth 100 -Compress)
            $phaseSemanticHash = Get-TextSha256 ($PhaseSummaryPayload | ConvertTo-Json -Depth 100 -Compress)
            if ($journalSemanticHash -cne $phaseSemanticHash) {
                throw 'P3 immutable phase summary payload is not semantically bound to its summary journal record.'
            }
            $matchedRecordSha256 = $canonical.claimed_hash
        }
        $previousHash = $canonical.claimed_hash
    }
    if ($matchCount -gt 1) { throw 'P3 summary journal contains duplicate records for the current process session.' }
    return [pscustomobject][ordered]@{
        exists = $true
        matched = $matchCount -eq 1
        complete_record_count = $completeCount
        sha256 = Get-FileSha256 $Path
        record_sha256 = $matchedRecordSha256
    }
}

function Update-P3RetainedProcessExitState {
    param(
        [Diagnostics.Process]$Process,
        $State,
        [Collections.Generic.List[object]]$Timeline,
        [string]$Outcome = 'normal'
    )
    if ([bool]$State.observed) { return }
    $Process.Refresh()
    if (-not $Process.HasExited) { return }
    # The parameterless wait flushes redirected stdout/stderr after the process
    # handle is signalled; without it the evidence hashes can race async drains.
    $Process.WaitForExit()
    $State.observed = $true
    $State.exit_code = [int]$Process.ExitCode
    $State.observed_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    Add-ProcessExitTimelineEvent $Timeline 'process-exit-observed' $Outcome $null
    if ($Outcome -ceq 'normal') {
        Add-ProcessExitTimelineEvent $Timeline 'normal-exit' 'completed' $null
        Add-ProcessExitTimelineEvent $Timeline 'exit-code-observed' 'captured' ([pscustomobject]@{ exit_code = [int]$State.exit_code })
    } else {
        Add-ProcessExitTimelineEvent $Timeline 'premature-exit' 'failed' ([pscustomobject]@{ observation_context = $Outcome })
        Add-ProcessExitTimelineEvent $Timeline 'exit-code-observed' 'captured-after-premature-exit' `
            ([pscustomobject]@{ exit_code = [int]$State.exit_code })
    }
}

function Invoke-P3ObservedStage {
    param(
        [string]$Name,
        [int]$CapSeconds,
        [Diagnostics.Stopwatch]$SharedClock,
        [int64]$SharedBudgetMilliseconds,
        [Diagnostics.Process]$Process,
        $ProcessState,
        [Collections.Generic.List[object]]$Timeline,
        [Collections.Generic.List[object]]$Stages,
        [Collections.Generic.List[object]]$ObservationFailures,
        [scriptblock]$Probe,
        [bool]$FailIfProcessAlreadyExited = $true
    )
    $stageStarted = [int64]$SharedClock.ElapsedMilliseconds
    $stageCapMilliseconds = [int64]$CapSeconds * 1000L
    $stage = [pscustomobject][ordered]@{
        name = $Name
        cap_seconds = $CapSeconds
        started_at_elapsed_milliseconds = $stageStarted
        completed_at_elapsed_milliseconds = $null
        elapsed_milliseconds = 0
        transient_file_contention_count = 0
        last_transient_file_contention = $null
        outcome = 'waiting'
        detail = $null
    }
    $pendingApplicationFailure = $null
    $applicationFailureAnnounced = $false
    $readFinalFailureTailAfterProcessExit = {
        param($ApplicationFailure)
        $finalObservation = $null
        try { $finalObservation = & $Probe }
        catch {
            $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-final-read-after-process-exit' $_))
            return
        }
        if ($null -eq $finalObservation) {
            $missingFinalObservation = [IO.InvalidDataException]::new(
                'P3 lifecycle final failure-tail read returned no observation after process exit.')
            $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-final-read-after-process-exit' $missingFinalObservation))
            return
        }
        $finalTailFailureProperty = $finalObservation.PSObject.Properties['failure_tail_observation_failure']
        if ($null -ne $finalTailFailureProperty -and $null -ne $finalTailFailureProperty.Value) {
            $ObservationFailures.Add($finalTailFailureProperty.Value)
            return
        }
        $finalApplicationFailureProperty = $finalObservation.PSObject.Properties['application_failure']
        if ($null -eq $finalApplicationFailureProperty -or $null -eq $finalApplicationFailureProperty.Value -or
            [string]$finalApplicationFailureProperty.Value.record_sha256 -cne [string]$ApplicationFailure.record_sha256) {
            $changedFinalObservation = [IO.InvalidDataException]::new(
                'P3 lifecycle final failure-tail read no longer contained the authenticated application failure record.')
            $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-final-read-after-process-exit' $changedFinalObservation))
            return
        }
        $finalPartialTailProperty = $finalObservation.PSObject.Properties['has_partial_tail']
        if ($null -eq $finalPartialTailProperty -or [bool]$finalPartialTailProperty.Value) {
            $partialTailFailure = [IO.InvalidDataException]::new(
                'P3 lifecycle retained a partial failure tail after the application process exited naturally.')
            $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-partial-after-process-exit' $partialTailFailure))
        }
    }
    try {
        while ($true) {
            $stageElapsed = [int64]$SharedClock.ElapsedMilliseconds - $stageStarted
            $sharedRemaining = $SharedBudgetMilliseconds - [int64]$SharedClock.ElapsedMilliseconds
            $stageRemaining = $stageCapMilliseconds - $stageElapsed
            if ($sharedRemaining -le 0 -or $stageRemaining -le 0) {
                if ($null -ne $pendingApplicationFailure) {
                    try { Update-P3RetainedProcessExitState $Process $ProcessState $Timeline 'application-reported-failure-partial-tail' }
                    catch {
                        $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-process-state-at-deadline' $_))
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                    if ([bool]$ProcessState.observed) {
                        & $readFinalFailureTailAfterProcessExit $pendingApplicationFailure
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                    $partialTailFailure = [IO.InvalidDataException]::new(
                        "P3 lifecycle retained a partial failure tail until stage '$Name' reached its original deadline.")
                    $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-partial-at-deadline' $partialTailFailure))
                    $stage.outcome = 'application-reported-failure'
                    $stage.detail = $pendingApplicationFailure
                    throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                }
                $timeout = [TimeoutException]::new(
                    "P3 app phase stage '$Name' timed out (stage cap ${CapSeconds}s; shared budget $([int]($SharedBudgetMilliseconds / 1000))s).")
                $timeout.Data['p3_stage'] = $Name
                throw $timeout
            }
            $observation = $null
            $transientContentionObserved = $false
            try { $observation = & $Probe }
            catch {
                $contention = Get-P3TransientFileContention $_.Exception
                if ($null -ne $contention) {
                    $transientContentionObserved = $true
                    $stage.transient_file_contention_count = [int]$stage.transient_file_contention_count + 1
                    $stage.last_transient_file_contention = [pscustomobject][ordered]@{
                        observed_at_elapsed_milliseconds = [int64]$SharedClock.ElapsedMilliseconds
                        win32_error_code = [int]$contention.win32_error_code
                        hresult = [int]$contention.hresult
                        message = [string]$contention.message
                    }
                } else {
                    $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' $Name $_))
                    if ($null -ne $pendingApplicationFailure) {
                        $stage.outcome = 'application-reported-failure'
                        $stage.detail = $pendingApplicationFailure
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                    $_.Exception.Data['p3_stage'] = $Name
                    $_.Exception.Data['p3_observation_failure'] = $true
                    throw
                }
            }
            if ($null -ne $observation) {
                $tailFailureProperty = $observation.PSObject.Properties['failure_tail_observation_failure']
                if ($null -ne $tailFailureProperty -and $null -ne $tailFailureProperty.Value) {
                    $ObservationFailures.Add($tailFailureProperty.Value)
                }
                $applicationFailureProperty = $observation.PSObject.Properties['application_failure']
                if ($null -ne $applicationFailureProperty -and $null -ne $applicationFailureProperty.Value) {
                    if ($null -eq $pendingApplicationFailure) {
                        $pendingApplicationFailure = $applicationFailureProperty.Value
                    }
                    $stage.outcome = 'application-reported-failure'
                    $stage.detail = $pendingApplicationFailure
                    if (-not $applicationFailureAnnounced) {
                        Add-ProcessExitTimelineEvent $Timeline 'application-reported-failure' 'authenticated' `
                            $pendingApplicationFailure
                        $applicationFailureAnnounced = $true
                    }
                    $partialTailProperty = $observation.PSObject.Properties['has_partial_tail']
                    $hasPartialFailureTail = $null -ne $partialTailProperty -and [bool]$partialTailProperty.Value
                    if (($null -ne $tailFailureProperty -and $null -ne $tailFailureProperty.Value) -or
                        -not $hasPartialFailureTail) {
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                }
            }
            if ($null -ne $pendingApplicationFailure) {
                if (-not $transientContentionObserved) {
                    try { Update-P3RetainedProcessExitState $Process $ProcessState $Timeline 'application-reported-failure-partial-tail' }
                    catch {
                        $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-process-state' $_))
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                    if ([bool]$ProcessState.observed) {
                        & $readFinalFailureTailAfterProcessExit $pendingApplicationFailure
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                }
                $waitSharedRemaining = $SharedBudgetMilliseconds - [int64]$SharedClock.ElapsedMilliseconds
                $waitStageRemaining = $stageCapMilliseconds - ([int64]$SharedClock.ElapsedMilliseconds - $stageStarted)
                $waitMilliseconds = [int][Math]::Min(100, [Math]::Min($waitSharedRemaining, $waitStageRemaining))
                if ($waitMilliseconds -gt 0) {
                    $processExitedDuringWait = $false
                    try {
                        if ($Process.WaitForExit($waitMilliseconds)) {
                            Update-P3RetainedProcessExitState $Process $ProcessState $Timeline `
                                'application-reported-failure-partial-tail'
                            $processExitedDuringWait = $true
                        }
                    } catch {
                        $ObservationFailures.Add((ConvertTo-StructuredFailure 'observation' 'failure-tail-exit-wait' $_))
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                    if ($processExitedDuringWait) {
                        & $readFinalFailureTailAfterProcessExit $pendingApplicationFailure
                        throw (New-P3ApplicationReportedFailureException $pendingApplicationFailure)
                    }
                }
                continue
            }
            if ($null -ne $observation -and [bool]$observation.satisfied) {
                $stage.outcome = 'observed'
                $stage.detail = $observation.detail
                return $observation.detail
            }
            if ($FailIfProcessAlreadyExited -and -not $transientContentionObserved) {
                Update-P3RetainedProcessExitState $Process $ProcessState $Timeline 'premature'
                if ([bool]$ProcessState.observed) {
                    $missing = [InvalidOperationException]::new(
                        "P3 app process exited before required stage '$Name' evidence was complete.")
                    $missing.Data['p3_stage'] = $Name
                    throw $missing
                }
            }
            $sleepMilliseconds = [int][Math]::Min(100, [Math]::Min($sharedRemaining, $stageRemaining))
            if ($sleepMilliseconds -gt 0) { [Threading.Thread]::Sleep($sleepMilliseconds) }
        }
    } finally {
        $stage.completed_at_elapsed_milliseconds = [int64]$SharedClock.ElapsedMilliseconds
        $stage.elapsed_milliseconds = [int64]$SharedClock.ElapsedMilliseconds - $stageStarted
        if ($stage.outcome -ceq 'waiting') { $stage.outcome = 'failed' }
        $Stages.Add($stage)
    }
}

function Wait-RunnerOwnedProcessExit {
    param(
        [Diagnostics.Process]$Process,
        $OwnerToken,
        [int]$ExecutionTimeoutSeconds,
        [string]$Phase,
        [string]$SessionName,
        [string]$ScenarioId,
        [string]$LifecyclePath,
        [string]$PhaseSummaryPath,
        [string]$SummaryJournalPath,
        $ExpectedIdentity,
        [ref]$Diagnostic
    )
    if ($ExecutionTimeoutSeconds -le 0) { throw 'P3 process execution timeout must be positive.' }
    $timeline = [Collections.Generic.List[object]]::new()
    $stages = [Collections.Generic.List[object]]::new()
    $cleanupFailures = [Collections.Generic.List[object]]::new()
    $observationFailures = [Collections.Generic.List[object]]::new()
    $processState = [pscustomobject][ordered]@{ observed = $false; exit_code = $null; observed_at_utc = '' }
    $sharedBudgetSeconds = [Math]::Min($ExecutionTimeoutSeconds, $script:p3ProcessStageTotalTimeoutSeconds)
    $sharedBudgetMilliseconds = [int64]$sharedBudgetSeconds * 1000L
    $diagnosticValue = [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-runner-process-exit-diagnostic/v1'
        phase = $Phase
        session_name = $SessionName
        scenario_id = $ScenarioId
        owner = $OwnerToken
        execution_wait = [pscustomobject][ordered]@{
            strategy = 'shared-deadline-staged-evidence-and-exit'
            timeout_seconds = $sharedBudgetSeconds
            slice_milliseconds = 100
            stage_caps_seconds = $script:p3ProcessStageTimeoutSeconds
            stages = $stages
            elapsed_milliseconds = 0
            process_exit_observed = $false
        }
        forced_cleanup = [pscustomobject][ordered]@{
            required = $false
            started = $false
            owner_identity = $null
            owner_identity_verified = $false
            kill_requested_through_retained_handle = $false
            process_exit_observed = $false
            completed = $false
        }
        process_table_convergence = $null
        primary_failure = $null
        cleanup_failures = $cleanupFailures
        observation_failures = $observationFailures
        timeline = $timeline
        outcome = 'running'
    }
    $Diagnostic.Value = $diagnosticValue
    Add-ProcessExitTimelineEvent $timeline 'execution-wait-start' 'started' ([pscustomobject]@{
        timeout_seconds = $sharedBudgetSeconds
        stage_caps_seconds = $script:p3ProcessStageTimeoutSeconds
    })
    $sharedClock = [Diagnostics.Stopwatch]::StartNew()
    $primaryException = $null
    try {
        $completion = Invoke-P3ObservedStage 'completion-handshake' $script:p3ProcessStageTimeoutSeconds.completion_handshake `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                $lifecycle = Read-P3LifecycleObservation $LifecyclePath $ExpectedIdentity
                [pscustomobject]@{
                    satisfied = $lifecycle.complete_record_count -ge 2
                    application_failure = $lifecycle.application_failure
                    has_partial_tail = $lifecycle.has_partial_tail
                    failure_tail_observation_failure = $lifecycle.failure_tail_observation_failure
                    detail = [pscustomobject]@{
                        lifecycle_path = $LifecyclePath
                        complete_record_count = $lifecycle.complete_record_count
                        events = @($lifecycle.events)
                        hwnd = $lifecycle.hwnd
                    }
                }
            }
        Add-ProcessExitTimelineEvent $timeline 'completion-handshake-observed' 'matched' $completion

        $shutdown = Invoke-P3ObservedStage 'shutdown-preparation' $script:p3ProcessStageTimeoutSeconds.shutdown_preparation `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                $lifecycle = Read-P3LifecycleObservation $LifecyclePath $ExpectedIdentity
                [pscustomobject]@{
                    satisfied = $lifecycle.complete_record_count -ge 9
                    application_failure = $lifecycle.application_failure
                    has_partial_tail = $lifecycle.has_partial_tail
                    failure_tail_observation_failure = $lifecycle.failure_tail_observation_failure
                    detail = [pscustomobject]@{ complete_record_count = $lifecycle.complete_record_count; events = @($lifecycle.events) }
                }
            }
        Add-ProcessExitTimelineEvent $timeline 'shutdown-preparation-observed' 'completed' $shutdown

        $onExit = Invoke-P3ObservedStage 'application-on-exit-enter' $script:p3ProcessStageTimeoutSeconds.application_on_exit_enter `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                $lifecycle = Read-P3LifecycleObservation $LifecyclePath $ExpectedIdentity
                [pscustomobject]@{
                    satisfied = $lifecycle.complete_record_count -ge 13
                    application_failure = $lifecycle.application_failure
                    has_partial_tail = $lifecycle.has_partial_tail
                    failure_tail_observation_failure = $lifecycle.failure_tail_observation_failure
                    detail = [pscustomobject]@{ complete_record_count = $lifecycle.complete_record_count; events = @($lifecycle.events); hwnd = $lifecycle.hwnd }
                }
            }
        Add-ProcessExitTimelineEvent $timeline 'application-on-exit-enter-observed' 'entered' $onExit

        $summaryCommit = Invoke-P3ObservedStage 'phase-summary-commit' $script:p3ProcessStageTimeoutSeconds.phase_summary_commit `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                $lifecycle = Read-P3LifecycleObservation $LifecyclePath $ExpectedIdentity
                if ($null -ne $lifecycle.application_failure) {
                    return [pscustomobject]@{
                        satisfied = $false
                        application_failure = $lifecycle.application_failure
                        has_partial_tail = $lifecycle.has_partial_tail
                        failure_tail_observation_failure = $lifecycle.failure_tail_observation_failure
                        detail = $null
                    }
                }
                $summary = Read-P3PhaseSummaryObservation $PhaseSummaryPath $ExpectedIdentity $lifecycle.hwnd
                $summaryJournal = if ($lifecycle.complete_record_count -ge 16 -and [bool]$summary.exists) {
                    Read-P3SummaryJournalBindingObservation $SummaryJournalPath $ExpectedIdentity `
                        $summary.payload $summary.record_sha256
                } else {
                    [pscustomobject]@{ exists = $false; matched = $false; complete_record_count = 0; sha256 = ''; record_sha256 = '' }
                }
                [pscustomobject]@{
                    satisfied = $lifecycle.complete_record_count -ge 16 -and [bool]$summary.exists -and [bool]$summaryJournal.matched
                    application_failure = $null
                    has_partial_tail = $lifecycle.has_partial_tail
                    failure_tail_observation_failure = $null
                    detail = [pscustomobject]@{
                        lifecycle_path = $LifecyclePath
                        lifecycle_record_count = $lifecycle.complete_record_count
                        lifecycle_last_record_sha256 = $lifecycle.last_record_sha256
                        phase_summary_path = $PhaseSummaryPath
                        phase_summary_sha256 = $summary.sha256
                        phase_summary_record_sha256 = $summary.record_sha256
                        summary_journal_path = $SummaryJournalPath
                        summary_journal_sha256 = $summaryJournal.sha256
                        summary_journal_record_sha256 = $summaryJournal.record_sha256
                        summary_journal_record_count = $summaryJournal.complete_record_count
                        hwnd = $summary.hwnd
                    }
                }
            }
        Add-ProcessExitTimelineEvent $timeline 'summary-observed' 'atomic-commit-observed' $summaryCommit

        $onExitCompleted = Invoke-P3ObservedStage 'application-on-exit-completed' $script:p3ProcessStageTimeoutSeconds.application_on_exit_completed `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                $lifecycle = Read-P3LifecycleObservation $LifecyclePath $ExpectedIdentity
                [pscustomobject]@{
                    satisfied = $lifecycle.complete_record_count -eq $script:p3LifecycleEvents.Count -and -not $lifecycle.has_partial_tail
                    application_failure = $lifecycle.application_failure
                    has_partial_tail = $lifecycle.has_partial_tail
                    failure_tail_observation_failure = $lifecycle.failure_tail_observation_failure
                    detail = [pscustomobject]@{
                        lifecycle_path = $LifecyclePath
                        lifecycle_sha256 = $(if ($lifecycle.complete_record_count -eq $script:p3LifecycleEvents.Count -and -not $lifecycle.has_partial_tail) { Get-FileSha256 $LifecyclePath } else { '' })
                        lifecycle_record_count = $lifecycle.complete_record_count
                        lifecycle_last_record_sha256 = $lifecycle.last_record_sha256
                        hwnd = $lifecycle.hwnd
                    }
                }
            }
        Add-ProcessExitTimelineEvent $timeline 'application-on-exit-completed-observed' 'completed' $onExitCompleted
        Add-ProcessExitTimelineEvent $timeline 'application-lifecycle-observed' 'complete-and-hash-checked' $onExitCompleted

        $exitDetail = Invoke-P3ObservedStage 'process-exit' $script:p3ProcessStageTimeoutSeconds.process_exit `
            $sharedClock $sharedBudgetMilliseconds $Process $processState $timeline $stages $observationFailures {
                Update-P3RetainedProcessExitState $Process $processState $timeline
                [pscustomobject]@{
                    satisfied = [bool]$processState.observed
                    detail = [pscustomobject]@{ exit_code = $processState.exit_code; observed_at_utc = $processState.observed_at_utc }
                }
            } $false
        $diagnosticValue.execution_wait.process_exit_observed = $true
        if ([int]$exitDetail.exit_code -ne 0) {
            $nonzero = [InvalidOperationException]::new("Automated app phase '$Phase' exited with non-zero code $([int]$exitDetail.exit_code).")
            $nonzero.Data['p3_stage'] = 'process-exit-code'
            throw $nonzero
        }

        $convergenceStageStarted = [int64]$sharedClock.ElapsedMilliseconds
        $remainingMilliseconds = $sharedBudgetMilliseconds - $convergenceStageStarted
        if ($remainingMilliseconds -le 0) {
            $deadline = [TimeoutException]::new(
                "P3 app phase exhausted its shared ${sharedBudgetSeconds}s deadline before process-table convergence.")
            $deadline.Data['p3_stage'] = 'process-table-convergence'
            throw $deadline
        }
        $convergenceTimeout = [int][Math]::Min(
            [int64]$script:p3ProcessStageTimeoutSeconds.process_table_convergence * 1000L,
            $remainingMilliseconds)
        $convergence = Wait-DevPreviewProcessTableConvergence $OwnerToken $convergenceTimeout 100 2
        $diagnosticValue.process_table_convergence = $convergence
        foreach ($failure in @($convergence.observation_failures)) { $observationFailures.Add($failure) }
        $convergenceCompleted = [int64]$sharedClock.ElapsedMilliseconds
        $stages.Add([pscustomobject][ordered]@{
            name = 'process-table-convergence'
            cap_seconds = $script:p3ProcessStageTimeoutSeconds.process_table_convergence
            started_at_elapsed_milliseconds = $convergenceStageStarted
            completed_at_elapsed_milliseconds = $convergenceCompleted
            elapsed_milliseconds = $convergenceCompleted - $convergenceStageStarted
            outcome = $(if ($convergence.converged) { 'observed' } else { 'failed' })
            detail = $convergence
        })
        if ($convergenceCompleted -gt $sharedBudgetMilliseconds) {
            $deadline = [TimeoutException]::new(
                "P3 app phase exceeded its shared ${sharedBudgetSeconds}s deadline during process-table convergence.")
            $deadline.Data['p3_stage'] = 'process-table-convergence'
            throw $deadline
        }
        if (-not $convergence.converged) {
            $message = if ($observationFailures.Count -ne 0) {
                'DevPreview process-table observation failed closed after normal process exit.'
            } else {
                "DevPreview process tables did not converge to empty after normal process exit; PID(s): $(@($convergence.final_pids) -join ', ')."
            }
            $exception = [InvalidOperationException]::new($message)
            $exception.Data['p3_stage'] = 'process-table-convergence'
            throw $exception
        }
        Add-ProcessExitTimelineEvent $timeline 'same-run-process-zero' 'observed' ([pscustomobject]@{
            consecutive_empty_observations = $convergence.observed_consecutive_empty_observations
        })
    } catch {
        $primaryException = $_.Exception
        $failureStage = if ($null -ne $primaryException.Data['p3_stage']) { [string]$primaryException.Data['p3_stage'] } else { 'staged-process-exit' }
        $diagnosticValue.primary_failure = ConvertTo-StructuredFailure 'primary' $failureStage $_
    }

    if ($null -ne $primaryException) {
        try { Update-P3RetainedProcessExitState $Process $processState $timeline 'failure-observation' }
        catch { $observationFailures.Add((ConvertTo-StructuredFailure 'observation' 'post-failure-process-state' $_)) }
        $isApplicationReportedFailure = $primaryException.Data.Contains('p3_application_reported_failure') -and
            [bool]$primaryException.Data['p3_application_reported_failure']
        if ($isApplicationReportedFailure -and -not [bool]$processState.observed) {
            $naturalExitRemainingMilliseconds = $sharedBudgetMilliseconds - [int64]$sharedClock.ElapsedMilliseconds
            $naturalExitWaitMilliseconds = [int][Math]::Min(
                [int64]$script:p3ProcessStageTimeoutSeconds.process_exit * 1000L,
                [Math]::Max(0L, $naturalExitRemainingMilliseconds))
            if ($naturalExitWaitMilliseconds -gt 0) {
                Add-ProcessExitTimelineEvent $timeline 'application-reported-failure-exit-wait' 'started' `
                    ([pscustomobject]@{ timeout_milliseconds = $naturalExitWaitMilliseconds })
                try {
                    if ($Process.WaitForExit($naturalExitWaitMilliseconds)) {
                        Update-P3RetainedProcessExitState $Process $processState $timeline 'application-reported-failure'
                    }
                } catch {
                    $observationFailures.Add((ConvertTo-StructuredFailure 'observation' 'application-reported-failure-exit-wait' $_))
                }
                Add-ProcessExitTimelineEvent $timeline 'application-reported-failure-exit-wait' `
                    $(if ([bool]$processState.observed) { 'natural-exit-observed' } else { 'timed-out' }) $null
            }
        }
        if (-not [bool]$processState.observed) {
            $diagnosticValue.forced_cleanup.required = $true
            $diagnosticValue.forced_cleanup.started = $true
            Add-ProcessExitTimelineEvent $timeline 'forced-cleanup-start' 'started' ([pscustomobject]@{ owner_pid = [int]$OwnerToken.Pid })
            try {
                Update-P3RetainedProcessExitState $Process $processState $timeline 'pre-forced-cleanup'
                if ([bool]$processState.observed) {
                    $diagnosticValue.forced_cleanup.required = $false
                    $diagnosticValue.forced_cleanup.process_exit_observed = $true
                    Add-ProcessExitTimelineEvent $timeline 'forced-cleanup-skipped' 'process-already-exited' `
                        ([pscustomobject]@{ exit_code = [int]$processState.exit_code })
                } else {
                    $ownership = Test-RunnerOwnedProcessIdentity $Process $OwnerToken
                    $diagnosticValue.forced_cleanup.owner_identity = $ownership
                    $diagnosticValue.forced_cleanup.owner_identity_verified = [bool]$ownership.valid
                    if (-not $ownership.valid) {
                        Update-P3RetainedProcessExitState $Process $processState $timeline 'post-owner-check'
                        if (-not [bool]$processState.observed) {
                            throw 'Runner-owned process identity could not be proven; refusing to kill by PID.'
                        }
                    }
                    if ([bool]$processState.observed) {
                        $diagnosticValue.forced_cleanup.required = $false
                        $diagnosticValue.forced_cleanup.process_exit_observed = $true
                        Add-ProcessExitTimelineEvent $timeline 'forced-cleanup-skipped' 'process-exited-during-owner-check' `
                            ([pscustomobject]@{ exit_code = [int]$processState.exit_code })
                    } else {
                        try {
                            $Process.Kill()
                            $diagnosticValue.forced_cleanup.kill_requested_through_retained_handle = $true
                        } catch {
                            Update-P3RetainedProcessExitState $Process $processState $timeline 'kill-race'
                            if (-not [bool]$processState.observed) { throw }
                        }
                        if ([bool]$processState.observed) {
                            $diagnosticValue.forced_cleanup.required = $false
                            $diagnosticValue.forced_cleanup.process_exit_observed = $true
                            Add-ProcessExitTimelineEvent $timeline 'forced-cleanup-skipped' 'process-exited-before-kill' `
                                ([pscustomobject]@{ exit_code = [int]$processState.exit_code })
                        } else {
                            if (-not $Process.WaitForExit(10000)) {
                                throw 'Runner-owned process did not exit within the bounded 10000 ms cleanup interval.'
                            }
                            $Process.WaitForExit()
                            $processState.observed = $true
                            $processState.exit_code = [int]$Process.ExitCode
                            $processState.observed_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
                            $diagnosticValue.forced_cleanup.process_exit_observed = $true
                            Add-ProcessExitTimelineEvent $timeline 'process-exit-observed' 'forced-cleanup' $null
                        }
                    }
                }
            } catch {
                $cleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' 'owner-scoped-process-termination' $_))
            } finally {
                $diagnosticValue.forced_cleanup.completed = $true
                Add-ProcessExitTimelineEvent $timeline 'forced-cleanup-completed' $(if ($cleanupFailures.Count -eq 0) { 'completed' } else { 'failed' }) $null
            }
        }
        if ($null -eq $diagnosticValue.process_table_convergence -or -not $diagnosticValue.process_table_convergence.converged) {
            try {
                $convergence = Wait-DevPreviewProcessTableConvergence $OwnerToken 10000 100 2
                $diagnosticValue.process_table_convergence = $convergence
                foreach ($failure in @($convergence.observation_failures)) { $observationFailures.Add($failure) }
                if (-not $convergence.converged -and $convergence.observation_failures.Count -eq 0) {
                    $cleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' 'post-failure-process-table-convergence' `
                        "DevPreview process tables retained PID(s): $(@($convergence.final_pids) -join ', ')."))
                }
                if ($convergence.converged) {
                    Add-ProcessExitTimelineEvent $timeline 'same-run-process-zero' 'observed' ([pscustomobject]@{
                        consecutive_empty_observations = $convergence.observed_consecutive_empty_observations
                    })
                }
            } catch {
                $observationFailures.Add((ConvertTo-StructuredFailure 'observation' 'post-failure-process-table-convergence' $_))
            }
        }
        $sharedClock.Stop()
        $diagnosticValue.execution_wait.elapsed_milliseconds = [int64]$sharedClock.ElapsedMilliseconds
        $diagnosticValue.execution_wait.process_exit_observed = [bool]$processState.observed
        $diagnosticValue.outcome = 'failed'
        $primaryException.Data['process_exit_diagnostic'] = $diagnosticValue | ConvertTo-Json -Compress -Depth 30
        $primaryException.Data['cleanup_failures'] = @($cleanupFailures) | ConvertTo-Json -Compress -Depth 12
        $primaryException.Data['observation_failures'] = @($observationFailures) | ConvertTo-Json -Compress -Depth 12
        throw $primaryException
    }

    $sharedClock.Stop()
    $diagnosticValue.execution_wait.elapsed_milliseconds = [int64]$sharedClock.ElapsedMilliseconds
    $diagnosticValue.execution_wait.process_exit_observed = $true
    $diagnosticValue.outcome = 'process-exited-and-tables-empty'
    return $diagnosticValue
}

function Invoke-FinalDevPreviewCheck {
    param(
        [AllowNull()][Exception]$PrimaryException,
        [AllowNull()][Collections.Generic.List[object]]$CleanupFailures,
        [AllowNull()][Collections.Generic.List[object]]$ObservationFailures,
        [int]$TimeoutMilliseconds = 5000,
        [AllowNull()][scriptblock]$SnapshotProvider
    )
    if ($null -eq $CleanupFailures) { $CleanupFailures = [Collections.Generic.List[object]]::new() }
    if ($null -eq $ObservationFailures) { $ObservationFailures = [Collections.Generic.List[object]]::new() }
    $convergence = Wait-DevPreviewProcessTableConvergence $null $TimeoutMilliseconds 100 2 $SnapshotProvider
    foreach ($failure in @($convergence.observation_failures)) { $ObservationFailures.Add($failure) }
    if ($convergence.converged) { return $convergence }
    $message = if ($ObservationFailures.Count -ne 0) {
        'Final DevPreview process observation failed closed.'
    } else {
        "Final DevPreview process cleanup did not converge; PID(s): $(@($convergence.final_pids) -join ', ')."
    }
    $failure = [InvalidOperationException]::new($message)
    if ($ObservationFailures.Count -eq 0) {
        $CleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' 'final-process-table-convergence' $failure))
    }
    if ($null -ne $PrimaryException) {
        $PrimaryException.Data['devpreview_cleanup_failure'] = $message
        $PrimaryException.Data['cleanup_failures'] = @($CleanupFailures) | ConvertTo-Json -Compress -Depth 12
        $PrimaryException.Data['observation_failures'] = @($ObservationFailures) | ConvertTo-Json -Compress -Depth 12
        return $convergence
    }
    throw $failure
}

function New-P3FinalFailureException {
    param(
        [string]$ActiveRunRoot,
        [Exception]$PrimaryException,
        [AllowEmptyCollection()][object[]]$CleanupFailures,
        [AllowEmptyCollection()][object[]]$ObservationFailures
    )
    $message = "P3 automated acceptance failed. Run root retained: $ActiveRunRoot`n$($PrimaryException.Message)"
    $outer = [InvalidOperationException]::new($message, $PrimaryException)
    $outer.Data['primary_failure'] = (ConvertTo-StructuredFailure 'primary' 'run' $PrimaryException) | ConvertTo-Json -Compress -Depth 12
    $outer.Data['cleanup_failures'] = @($CleanupFailures) | ConvertTo-Json -Compress -Depth 12
    $outer.Data['observation_failures'] = @($ObservationFailures) | ConvertTo-Json -Compress -Depth 12
    return $outer
}

function Get-DisplayObservation {
    if (-not ('PixelTartP3AutomatedDisplayObservation' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class PixelTartP3AutomatedDisplayObservation
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);
}
'@
    }
    $appliedDpi = $null
    try { $appliedDpi = (Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction Stop).AppliedDPI } catch { }
    return [ordered]@{
        primary_width = [PixelTartP3AutomatedDisplayObservation]::GetSystemMetrics(0)
        primary_height = [PixelTartP3AutomatedDisplayObservation]::GetSystemMetrics(1)
        applied_dpi = $appliedDpi
    }
}

function Test-SameDisplayObservation {
    param($Before, $After)
    return $Before.primary_width -eq $After.primary_width -and
        $Before.primary_height -eq $After.primary_height -and
        [object]::Equals($Before.applied_dpi, $After.applied_dpi)
}

function Invoke-WithEnvironment {
    param([hashtable]$Values, [scriptblock]$Action)
    $before = @{}
    foreach ($key in $Values.Keys) {
        $before[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $Values[$key], 'Process')
    }
    try { return & $Action } finally {
        foreach ($key in $before.Keys) { [Environment]::SetEnvironmentVariable($key, $before[$key], 'Process') }
    }
}

function Write-JsonAtomic {
    param([string]$Path, $Value)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $parent = Split-Path -Parent $fullPath
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $writeToken = [guid]::NewGuid().ToString('N')
    $temporary = Join-Path $parent ('.{0}.{1}.tmp' -f [IO.Path]::GetFileName($fullPath), $writeToken)
    $backup = Join-Path $parent ('.{0}.{1}.bak' -f [IO.Path]::GetFileName($fullPath), $writeToken)
    $json = $Value | ConvertTo-Json -Depth 20
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $committed = $false
    try {
        $stream = [IO.FileStream]::new(
            $temporary,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None,
            4096,
            [IO.FileOptions]::WriteThrough)
        try {
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        if ([IO.File]::Exists($fullPath)) {
            [IO.File]::Replace($temporary, $fullPath, $backup)
        } else {
            try { [IO.File]::Move($temporary, $fullPath) }
            catch {
                if (-not [IO.File]::Exists($fullPath)) { throw }
                [IO.File]::Replace($temporary, $fullPath, $backup)
            }
        }
        $committed = $true
    } finally {
        # The unique staging name is owned by this invocation. Never delete a
        # shared wildcard, a caller path, or another writer's staging file.
        if (-not $committed -and [IO.File]::Exists($temporary)) {
            [IO.File]::Delete($temporary)
        }
        # A successful Replace has already durably committed the target, so its
        # invocation-owned backup is no longer needed.  If Replace itself fails,
        # retain the backup rather than deleting the only recoverable prior value.
        if ($committed -and [IO.File]::Exists($backup)) { [IO.File]::Delete($backup) }
    }
}

function Test-IsAbsolutePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) { return $false }
    $root = [IO.Path]::GetPathRoot($Path)
    return $root -match '^[A-Za-z]:\\$' -or $root.StartsWith('\\')
}

function Test-PathWithin {
    param([string]$Path, [string]$Root)
    if (-not (Test-IsAbsolutePath $Path) -or -not (Test-IsAbsolutePath $Root)) { return $false }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return [string]::Equals($full, $base, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Get-FileSha256 {
    param([string]$Path)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '') }
        finally { $sha.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-TextSha256 {
    param([AllowEmptyString()][string]$Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

function Get-BinarySnapshotTreeSha256 {
    param([object[]]$Rows)
    $lines = @($Rows | ForEach-Object {
        "{0}|{1}|{2}" -f ([string]$_.path), ([int64]$_.byte_length), ([string]$_.sha256)
    })
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

function Assert-BinarySnapshotState {
    param([AllowNull()]$Snapshot)
    if ($null -eq $Snapshot) { throw 'The run-owned binary snapshot is missing.' }
    $directory = [IO.Path]::GetFullPath([string]$Snapshot.directory).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw "The run-owned binary snapshot directory is missing: $directory" }
    $entries = @(Get-ChildItem -LiteralPath $directory -Recurse -Force -ErrorAction Stop)
    foreach ($entry in $entries) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "The run-owned binary snapshot contains a reparse point: $($entry.FullName)" }
    }
    $files = @($entries | Where-Object { -not $_.PSIsContainer })
    $rows = @($Snapshot.files)
    if ($files.Count -ne $rows.Count -or [int]$Snapshot.file_count -ne $rows.Count) { throw 'The run-owned binary snapshot file set changed.' }
    $liveRows = [Collections.Generic.List[object]]::new()
    foreach ($row in $rows) {
        $relative = [string]$row.path
        if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative -match '(^|/)\.\.?(/|$)') { throw "The binary snapshot path is not canonical: '$relative'." }
        $full = [IO.Path]::GetFullPath((Join-Path $directory $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
        if (-not $full.StartsWith($directory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "The binary snapshot file is missing or escaped its root: '$relative'." }
        $length = [int64](Get-Item -LiteralPath $full -Force).Length
        $hash = Get-FileSha256 $full
        if ($length -ne [int64]$row.byte_length -or $hash -cne [string]$row.sha256) { throw "The binary snapshot file changed: '$relative'." }
        $liveRows.Add([ordered]@{ path = $relative; byte_length = $length; sha256 = $hash })
    }
    $treeHash = Get-BinarySnapshotTreeSha256 @($liveRows)
    if ($treeHash -cne [string]$Snapshot.tree_sha256) { throw 'The run-owned binary snapshot tree hash differs.' }
    return $treeHash
}

function New-BinarySnapshot {
    param([string]$SourceDirectory, [string]$DestinationDirectory)
    $sourceBase = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\', '/')
    $destinationBase = [IO.Path]::GetFullPath($DestinationDirectory).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $sourceBase -PathType Container)) { throw "Build output directory is missing: $sourceBase" }
    if (((Get-Item -LiteralPath $sourceBase -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Build output directory is a reparse point: $sourceBase" }
    if (Test-Path -LiteralPath $destinationBase) { throw "Run-owned binary snapshot is not fresh: $destinationBase" }
    [IO.Directory]::CreateDirectory($destinationBase) | Out-Null
    if (((Get-Item -LiteralPath $destinationBase -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Run-owned binary snapshot directory is a reparse point: $destinationBase" }
    $rows = [Collections.Generic.List[object]]::new()
    $sourceEntries = @(Get-ChildItem -LiteralPath $sourceBase -Recurse -Force -ErrorAction Stop)
    foreach ($sourceEntry in $sourceEntries) {
        if (($sourceEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Build output contains a reparse point: $($sourceEntry.FullName)" }
    }
    $sourceFiles = @($sourceEntries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName)
    if ($sourceFiles.Count -eq 0) { throw 'The build output directory contains no files to seal.' }
    foreach ($sourceFile in $sourceFiles) {
        $relative = $sourceFile.FullName.Substring($sourceBase.Length).TrimStart('\', '/').Replace('\', '/')
        $destination = Join-Path $destinationBase $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        $sourceHashBefore = Get-FileSha256 $sourceFile.FullName
        [IO.File]::Copy($sourceFile.FullName, $destination, $false)
        $sourceHashAfter = Get-FileSha256 $sourceFile.FullName
        $destinationHash = Get-FileSha256 $destination
        if ($sourceHashAfter -cne $sourceHashBefore -or $destinationHash -cne $sourceHashBefore) {
            throw "Run-owned binary copy verification failed for '$relative'."
        }
        $rows.Add([ordered]@{
            path = $relative
            byte_length = [int64](Get-Item -LiteralPath $destination).Length
            sha256 = $destinationHash
        })
    }
    return [ordered]@{
        schema = 'pixel-tart-p3-run-owned-binary-snapshot/v1'
        source_directory = $sourceBase
        directory = $destinationBase
        file_count = $rows.Count
        copy_verified_before_execution = $true
        tree_sha256 = Get-BinarySnapshotTreeSha256 @($rows)
        files = @($rows)
    }
}

function Get-RunTreeFingerprint {
    param([string]$Root)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $lines = @(Get-ChildItem -LiteralPath $base -Recurse -File -Force -ErrorAction Stop |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($base.Length).TrimStart('\', '/').Replace('\', '/')
            $hash = Get-FileSha256 $_.FullName
            "$relative|$($_.Length)|$hash"
        })
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

function Get-RunTreeStateFingerprint {
    param([string]$Root)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $lines = @(Get-ChildItem -LiteralPath $base -Recurse -Force -ErrorAction Stop |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($base.Length).TrimStart('\', '/').Replace('\', '/')
            if ($_.PSIsContainer) {
                "D|$relative|$([int]$_.Attributes)"
            } else {
                "F|$relative|$($_.Length)|$(Get-FileSha256 $_.FullName)|$([int]$_.Attributes)"
            }
        })
    return Get-TextSha256 ($lines -join "`n")
}

function Get-CanonicalFileTreeSha256 {
    param([object[]]$Rows)
    $lineList = [Collections.Generic.List[string]]::new()
    foreach ($row in $Rows) {
        $lineList.Add(("{0}|{1}|{2}" -f ([string]$row.path), ([int64]$row.byte_length), ([string]$row.sha256)))
    }
    $lines = $lineList.ToArray()
    [Array]::Sort($lines, [StringComparer]::Ordinal)
    return Get-TextSha256 ($lines -join "`n")
}

function Get-RunRelativePath {
    param([string]$RunRoot, [string]$Path)
    $base = [IO.Path]::GetFullPath($RunRoot).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $base + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes the run root: $full"
    }
    return $full.Substring($prefix.Length).Replace('\', '/')
}

function New-AcceptanceInputSnapshot {
    param([string]$RunRoot)
    $sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\', '/')
    $directory = [IO.Path]::GetFullPath((Join-Path $RunRoot 'runner\acceptance-inputs')).TrimEnd('\', '/')
    if (-not (Test-PathWithin $directory $RunRoot) -or (Test-Path -LiteralPath $directory)) {
        throw "Acceptance input snapshot is not fresh or escaped its run root: $directory"
    }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $rows = [Collections.Generic.List[object]]::new()
    try {
        foreach ($fileName in $script:requiredAcceptanceInputFiles) {
            $source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $fileName))
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Acceptance input is missing: $source" }
            $sourceItem = Get-Item -LiteralPath $source -Force
            if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Acceptance input is a reparse point: $source"
            }
            $destination = [IO.Path]::GetFullPath((Join-Path $directory $fileName))
            $sourceHashBefore = Get-FileSha256 $source
            [IO.File]::Copy($source, $destination, $false)
            $sourceHashAfter = Get-FileSha256 $source
            $destinationHash = Get-FileSha256 $destination
            if ($sourceHashBefore -cne $sourceHashAfter -or $sourceHashBefore -cne $destinationHash) {
                throw "Acceptance input changed while it was snapshotted: $fileName"
            }
            $destinationItem = Get-Item -LiteralPath $destination -Force
            $destinationItem.Attributes = $destinationItem.Attributes -bor [IO.FileAttributes]::ReadOnly
            $rows.Add([ordered]@{
                path = $fileName
                byte_length = [int64]$destinationItem.Length
                sha256 = $destinationHash
            })
        }
        $sortedRows = @($rows | Sort-Object { [string]$_.path })
        return [ordered]@{
            schema = 'pixel-tart-p3-acceptance-input-snapshot/v1'
            source_directory = $sourceRoot
            directory = $directory
            file_count = $sortedRows.Count
            copy_verified_before_execution = $true
            files_read_only_before_execution = $true
            tree_sha256 = Get-CanonicalFileTreeSha256 $sortedRows
            files = $sortedRows
        }
    } catch {
        if (Test-Path -LiteralPath $directory -PathType Container) {
            foreach ($item in @(Get-ChildItem -LiteralPath $directory -Recurse -Force -File -ErrorAction SilentlyContinue)) {
                try { $item.Attributes = $item.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly) } catch { }
            }
        }
        throw
    }
}

function Assert-AcceptanceInputSnapshot {
    param($Snapshot, [string]$RunRoot)
    if ($null -eq $Snapshot -or [string]$Snapshot.schema -cne 'pixel-tart-p3-acceptance-input-snapshot/v1') {
        throw 'Acceptance input snapshot metadata is missing or invalid.'
    }
    $expectedDirectory = [IO.Path]::GetFullPath((Join-Path $RunRoot 'runner\acceptance-inputs')).TrimEnd('\', '/')
    $directory = [IO.Path]::GetFullPath([string]$Snapshot.directory).TrimEnd('\', '/')
    if (-not [string]::Equals($directory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw 'Acceptance input snapshot directory differs.'
    }
    $rows = @($Snapshot.files)
    if ([int]$Snapshot.file_count -ne $rows.Count -or $rows.Count -ne $script:requiredAcceptanceInputFiles.Count) {
        throw 'Acceptance input snapshot file count differs.'
    }
    $actualNames = @($rows | ForEach-Object { [string]$_.path } | Sort-Object)
    if (($actualNames -join '|') -cne (@($script:requiredAcceptanceInputFiles | Sort-Object) -join '|')) {
        throw 'Acceptance input snapshot file names differ.'
    }
    $liveRows = [Collections.Generic.List[object]]::new()
    foreach ($row in $rows) {
        $name = [string]$row.path
        if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::IsPathRooted($name) -or $name.Contains('/') -or $name.Contains('\')) {
            throw "Acceptance input snapshot path is not a canonical file name: '$name'."
        }
        $path = [IO.Path]::GetFullPath((Join-Path $directory $name))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Acceptance input snapshot file is missing: $name" }
        $item = Get-Item -LiteralPath $path -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            ($item.Attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) {
            throw "Acceptance input snapshot file is not sealed read-only: $name"
        }
        $hash = Get-FileSha256 $path
        if ([int64]$item.Length -ne [int64]$row.byte_length -or $hash -cne [string]$row.sha256) {
            throw "Acceptance input snapshot file changed: $name"
        }
        $liveRows.Add([ordered]@{ path = $name; byte_length = [int64]$item.Length; sha256 = $hash })
    }
    $treeHash = Get-CanonicalFileTreeSha256 @($liveRows)
    if ($treeHash -cne [string]$Snapshot.tree_sha256) { throw 'Acceptance input snapshot tree hash differs.' }
    return $treeHash
}

function Get-AcceptanceInputPath {
    param($Snapshot, [string]$RunRoot, [string]$FileName)
    [void](Assert-AcceptanceInputSnapshot $Snapshot $RunRoot)
    if ($FileName -cnotin $script:requiredAcceptanceInputFiles) { throw "Unknown acceptance input: $FileName" }
    return [IO.Path]::GetFullPath((Join-Path ([string]$Snapshot.directory) $FileName))
}

function Get-SealedAcceptanceInputPath {
    param([string]$RunRoot, [string]$FileName)
    $manifestPath = Join-Path $RunRoot 'run-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Run manifest is missing: $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    return Get-AcceptanceInputPath $manifest.acceptance_inputs $RunRoot $FileName
}

function New-RunSeal {
    param([string]$RunRoot, [string]$RunId, [string]$SourceHead)
    $root = [IO.Path]::GetFullPath($RunRoot).TrimEnd('\', '/')
    $sealRelativePath = 'runner/run-seal.json'
    $sealPath = [IO.Path]::GetFullPath((Join-Path $root $sealRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not (Test-PathWithin $sealPath $root) -or (Test-Path -LiteralPath $sealPath)) {
        throw "Run seal path is not fresh or escaped its root: $sealPath"
    }
    $entries = @(Get-ChildItem -LiteralPath $root -Recurse -Force -ErrorAction Stop)
    foreach ($entry in $entries) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Run seal refuses a reparse point: $($entry.FullName)"
        }
    }
    $rows = @($entries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = Get-RunRelativePath $root $_.FullName
            byte_length = [int64]$_.Length
            sha256 = Get-FileSha256 $_.FullName
        }
    })
    $payload = [ordered]@{
        schema = 'pixel-tart-p3-run-seal/v1'
        run_root = $root
        run_id = $RunId
        source_head = $SourceHead
        sealed_at = [DateTimeOffset]::UtcNow.ToString('O')
        seal_file = $sealRelativePath
        inventory_excludes_seal_file = $true
        read_only_required = $true
        file_count = $rows.Count
        tree_sha256 = Get-CanonicalFileTreeSha256 $rows
        files = $rows
    }
    Write-JsonAtomic $sealPath $payload
    try {
        $allFiles = @(Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction Stop)
        foreach ($file in $allFiles) { $file.Attributes = $file.Attributes -bor [IO.FileAttributes]::ReadOnly }
        foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction Stop)) {
            if (($file.Attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) {
                throw "Run seal could not make a file read-only: $($file.FullName)"
            }
        }
        return $payload
    } catch {
        foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue)) {
            try { $file.Attributes = $file.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly) } catch { }
        }
        if (Test-Path -LiteralPath $sealPath -PathType Leaf) { [IO.File]::Delete($sealPath) }
        throw
    }
}

function Get-SafetyScanRules {
    # These patterns target executable call sites, not comments or field names.
    # The validator owns the same fixed allowlist and independently rescans the
    # sealed source snapshots, so a producer cannot merely report zeroes.
    return [ordered]@{
        desktop_input_injection = '(?i)\b(?:SendInput|mouse_event|keybd_event)\s*\('
        uia_invoke = '(?i)\b(?:InvokePattern|IInvokeProvider)\b'
        forced_foreground = '(?i)\bSetForegroundWindow\s*\('
        real_display_setting_write = '(?i)\b(?:ChangeDisplaySettings|ChangeDisplaySettingsEx|SetProcessDpiAwarenessContext)\s*\('
        eagle_io = '(?i)(?:\bEagle\.exe\b|\.(?:eaglepack|library)(?:\\|/))'
        network_upload = '(?i)\b(?:HttpClient|HttpWebRequest|WebClient|TcpClient|UdpClient|Socket|UploadFile|UploadData)\b'
        direct_width_mutation = '(?i)\b(?:OrganizationPaneWidth|InspectorPaneWidth)\s*=(?!=)'
        direct_settings_mutation = '(?i)\b(?:BindingFlags|GetField|SetValue)\b'
        direct_sqlite_row_edit = '(?i)\b(?:ExecuteNonQuery(?:Async)?\s*\(|INSERT\s+INTO|UPDATE\s+[A-Za-z_][A-Za-z0-9_]*\s+SET|DELETE\s+FROM)'
    }
}

function Measure-SafetyStaticScan {
    param([string]$SnapshotRoot, [object[]]$Targets)
    $rules = Get-SafetyScanRules
    $ruleRows = [Collections.Generic.List[object]]::new()
    foreach ($ruleId in $rules.Keys) {
        $matches = [Collections.Generic.List[object]]::new()
        foreach ($target in $Targets) {
            $path = [IO.Path]::GetFullPath([string]$target.path)
            $lineNumber = 0
            foreach ($line in [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)) {
                $lineNumber++
                $count = [regex]::Matches($line, [string]$rules[$ruleId], [Text.RegularExpressions.RegexOptions]::CultureInvariant).Count
                if ($count -gt 0) {
                    $matches.Add([ordered]@{ path = [string]$target.relative_path; line = $lineNumber; count = $count })
                }
            }
        }
        $matchCount = 0
        foreach ($match in $matches) { $matchCount += [int]$match.count }
        $ruleRows.Add([ordered]@{
            rule_id = $ruleId
            match_count = $matchCount
            matches = @($matches)
        })
    }
    return [ordered]@{
        schema = 'pixel-tart-p3-safety-static-scan/v1'
        snapshot_root = [IO.Path]::GetFullPath($SnapshotRoot)
        snapshot_tree_sha256 = Get-RunTreeFingerprint $SnapshotRoot
        targets = @($Targets)
        rules = @($ruleRows)
    }
}

function New-SafetyStaticScanInput {
    param([string]$RunRoot)
    $snapshotRoot = Join-Path $RunRoot 'runner\safety-source-snapshot'
    if (Test-Path -LiteralPath $snapshotRoot) { throw "Safety source snapshot is not fresh: $snapshotRoot" }
    [IO.Directory]::CreateDirectory($snapshotRoot) | Out-Null
    $repositoryPaths = @(
        'src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs',
        'src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs',
        'src/PixelTart.Modules.AssetLibrary/AssetLibraryP3AutomatedAcceptanceDriver.cs'
    )
    $targets = [Collections.Generic.List[object]]::new()
    foreach ($repositoryPath in $repositoryPaths) {
        $source = [IO.Path]::GetFullPath((Join-Path $script:repo $repositoryPath.Replace('/', [IO.Path]::DirectorySeparatorChar)))
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Safety scan source is missing: $repositoryPath" }
        $relative = $repositoryPath.Replace('/', '__')
        $destination = Join-Path $snapshotRoot $relative
        $sourceHashBefore = Get-FileSha256 $source
        [IO.File]::Copy($source, $destination, $false)
        $sourceHashAfter = Get-FileSha256 $source
        $destinationHash = Get-FileSha256 $destination
        if ($sourceHashBefore -cne $sourceHashAfter -or $sourceHashBefore -cne $destinationHash) {
            throw "Safety scan source changed while it was snapshotted: $repositoryPath"
        }
        $targets.Add([ordered]@{
            repository_path = $repositoryPath
            relative_path = "runner/safety-source-snapshot/$relative"
            path = [IO.Path]::GetFullPath($destination)
            byte_length = [int64](Get-Item -LiteralPath $destination -Force).Length
            sha256 = $destinationHash
        })
    }
    return Measure-SafetyStaticScan $snapshotRoot @($targets)
}

function Get-SafetyRuleCount {
    param($Scan, [string]$RuleId)
    $row = @($Scan.rules | Where-Object { [string]$_.rule_id -ceq $RuleId }) | Select-Object -First 1
    if ($null -eq $row) { throw "Safety static scan omitted rule '$RuleId'." }
    return [int]$row.match_count
}

function Quote-ProcessArgument {
    param([AllowEmptyString()][string]$Value)
    if ($Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    # Windows CommandLineToArgvW quoting: double backslashes only when they
    # immediately precede a quote or the closing quote.
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * ($backslashes * 2 + 1)))
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) { [void]$builder.Append(('\' * $backslashes)); $backslashes = 0 }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) { [void]$builder.Append(('\' * ($backslashes * 2))) }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Get-GitBlobAudit {
    param([string]$Head, [string]$RepositoryPath)
    if ($RepositoryPath -notmatch '^[A-Za-z0-9._/-]+$') { throw "Unsafe repository path for source audit: $RepositoryPath" }
    $objectId = ((Invoke-Git @('rev-parse', "$Head`:$RepositoryPath")) -join '').Trim()
    if ($objectId -cnotmatch '^[0-9a-f]{40,64}$') { throw "Invalid Git object id for $RepositoryPath." }
    $gitPath = (Get-Command git.exe -ErrorAction Stop).Source
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $gitPath
    $startInfo.Arguments = "-C $(Quote-ProcessArgument $script:repo) cat-file blob $objectId"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw "Could not read Git blob for $RepositoryPath." }
    $memory = [IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "git cat-file failed for $RepositoryPath`: $errorText" }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = (($sha.ComputeHash($memory.ToArray()) | ForEach-Object { $_.ToString('x2') }) -join '') }
        finally { $sha.Dispose() }
        return [ordered]@{
            path = $RepositoryPath
            git_blob_oid = $objectId
            sha256 = $hash
            byte_length = $memory.Length
        }
    } finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

function New-RunRoot {
    if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) {
        if (-not (Test-IsAbsolutePath $OutputRoot)) { throw 'OutputRoot must be absolute.' }
        $base = [IO.Path]::GetFullPath($OutputRoot)
    } else {
        $base = Join-Path $script:repo '.validation'
    }
    [IO.Directory]::CreateDirectory($base) | Out-Null
    $stamp = [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')
    $path = Join-Path $base "P3-Automated-Acceptance-$stamp-$([guid]::NewGuid().ToString('N').Substring(0,12))"
    if (Test-Path -LiteralPath $path) { throw "Run root already exists: $path" }
    [IO.Directory]::CreateDirectory($path) | Out-Null
    return [IO.Path]::GetFullPath($path)
}

function Invoke-LoggedProcess {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$Name,
        [string]$LogDirectory,
        [int]$Timeout = 1800
    )
    [IO.Directory]::CreateDirectory($LogDirectory) | Out-Null
    $stdout = Join-Path $LogDirectory "$Name.stdout.log"
    $stderr = Join-Path $LogDirectory "$Name.stderr.log"
    $started = [DateTimeOffset]::UtcNow
    $argumentLine = ($Arguments | ForEach-Object { Quote-ProcessArgument ([string]$_) }) -join ' '
    $process = Start-Process -FilePath $FilePath -ArgumentList $argumentLine -WorkingDirectory $script:repo `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $process.WaitForExit($Timeout * 1000)) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
        throw "$Name timed out after $Timeout seconds."
    }
    $process.WaitForExit()
    $result = [ordered]@{
        name = $Name
        file = $FilePath
        arguments = $Arguments
        started_at = $started.ToString('O')
        finished_at = [DateTimeOffset]::UtcNow.ToString('O')
        duration_ms = [int64]([DateTimeOffset]::UtcNow - $started).TotalMilliseconds
        exit_code = [int]$process.ExitCode
        stdout = $stdout
        stderr = $stderr
        stdout_sha256 = Get-FileSha256 $stdout
        stderr_sha256 = Get-FileSha256 $stderr
    }
    Write-JsonAtomic (Join-Path $LogDirectory "$Name.result.json") $result
    if ($result.exit_code -ne 0) { throw "$Name failed with exit $($result.exit_code). See $stdout and $stderr." }
    return $result
}

function Invoke-AppPhase {
    param(
        [string]$Phase,
        [string[]]$ScenarioIds,
        [string]$SessionName,
        [string]$Head,
        [string]$Executable,
        [string]$ActiveRunRoot,
        [string]$RunId,
        [string]$RunStartedAtUtc,
        [string]$LogDirectory,
        [AllowNull()]$BinarySnapshot
    )
    Assert-NoDevPreview
    if ($ScenarioIds.Count -ne 1) { throw 'Each automated app process must own exactly one scenario.' }
    $scenarioDirectory = ($ScenarioIds[0] -replace '[^a-zA-Z0-9.-]', '-')
    # Each of the three restart phases deliberately reopens its scenario's isolated
    # application root. Every primary scenario gets a fresh root and process.
    $scenarioRoot = Join-Path $ActiveRunRoot "runtime\$scenarioDirectory"
    $runtimeRoot = Join-Path $scenarioRoot 'app-data'
    if ($Phase -eq 'primary') {
        if (Test-Path -LiteralPath $runtimeRoot) { throw "Runtime root is not fresh: $runtimeRoot" }
        [IO.Directory]::CreateDirectory($runtimeRoot) | Out-Null
    } elseif (-not (Test-Path -LiteralPath $runtimeRoot -PathType Container)) {
        throw "Restart phase is missing the primary isolated runtime root: $runtimeRoot"
    }
    $planPath = Join-Path $ActiveRunRoot "plans\$SessionName.json"
    $moduleDll = Join-Path (Split-Path -Parent $Executable) 'PixelTart.Modules.AssetLibrary.dll'
    $applicationDll = Join-Path (Split-Path -Parent $Executable) 'PixelTart_ModularHarness_V1_DevPreview.dll'
    foreach ($sealedPath in @($applicationDll, $moduleDll)) {
        if (-not (Test-Path -LiteralPath $sealedPath -PathType Leaf)) { throw "A sealed application identity file is missing: $sealedPath" }
    }
    $expectedExecutablePath = [IO.Path]::GetFullPath($Executable)
    $expectedApplicationPath = [IO.Path]::GetFullPath($applicationDll)
    $expectedModulePath = [IO.Path]::GetFullPath($moduleDll)
    $expectedExecutableHash = Get-FileSha256 $expectedExecutablePath
    $expectedApplicationHash = Get-FileSha256 $expectedApplicationPath
    $expectedModuleHash = Get-FileSha256 $expectedModulePath
    $snapshotTreeBefore = Assert-BinarySnapshotState $BinarySnapshot
    $fixtureRoot = Join-Path $ActiveRunRoot 'synthetic-fixture'
    $fixtureVariant = if ($ScenarioIds[0] -ceq 'smart-folder-invalid-migration/v1') { 'legacy-v6' } else { 'current-v7' }
    $fixtureDatabaseName = if ($fixtureVariant -ceq 'legacy-v6') { 'asset-library-v16-legacy-v6.db' } else { 'asset-library-v16.db' }
    $fixtureDatabasePath = [IO.Path]::GetFullPath((Join-Path $fixtureRoot $fixtureDatabaseName))
    if (-not (Test-PathWithin $fixtureDatabasePath $fixtureRoot) -or -not (Test-Path -LiteralPath $fixtureDatabasePath -PathType Leaf)) {
        throw "The selected $fixtureVariant fixture database is missing or escaped its root: $fixtureDatabasePath"
    }
    $expectedProcessSessionId = [guid]::NewGuid().ToString('N')
    Write-JsonAtomic $planPath ([ordered]@{
        schema_version = 'pixel-tart-p3-automated-plan/v1'
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        run_id = $RunId
        process_session_id = $expectedProcessSessionId
        phase = $Phase
        source_head = $Head
        executable_path = $expectedExecutablePath
        executable_sha256 = $expectedExecutableHash
        application_path = $expectedApplicationPath
        application_sha256 = $expectedApplicationHash
        asset_module_path = $expectedModulePath
        asset_module_sha256 = $expectedModuleHash
        binary_snapshot_directory = [IO.Path]::GetFullPath((Split-Path -Parent $expectedExecutablePath))
        binary_snapshot_tree_sha256 = $snapshotTreeBefore
        scenario_ids = $ScenarioIds
        scenario_root = $scenarioRoot
        fixture_root = $fixtureRoot
        fixture_variant = $fixtureVariant
        fixture_database_path = $fixtureDatabasePath
    })
    $stdout = Join-Path $LogDirectory "app-$SessionName.stdout.log"
    $stderr = Join-Path $LogDirectory "app-$SessionName.stderr.log"
    $environment = @{
        PIXEL_TART_ACCEPTANCE_ROOT = $runtimeRoot
        PIXEL_TART_ASSET_LIBRARY_DEMO_DIR = $null
        PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE = $null
        PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE = $null
        PIXEL_TART_ASSET_LIBRARY_P1_HEAD = $null
        PIXEL_TART_P3_AUTOMATED_HEAD = $null
        PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS = $null
        PIXEL_TART_P3_AUTOMATED_ACCEPTANCE = '1'
        PIXEL_TART_P3_AUTOMATED_RUN_ROOT = $ActiveRunRoot
        PIXEL_TART_P3_AUTOMATED_PLAN_PATH = $planPath
        PIXEL_TART_P3_AUTOMATED_SOURCE_HEAD = $Head
        PIXEL_TART_P3_AUTOMATED_FIXTURE_ROOT = $fixtureRoot
    }
    $started = [DateTimeOffset]::UtcNow
    $process = Invoke-WithEnvironment $environment {
        Start-Process -FilePath $Executable -WorkingDirectory (Split-Path -Parent $Executable) `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    }
    $ownerToken = New-RunnerProcessOwnerToken $process $expectedExecutablePath $expectedExecutableHash $RunId `
        $expectedProcessSessionId (Join-Path $ActiveRunRoot 'binaries') $RunStartedAtUtc
    $processExitDiagnosticPath = Join-Path $LogDirectory "app-$SessionName.process-exit.json"
    $phaseSummaryPath = Join-Path $ActiveRunRoot ("app\evidence\summary-{0}-{1}.json" -f $scenarioDirectory, $Phase)
    $lifecyclePath = Join-Path $ActiveRunRoot ("app\evidence\lifecycle-{0}-{1}.ndjson" -f $scenarioDirectory, $Phase)
    $summaryJournalPath = Join-Path $ActiveRunRoot 'app\evidence\summary.ndjson'
    $expectedLifecycleIdentity = [pscustomobject][ordered]@{
        schema = 'pixel-tart-p3-automated-lifecycle/v1'
        run_id = $RunId
        scenario_id = $ScenarioIds[0]
        phase = $Phase
        process_session_id = $expectedProcessSessionId
        source_head = $Head
        executable_sha256 = $expectedExecutableHash
        application_sha256 = $expectedApplicationHash
        asset_module_sha256 = $expectedModuleHash
        pid = [int]$process.Id
    }
    $processExitDiagnostic = $null
    try {
        [void](Wait-RunnerOwnedProcessExit $process $ownerToken $TimeoutSeconds $Phase $SessionName $ScenarioIds[0] `
            $lifecyclePath $phaseSummaryPath $summaryJournalPath $expectedLifecycleIdentity ([ref]$processExitDiagnostic))
    } catch {
        $phaseFailure = $_
        if ($null -ne $processExitDiagnostic) {
            try { Write-JsonAtomic $processExitDiagnosticPath $processExitDiagnostic }
            catch { $phaseFailure.Exception.Data['process_exit_diagnostic_write_failure'] = $_.Exception.ToString() }
        }
        throw
    }
    try {
        if (-not (Test-Path -LiteralPath $phaseSummaryPath -PathType Leaf)) {
            throw "Automated app phase '$Phase' did not write its immutable phase summary: $phaseSummaryPath"
        }
        $phaseSummary = Get-Content -LiteralPath $phaseSummaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not (Test-Path -LiteralPath $lifecyclePath -PathType Leaf)) {
            throw "Automated app phase '$Phase' did not write its lifecycle handshake journal: $lifecyclePath"
        }
        $lifecycleSha256 = Get-FileSha256 $lifecyclePath
        $phaseScenario = @($phaseSummary.scenarios | Where-Object { $_.id -ceq $ScenarioIds[0] })
        if ($phaseScenario.Count -ne 1) { throw "Automated app phase '$Phase' has no unique scenario summary." }
        $processSessionId = [string]$phaseSummary.process_session_id
        if ($processSessionId -cnotmatch '^[0-9a-f]{32}$' -or $processSessionId -cne $expectedProcessSessionId) {
            throw "Automated app phase '$Phase' did not return its runner-preassigned process_session_id."
        }
        if ([string]$phaseSummary.status -cne 'completed') {
            throw "Automated app phase '$Phase' failed in the application: $([string]$phaseSummary.failure)"
        }
        $phasePid = if ($Phase -ceq 'primary') { [int]$phaseScenario[0].pid } else { [int]$phaseScenario[0].restart_pid }
        $phaseHwnd = if ($Phase -ceq 'primary') { [string]$phaseScenario[0].hwnd } else { [string]$phaseScenario[0].restart_hwnd }
        if ($phasePid -ne $process.Id -or $phaseHwnd -cnotmatch '^0x[0-9a-fA-F]+$' -or
            [string]$phaseSummary.run_id -cne $RunId -or [string]$phaseSummary.source_head -cne $Head -or
            [string]$phaseSummary.phase -cne $Phase -or [string]$phaseSummary.status -cne 'completed' -or
            -not [string]::Equals([IO.Path]::GetFullPath([string]$phaseSummary.executable_path), $expectedExecutablePath, [StringComparison]::OrdinalIgnoreCase) -or
            [string]$phaseSummary.executable_sha256 -cne $expectedExecutableHash -or
            -not [string]::Equals([IO.Path]::GetFullPath([string]$phaseSummary.application_path), $expectedApplicationPath, [StringComparison]::OrdinalIgnoreCase) -or
            [string]$phaseSummary.application_sha256 -cne $expectedApplicationHash -or
            -not [string]::Equals([IO.Path]::GetFullPath([string]$phaseSummary.asset_module_path), $expectedModulePath, [StringComparison]::OrdinalIgnoreCase) -or
            [string]$phaseSummary.asset_module_sha256 -cne $expectedModuleHash) {
            throw "Automated app phase '$Phase' summary identity does not match the runner-owned process."
        }
        $ownerToken.ProcessSessionId = $processSessionId
        $ownerToken.WindowHandle = $phaseHwnd
        $ownerToken.HasExited = $true
    } catch {
        $summaryFailure = $_
        $processExitDiagnostic.primary_failure = ConvertTo-StructuredFailure 'primary' 'completion-summary-handshake' $summaryFailure
        $processExitDiagnostic.outcome = 'failed'
        try { Write-JsonAtomic $processExitDiagnosticPath $processExitDiagnostic }
        catch { $summaryFailure.Exception.Data['process_exit_diagnostic_write_failure'] = $_.Exception.ToString() }
        throw
    }
    $snapshotTreeAfter = Assert-BinarySnapshotState $BinarySnapshot
    if ($snapshotTreeAfter -cne $snapshotTreeBefore -or
        (Get-FileSha256 $expectedExecutablePath) -cne $expectedExecutableHash -or
        (Get-FileSha256 $expectedApplicationPath) -cne $expectedApplicationHash -or
        (Get-FileSha256 $expectedModulePath) -cne $expectedModuleHash) {
        throw "Automated app phase '$Phase' changed its sealed executable, application assembly, or module."
    }
    if ([int]$process.ExitCode -ne 0) {
        $nonzeroExit = [InvalidOperationException]::new("Automated app phase '$Phase' failed with exit $([int]$process.ExitCode).")
        $processExitDiagnostic.primary_failure = ConvertTo-StructuredFailure 'primary' 'process-exit-code' $nonzeroExit
        $processExitDiagnostic.outcome = 'failed'
        try { Write-JsonAtomic $processExitDiagnosticPath $processExitDiagnostic }
        catch { $nonzeroExit.Data['process_exit_diagnostic_write_failure'] = $_.Exception.ToString() }
        throw $nonzeroExit
    }
    $processExitDiagnostic.outcome = 'completed'
    Write-JsonAtomic $processExitDiagnosticPath $processExitDiagnostic
    $processExitDiagnosticSha256 = Get-FileSha256 $processExitDiagnosticPath
    $resultPath = Join-Path $LogDirectory "app-$SessionName.result.json"
    $result = [ordered]@{
        schema = 'pixel-tart-p3-runner-session-result/v1'
        status = 'completed'
        phase = $Phase
        session_name = $SessionName
        scenario_id = $ScenarioIds[0]
        pid = $process.Id
        hwnd = $phaseHwnd
        process_session_id = $processSessionId
        exit_code = [int]$process.ExitCode
        run_id = $RunId
        source_head = $Head
        started_at = $started.ToString('O')
        finished_at = [DateTimeOffset]::UtcNow.ToString('O')
        duration_ms = [int64]([DateTimeOffset]::UtcNow - $started).TotalMilliseconds
        runtime_root = $runtimeRoot
        scenario_root = $scenarioRoot
        plan_path = $planPath
        phase_summary_path = $phaseSummaryPath
        phase_summary_sha256 = Get-FileSha256 $phaseSummaryPath
        phase_summary_record_sha256 = [string]$phaseSummary.record_sha256
        lifecycle_path = $lifecyclePath
        lifecycle_sha256 = $lifecycleSha256
        executable_path = $expectedExecutablePath
        executable_sha256 = $expectedExecutableHash
        application_path = $expectedApplicationPath
        application_sha256 = $expectedApplicationHash
        asset_module_path = $expectedModulePath
        asset_module_sha256 = $expectedModuleHash
        binary_snapshot_tree_sha256_before = $snapshotTreeBefore
        binary_snapshot_tree_sha256_after = $snapshotTreeAfter
        stdout = $stdout
        stderr = $stderr
        stdout_sha256 = Get-FileSha256 $stdout
        stderr_sha256 = Get-FileSha256 $stderr
        process_exit_diagnostic_path = $processExitDiagnosticPath
        process_exit_diagnostic_sha256 = $processExitDiagnosticSha256
        process_exit_diagnostic = $processExitDiagnostic
        result_path = $resultPath
    }
    $result.record_sha256 = Get-TextSha256 ($result | ConvertTo-Json -Depth 30 -Compress)
    Write-JsonAtomic $resultPath $result
    $session = [ordered]@{}
    foreach ($entry in $result.GetEnumerator()) { $session[$entry.Key] = $entry.Value }
    $session.result_sha256 = Get-FileSha256 $resultPath
    return $session
}

function New-P3SyntheticFixture {
    param([string]$ActiveRunRoot, $AcceptanceInputs)
    if ([string]::IsNullOrWhiteSpace($ActiveRunRoot) -or -not (Test-IsAbsolutePath $ActiveRunRoot)) {
        throw 'The synthetic fixture requires an absolute ActiveRunRoot.'
    }
    $runRoot = [IO.Path]::GetFullPath($ActiveRunRoot).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $runRoot -PathType Container)) {
        throw "The synthetic fixture run root does not exist: $runRoot"
    }
    if ((Get-Item -LiteralPath $runRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "The synthetic fixture run root is a reparse point: $runRoot"
    }
    $directory = [IO.Path]::GetFullPath((Join-Path $runRoot 'synthetic-fixture')).TrimEnd('\', '/')
    if (-not (Test-PathWithin $directory $runRoot)) {
        throw "The synthetic fixture directory escaped the run root: $directory"
    }
    if (Test-Path -LiteralPath $directory) {
        throw "The synthetic fixture directory is not fresh; refusing to modify an existing run root: $directory"
    }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "The synthetic fixture directory is a reparse point: $directory"
    }
    $databasePath = [IO.Path]::GetFullPath((Join-Path $directory 'asset-library-v16.db'))
    $legacyDatabasePath = [IO.Path]::GetFullPath((Join-Path $directory 'asset-library-v16-legacy-v6.db'))
    $expectationsPath = [IO.Path]::GetFullPath((Join-Path $directory 'fixture-expectations.json'))
    $generatorPath = [IO.Path]::GetFullPath((Join-Path $directory 'fixture-generator.py'))
    if (-not (Test-PathWithin $databasePath $directory) -or [IO.Path]::GetFileName($databasePath) -cne 'asset-library-v16.db') {
        throw "The synthetic fixture database path is invalid: $databasePath"
    }
    if (-not (Test-PathWithin $generatorPath $directory) -or [IO.Path]::GetFileName($generatorPath) -cne 'fixture-generator.py') {
        throw "The synthetic fixture generator path is invalid: $generatorPath"
    }
    if (-not (Test-PathWithin $legacyDatabasePath $directory) -or
        [IO.Path]::GetFileName($legacyDatabasePath) -cne 'asset-library-v16-legacy-v6.db' -or
        -not (Test-PathWithin $expectationsPath $directory) -or
        [IO.Path]::GetFileName($expectationsPath) -cne 'fixture-expectations.json') {
        throw 'The synthetic fixture legacy database or expectations path is invalid.'
    }
    if ((Test-Path -LiteralPath $databasePath) -or
        (Test-Path -LiteralPath $legacyDatabasePath) -or
        (Test-Path -LiteralPath $expectationsPath) -or
        (Test-Path -LiteralPath $generatorPath)) {
        throw 'The synthetic fixture generator, database, and expectations paths must not already exist.'
    }
    $python = [IO.Path]::GetFullPath((Get-Command python.exe -ErrorAction Stop).Source)
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
        throw "The Python executable is not a file: $python"
    }
    $sourceGeneratorPath = Get-AcceptanceInputPath $AcceptanceInputs $runRoot 'New-P3SyntheticFixture.py'
    if (-not (Test-Path -LiteralPath $sourceGeneratorPath -PathType Leaf)) {
        throw "The sealed P3 synthetic fixture generator was not found: $sourceGeneratorPath"
    }
    $pythonCode = [IO.File]::ReadAllText(
        [IO.Path]::GetFullPath($sourceGeneratorPath),
        [Text.Encoding]::UTF8)
    if ([string]::IsNullOrEmpty($pythonCode)) { throw 'The synthetic fixture generator script is empty.' }
    [IO.File]::WriteAllText($generatorPath, $pythonCode, [Text.UTF8Encoding]::new($false))
    if (-not (Test-Path -LiteralPath $generatorPath -PathType Leaf)) {
        throw "The synthetic fixture generator script was not written: $generatorPath"
    }
    $generatorItem = Get-Item -LiteralPath $generatorPath -Force
    if (($generatorItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "The synthetic fixture generator script is a reparse point: $generatorPath"
    }
    $generatorScriptHash = Get-FileSha256 $generatorPath
    if ($generatorScriptHash -cnotmatch '^[0-9a-f]{64}$') {
        throw "The synthetic fixture generator script hash is invalid: $generatorScriptHash"
    }
    $generatorArguments = @('-I', $generatorPath, $directory, $databasePath, $legacyDatabasePath)
    if ($generatorArguments.Count -ne 5) {
        throw "The fixture generator invocation must contain exactly 5 process arguments; actual count=$($generatorArguments.Count)."
    }
    if ($generatorArguments[0] -cne '-I' -or
        [IO.Path]::IsPathRooted([string]$generatorArguments[0]) -or
        [IO.Path]::GetFullPath([string]$generatorArguments[1]) -cne $generatorPath -or
        [IO.Path]::GetFullPath([string]$generatorArguments[2]) -cne $directory -or
        [IO.Path]::GetFullPath([string]$generatorArguments[3]) -cne $databasePath -or
        [IO.Path]::GetFullPath([string]$generatorArguments[4]) -cne $legacyDatabasePath -or
        -not (Test-PathWithin ([string]$generatorArguments[1]) $directory) -or
        -not (Test-PathWithin ([string]$generatorArguments[2]) $runRoot) -or
        -not (Test-PathWithin ([string]$generatorArguments[3]) $directory) -or
        -not (Test-PathWithin ([string]$generatorArguments[4]) $directory)) {
        throw 'The fixture generator invocation contains an invalid or escaped argument path.'
    }
    $generatorLogDirectory = Join-Path $runRoot 'runner'
    $generatorResult = Invoke-LoggedProcess -FilePath $python `
        -Arguments $generatorArguments `
        -Name 'fixture-generator' -LogDirectory $generatorLogDirectory -Timeout 300
    foreach ($generatedPath in @($databasePath, $legacyDatabasePath, $expectationsPath)) {
        if (-not (Test-Path -LiteralPath $generatedPath -PathType Leaf)) {
            throw "The fixture generator completed without creating an expected output: $generatedPath"
        }
    }
    $generatorOutputLines = @(Get-Content -LiteralPath $generatorResult.stdout -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($generatorOutputLines.Count -ne 1) {
        throw "The fixture generator must emit exactly one metadata JSON record; actual count=$($generatorOutputLines.Count)."
    }
    try { $generatedMetadata = $generatorOutputLines[0] | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "The fixture generator metadata record is invalid JSON: $($_.Exception.Message)" }
    if ([int]$generatedMetadata.schema_version -ne 7 -or
        [int]$generatedMetadata.total_count -ne 10128 -or
        [int]$generatedMetadata.active_count -ne 10000 -or
        [int]$generatedMetadata.archived_count -ne 128 -or
        [int]$generatedMetadata.display_name_count -ne 10128 -or
        [int]$generatedMetadata.content_hash_count -ne 10128 -or
        [int]$generatedMetadata.missing_count -ne 512 -or
        [string]$generatedMetadata.visual_feature_counts.analysis_version -cne 'visual-analysis-v2' -or
        [int]$generatedMetadata.visual_feature_counts.valid -ne 3072 -or
        [int]$generatedMetadata.visual_feature_counts.failed -ne 1024 -or
        [int]$generatedMetadata.visual_feature_counts.not_analyzed -ne 6032 -or
        [int]$generatedMetadata.visual_feature_counts.feature_rows -ne 4096 -or
        [int]$generatedMetadata.legacy_variant.schema_version -ne 6 -or
        [int]$generatedMetadata.legacy_variant.total_count -ne 64 -or
        [int]$generatedMetadata.legacy_variant.active_count -ne 60 -or
        [int]$generatedMetadata.legacy_variant.archived_count -ne 4 -or
        [string]$generatedMetadata.source_path_observation -cne 'sqlite-sourcepath-enumeration/v1' -or
        [int]$generatedMetadata.source_path_count -ne 10192 -or
        [int]$generatedMetadata.source_paths_inside_fixture_count -ne 10192 -or
        [int]$generatedMetadata.source_paths_outside_fixture_count -ne 0 -or
        [string]$generatedMetadata.current_source_path_sha256 -notmatch '^[0-9a-f]{64}$' -or
        [string]$generatedMetadata.legacy_source_path_sha256 -notmatch '^[0-9a-f]{64}$' -or
        [string]$generatedMetadata.source_path_tree_sha256 -notmatch '^[0-9a-f]{64}$' -or
        [IO.Path]::GetFullPath([string]$generatedMetadata.expectations_path) -cne $expectationsPath) {
        throw 'The fixture generator metadata does not satisfy the P3 fixture contract.'
    }
    $generatedFileRows = @(Get-ChildItem -LiteralPath $directory -Force -File -ErrorAction Stop |
        Sort-Object Name |
        ForEach-Object {
            [ordered]@{
                path = $_.Name
                byte_length = [int64]$_.Length
                sha256 = Get-FileSha256 $_.FullName
            }
        })
    $generatedFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($row in $generatedFileRows) { [void]$generatedFileNames.Add([string]$row.path) }
    $expectedFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in 'asset-library-v16.db', 'asset-library-v16-legacy-v6.db', 'fixture-expectations.json', 'fixture-generator.py') {
        [void]$expectedFileNames.Add($name)
    }
    if ($generatedFileRows.Count -ne 4 -or -not $generatedFileNames.SetEquals($expectedFileNames)) {
        throw 'The generated fixture input tree has an unexpected file inventory.'
    }
    $fixture = [ordered]@{
        schema = 'pixel-tart-p3-synthetic-fixture/v1'
        source_kind = 'synthetic-run-owned'
        directory = [IO.Path]::GetFullPath($directory)
        database_path = [IO.Path]::GetFullPath($databasePath)
        database_sha256 = Get-FileSha256 $databasePath
        legacy_database_path = [IO.Path]::GetFullPath($legacyDatabasePath)
        legacy_database_sha256 = Get-FileSha256 $legacyDatabasePath
        expectations_path = [IO.Path]::GetFullPath($expectationsPath)
        expectations_sha256 = Get-FileSha256 $expectationsPath
        generator_script_path = [IO.Path]::GetFullPath($generatorPath)
        generator_script_sha256 = $generatorScriptHash
        generator_script_byte_length = [int64]$generatorItem.Length
        generator_arguments = @($generatorArguments)
        generator_process_result = $generatorResult
        schema_version = [int]$generatedMetadata.schema_version
        total_count = [int]$generatedMetadata.total_count
        active_count = [int]$generatedMetadata.active_count
        archived_count = [int]$generatedMetadata.archived_count
        display_name_count = [int]$generatedMetadata.display_name_count
        display_name_language = 'zh-CN'
        content_hash_count = [int]$generatedMetadata.content_hash_count
        content_hash_algorithm = 'sha256'
        content_hash_deterministic = $true
        missing_count = [int]$generatedMetadata.missing_count
        visual_feature_counts = [ordered]@{
            analysis_version = [string]$generatedMetadata.visual_feature_counts.analysis_version
            valid = [int]$generatedMetadata.visual_feature_counts.valid
            failed = [int]$generatedMetadata.visual_feature_counts.failed
            not_analyzed = [int]$generatedMetadata.visual_feature_counts.not_analyzed
            feature_rows = [int]$generatedMetadata.visual_feature_counts.feature_rows
        }
        legacy_variant = [ordered]@{
            schema_version = [int]$generatedMetadata.legacy_variant.schema_version
            total_count = [int]$generatedMetadata.legacy_variant.total_count
            active_count = [int]$generatedMetadata.legacy_variant.active_count
            archived_count = [int]$generatedMetadata.legacy_variant.archived_count
        }
        source_path_observation = [string]$generatedMetadata.source_path_observation
        source_path_count = [int]$generatedMetadata.source_path_count
        source_paths_inside_fixture_count = [int]$generatedMetadata.source_paths_inside_fixture_count
        source_paths_outside_fixture_count = [int]$generatedMetadata.source_paths_outside_fixture_count
        current_source_path_sha256 = [string]$generatedMetadata.current_source_path_sha256
        legacy_source_path_sha256 = [string]$generatedMetadata.legacy_source_path_sha256
        source_path_tree_sha256 = [string]$generatedMetadata.source_path_tree_sha256
        user_source_read_count = [int]$generatedMetadata.source_paths_outside_fixture_count
        user_source_write_count = [int]$generatedMetadata.source_paths_outside_fixture_count
        generated_file_count = $generatedFileRows.Count
        generated_tree_sha256 = Get-CanonicalFileTreeSha256 $generatedFileRows
        generated_files = $generatedFileRows
    }
    $fixtureManifestPath = Join-Path $directory 'fixture-manifest.json'
    Write-JsonAtomic $fixtureManifestPath $fixture
    $fixture['fixture_manifest_path'] = [IO.Path]::GetFullPath($fixtureManifestPath)
    $fixture['fixture_manifest_sha256'] = Get-FileSha256 $fixtureManifestPath
    return $fixture
}

function Remove-RunOwnedRuntimeDatabases {
    param([string]$ActiveRunRoot)
    $runtimeRoot = [IO.Path]::GetFullPath((Join-Path $ActiveRunRoot 'runtime')).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $runtimeRoot -PathType Container)) {
        return [ordered]@{ removed_count = 0; removed_paths = @(); runtime_database_count_after = 0 }
    }
    $targets = @(Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -ErrorAction Stop | Where-Object {
        $_.Name -like '*.db' -or $_.Name -like '*.db-wal' -or $_.Name -like '*.db-shm' -or
        $_.Name -like '*-wal' -or $_.Name -like '*-shm'
    })
    $removed = [Collections.Generic.List[string]]::new()
    foreach ($target in $targets) {
        $full = [IO.Path]::GetFullPath($target.FullName)
        if (-not $full.StartsWith($runtimeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a database outside the generated runtime root: $full"
        }
        Remove-Item -LiteralPath $full -Force -ErrorAction Stop
        $removed.Add($full)
    }
    $remaining = @(Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -ErrorAction Stop | Where-Object {
        $_.Name -like '*.db' -or $_.Name -like '*.db-wal' -or $_.Name -like '*.db-shm' -or
        $_.Name -like '*-wal' -or $_.Name -like '*-shm'
    })
    return [ordered]@{
        removed_count = $removed.Count
        removed_paths = @($removed)
        runtime_database_count_after = $remaining.Count
    }
}

function Invoke-PreCleanupDatabaseAudit {
    param([string]$ActiveRunRoot, [string]$LogDirectory)
    $python = (Get-Command python.exe -ErrorAction Stop).Source
    $summaryPath = Join-Path $ActiveRunRoot 'app\evidence\summary.json'
    if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
        throw "Application summary is missing before the database consistency audit: $summaryPath"
    }
    $auditPath = Join-Path $ActiveRunRoot 'runner\database-consistency-audit.json'
    $pythonCode = @'
import hashlib, json, pathlib, sqlite3, sys
summary_path = pathlib.Path(sys.argv[1]).resolve()
run_root = pathlib.Path(sys.argv[2]).resolve()
output_path = pathlib.Path(sys.argv[3]).resolve()
summary = json.loads(summary_path.read_text(encoding="utf-8"))

def inside(path):
    path.relative_to(run_root)
    return path

def inspect(path, expected_counts):
    path = inside(path.resolve())
    if not path.is_file():
        raise RuntimeError(f"database is missing: {path}")
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    connection = sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)
    try:
        connection.execute("PRAGMA query_only=ON")
        quick_check = connection.execute("PRAGMA quick_check").fetchone()[0]
        schema_version = connection.execute("SELECT MAX(Version) FROM AssetLibrarySchemaInfo").fetchone()[0]
        asset_count = connection.execute("SELECT COUNT(*) FROM AssetItems").fetchone()[0]
        active_count = connection.execute("SELECT COUNT(*) FROM AssetItems WHERE IsArchived=0").fetchone()[0]
        archived_count = connection.execute("SELECT COUNT(*) FROM AssetItems WHERE IsArchived=1").fetchone()[0]
    finally:
        connection.close()
    after = hashlib.sha256(path.read_bytes()).hexdigest()
    if before != after:
        raise RuntimeError(f"read-only audit changed database: {path}")
    if quick_check != "ok" or schema_version != 7 or (asset_count, active_count, archived_count) != expected_counts:
        raise RuntimeError(f"invalid database {path}: quick_check={quick_check}, schema={schema_version}, counts={(asset_count,active_count,archived_count)}")
    return {"path": str(path), "sha256": before, "quick_check": quick_check,
            "schema_version": schema_version, "asset_count": asset_count,
            "active_count": active_count, "archived_count": archived_count}

rows = []
expected_ids = [
    "scope-switch/v1", "ime-cancellation/v1", "search-suggestions-history/v1",
    "folder-any-all-not/v1", "tag-any-all-not/v1", "scalar-null-composition/v1",
    "visual-composition/v1", "nested-canonical-query/v1", "invalid-query-fail-closed/v1",
    "smart-folder-lifecycle-preview/v1", "smart-folder-invalid-migration/v1",
    "tag-manager-lifecycle/v1", "bulk-metadata-journal/v1",
    "four-view-resilience-layout/v1",
]
if [scenario["id"] for scenario in summary["scenarios"]] != expected_ids:
    raise RuntimeError("pre-cleanup scenario order differs")
seen_active, seen_evidence = set(), set()
for scenario in summary["scenarios"]:
    database = scenario["database"]
    scenario_root = inside(pathlib.Path(scenario["scenario_root"]).resolve())
    expected_active = (scenario_root / "app-data" / "Data" / "asset-library-v16.db").resolve()
    declared_active = inside(pathlib.Path(database["active_database_absolute_path"]).resolve())
    if declared_active != expected_active:
        raise RuntimeError(f"active database path differs for {scenario['id']}: {declared_active}")
    if declared_active in seen_active:
        raise RuntimeError(f"active database path is reused: {declared_active}")
    seen_active.add(declared_active)
    expected_counts = (64, 60, 4) if scenario["id"] == "smart-folder-invalid-migration/v1" else (10128, 10000, 128)
    active = inspect(declared_active, expected_counts)
    evidence_refs = database["evidence_paths"]
    if not evidence_refs:
        raise RuntimeError(f"scenario has no evidence database: {scenario['id']}")
    if evidence_refs[-1] != database["path"]:
        raise RuntimeError(f"final evidence reference differs for {scenario['id']}")
    evidence_path = inside((run_root / evidence_refs[-1]).resolve())
    if evidence_path in seen_evidence:
        raise RuntimeError(f"evidence database path is reused: {evidence_path}")
    seen_evidence.add(evidence_path)
    evidence = inspect(evidence_path, expected_counts)
    expected = int(database["asset_count"])
    if expected != expected_counts[0]:
        raise RuntimeError(f"declared asset count differs for {scenario['id']}: expected={expected_counts[0]}, declared={expected}")
    if active["asset_count"] != expected or evidence["asset_count"] != expected:
        raise RuntimeError(f"asset count differs for {scenario['id']}: expected={expected}, active={active['asset_count']}, evidence={evidence['asset_count']}")
    if active["schema_version"] != evidence["schema_version"]:
        raise RuntimeError(f"schema differs for {scenario['id']}")
    if evidence["sha256"] != database["sha256"]:
        raise RuntimeError(f"evidence hash differs for {scenario['id']}")
    rows.append({"scenario_id": scenario["id"], "scenario_root": str(scenario_root),
                 "status": "matched", "expected_asset_count": expected,
                 "active": active, "evidence": evidence})

payload = {"schema": "pixel-tart-p3-pre-cleanup-database-audit/v1",
           "validation_mode": "automated", "owner_manual_ux_smoke": "waived",
           "manual_evidence_claimed": False, "automated_capture_status": "captured",
           "historical_manual_gate": "not_closed_superseded_as_release_blocker",
           "run_id": summary["run_id"],
           "source_head": summary["source_head"], "status": "passed",
           "scenario_count": len(rows), "scenarios": rows}
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
'@
    $process = Invoke-LoggedProcess -FilePath $python `
        -Arguments @('-I', '-c', $pythonCode, $summaryPath, $ActiveRunRoot, $auditPath) `
        -Name 'pre-cleanup-database-audit' -LogDirectory $LogDirectory -Timeout 300
    $audit = Get-Content -LiteralPath $auditPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($audit.status -cne 'passed' -or [int]$audit.scenario_count -ne 14) {
        throw 'The pre-cleanup database consistency audit did not pass all 14 P3 scenarios.'
    }
    return [ordered]@{
        path = $auditPath
        sha256 = Get-FileSha256 $auditPath
        scenario_count = [int]$audit.scenario_count
        result = $process
    }
}

function Invoke-Validator {
    param([string]$ActiveRunRoot, [string]$LogDirectory, [string]$Name = 'validator')
    $targetRoot = [IO.Path]::GetFullPath($ActiveRunRoot).TrimEnd('\', '/')
    $validator = Get-SealedAcceptanceInputPath $targetRoot 'Test-P3AssetLibraryAutomatedEvidence.ps1'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw "Sealed validator not found: $validator" }
    $validatorLogRoot = [IO.Path]::GetFullPath($LogDirectory).TrimEnd('\', '/')
    if (Test-PathWithin $validatorLogRoot $targetRoot) {
        throw "Validator log directory must be outside the sealed run root: $validatorLogRoot"
    }
    $targetManifestPath = Join-Path $targetRoot 'run-manifest.json'
    if (-not (Test-Path -LiteralPath $targetManifestPath -PathType Leaf)) { throw "Validator target manifest not found: $targetManifestPath" }
    try { $targetManifest = Get-Content -LiteralPath $targetManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Validator target manifest is not valid JSON: $targetManifestPath`n$($_.Exception.Message)" }
    $targetHead = [string]$targetManifest.source_head
    if ($targetHead -notmatch '^[0-9a-f]{40}$') { throw "Validator target manifest has an invalid source_head: $targetManifestPath" }
    $sealedContractPath = Get-SealedAcceptanceInputPath $targetRoot 'automated-acceptance-contract.json'
    try { $sealedContract = Get-Content -LiteralPath $sealedContractPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Validator target contract is not valid JSON: $sealedContractPath`n$($_.Exception.Message)" }
    $expectedNegativeProofCount = @($sealedContract.required_negative_fixtures).Count
    if ($expectedNegativeProofCount -le 0) { throw 'Validator target contract has no negative fixtures.' }
    $validatorTimeoutSeconds = [int]$sealedContract.validator_process_timeout_seconds
    $negativeProofTimeoutSeconds = [int]$sealedContract.negative_evidence_proof_timeout_seconds
    if ($negativeProofTimeoutSeconds -ne 3600 -or $validatorTimeoutSeconds -ne 3900 -or
        $validatorTimeoutSeconds -le $negativeProofTimeoutSeconds) {
        throw 'Validator target contract has invalid timeout bounds.'
    }
    $result = Invoke-LoggedProcess -FilePath 'powershell.exe' `
        -Arguments @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $validator, '-RunRoot', $ActiveRunRoot) `
        -Name $Name -LogDirectory $LogDirectory -Timeout $validatorTimeoutSeconds
    $stderrText = Get-Content -LiteralPath $result.stderr -Raw -Encoding UTF8 -ErrorAction Stop
    if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
        throw "Validator emitted unexpected stderr. See $($result.stderr)."
    }
    $stdoutText = Get-Content -LiteralPath $result.stdout -Raw -Encoding UTF8 -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($stdoutText)) { throw "Validator emitted empty stdout. See $($result.stdout)." }
    try { $validation = $stdoutText | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Validator stdout is not valid JSON. See $($result.stdout).`n$($_.Exception.Message)" }
    $validationRoot = [string]$validation.run_root
    try { $validationRoot = [IO.Path]::GetFullPath($validationRoot).TrimEnd('\', '/') } catch { $validationRoot = '' }
    if ([string]$validation.schema -cne 'pixel-tart-p3-automated-validation-result/v1' -or
        [string]$validation.status -cne 'passed' -or
        [bool]$validation.negative_proofs_skipped -or
        $validationRoot -cne $targetRoot -or
        [string]$validation.source_head -cne $targetHead -or
        [int]$validation.negative_fixture_proof_count -ne $expectedNegativeProofCount -or
        [string]$validation.negative_fixture_proof_sha256 -notmatch '^[0-9a-f]{64}$') {
        throw "Validator stdout failed the result contract. See $($result.stdout)."
    }
    return $result
}

function Invoke-RecoveryTest {
    Assert-NoDevPreview
    $sentinels = @{}
    foreach ($key in $script:environmentKeys) { $sentinels[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
    try {
        try { Invoke-WithEnvironment @{ PIXEL_TART_P3_AUTOMATED_ACCEPTANCE = 'recovery-sentinel' } { throw 'recovery-sentinel' } } catch {
            if ($_.Exception.Message -cne 'recovery-sentinel') { throw }
        }
        foreach ($key in $sentinels.Keys) {
            if (-not [string]::Equals([Environment]::GetEnvironmentVariable($key, 'Process'), $sentinels[$key], [StringComparison]::Ordinal)) {
                throw "Environment restoration failed for $key."
            }
        }
        Assert-NoDevPreview
        [pscustomobject]@{
            validation_mode = 'automated'
            owner_manual_ux_smoke = 'waived'
            manual_evidence_claimed = $false
            status = 'recovery-test-passed'
            devpreview_process_count = 0
            environment_restored = $true
            desktop_input_injection = 0
            display_setting_writes = 0
        } | ConvertTo-Json -Depth 5
    } finally {
        foreach ($key in $sentinels.Keys) { [Environment]::SetEnvironmentVariable($key, $sentinels[$key], 'Process') }
    }
}

function Invoke-DryRun {
    $head = Assert-CleanCommit
    Assert-NoDevPreview
    $dotnet = Get-DotNetPath
    $contractPath = Join-Path $PSScriptRoot 'automated-acceptance-contract.json'
    $validatorPath = Join-Path $PSScriptRoot 'Test-P3AssetLibraryAutomatedEvidence.ps1'
    $generatorPath = Join-Path $PSScriptRoot 'New-P3SyntheticFixture.py'
    foreach ($requiredPath in @($script:requiredAcceptanceInputFiles | ForEach-Object { Join-Path $PSScriptRoot $_ })) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Automated acceptance preflight file is missing: $requiredPath" }
    }
    $contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$contract.schema -cne 'pixel-tart-asset-library-p3-automated-acceptance-contract/v1' -or
        [string]$contract.validation_mode -cne 'automated' -or
        [string]$contract.owner_manual_ux_smoke -cne 'waived' -or
        [bool]$contract.manual_evidence_claimed -or
        [int]$contract.required_runner_session_count -ne 17 -or
        [string]$contract.process_table_snapshot_schema -cne 'pixel-tart-p3-process-table-snapshot/v1' -or
        [string]$contract.process_observation_schema -cne 'pixel-tart-p3-devpreview-process-observation/v1' -or
        [string]$contract.process_owner_schema -cne 'pixel-tart-p3-runner-process-owner/v1' -or
        [string]$contract.process_exit_diagnostic_schema -cne 'pixel-tart-p3-runner-process-exit-diagnostic/v1' -or
        [string]$contract.runner_session_result_schema -cne 'pixel-tart-p3-runner-session-result/v1' -or
        [string]$contract.process_table_convergence_schema -cne 'pixel-tart-p3-process-table-convergence/v1' -or
        [string]$contract.run_failure_model_schema -cne 'pixel-tart-p3-run-failure-model/v1' -or
        [int]$contract.negative_evidence_proof_timeout_seconds -ne 3600 -or
        [int]$contract.validator_process_timeout_seconds -ne 3900 -or
        [int]$contract.validator_process_timeout_seconds -le [int]$contract.negative_evidence_proof_timeout_seconds -or
        [string]$contract.process_exit_wait_strategy -cne 'shared-deadline-staged-evidence-and-exit' -or
        [int]$contract.process_exit_total_timeout_seconds -ne $script:p3ProcessStageTotalTimeoutSeconds -or
        [int]$contract.process_table_required_consecutive_empty_observations -ne 2 -or
        -not [bool]$contract.forced_cleanup_requires_retained_process_handle -or
        -not [bool]$contract.runner_preassigns_process_session_id -or
        [string]$contract.application_lifecycle_schema -cne 'pixel-tart-p3-automated-lifecycle/v1' -or
        [string]$contract.acceptance_input_snapshot_schema -cne 'pixel-tart-p3-acceptance-input-snapshot/v1' -or
        [string]$contract.run_seal_schema -cne 'pixel-tart-p3-run-seal/v1' -or
        [string]$contract.run_seal_file -cne 'runner/run-seal.json' -or
        -not [bool]$contract.run_seal_inventory_excludes_seal_file -or
        -not [bool]$contract.run_seal_requires_read_only -or
        (@($contract.required_acceptance_input_files | ForEach-Object { [string]$_ } | Sort-Object) -join '|') -cne
            (@($script:requiredAcceptanceInputFiles | Sort-Object) -join '|') -or
        @($contract.required_scenario_order).Count -ne 14 -or
        @($contract.required_restart_scenarios).Count -ne 3 -or
        [int]$contract.fixture.total_count -ne 10128 -or
        [int]$contract.repository.schema_version -ne 7) {
        throw 'Automated acceptance contract preflight failed.'
    }
    $fixedProcessIdentityFields = @(
        'Pid','ProcessName','StartTimeUtc','ExecutablePath','ExecutableSha256','RunId',
        'ProcessSessionId','WindowHandle','OwnedByRun','HasExited','ObservationError')
    if ((@($contract.process_identity_fields | ForEach-Object { [string]$_ }) -join '|') -cne
        ($fixedProcessIdentityFields -join '|')) {
        throw 'Automated acceptance process identity field contract preflight failed.'
    }
    if ((@($contract.required_application_lifecycle_events | ForEach-Object { [string]$_ }) -join '|') -cne
        ($script:p3LifecycleEvents -join '|')) {
        throw 'Automated acceptance application lifecycle event contract preflight failed.'
    }
    if ((@($contract.required_application_lifecycle_results | ForEach-Object { [string]$_ }) -join '|') -cne
        ($script:p3LifecycleResults -join '|')) {
        throw 'Automated acceptance application lifecycle result contract preflight failed.'
    }
    if ((@($contract.required_application_lifecycle_pending | ForEach-Object { [bool]$_ }) -join '|') -cne
        (@($script:p3LifecyclePending | ForEach-Object { [bool]$_ }) -join '|')) {
        throw 'Automated acceptance application lifecycle pending-state contract preflight failed.'
    }
    if ((@($contract.required_application_lifecycle_pending_operation_count_rules | ForEach-Object { [string]$_ }) -join '|') -cne
        ($script:p3LifecyclePendingOperationCountRules -join '|')) {
        throw 'Automated acceptance application lifecycle pending-operation contract preflight failed.'
    }
    $contractStageNames = @($contract.process_exit_stage_timeouts_seconds.PSObject.Properties.Name)
    $fixedStageNames = @($script:p3ProcessStageTimeoutSeconds.Keys)
    $contractStageTotal = 0
    foreach ($stageName in $contractStageNames) {
        $contractStageValue = [int]$contract.process_exit_stage_timeouts_seconds.$stageName
        $contractStageTotal += $contractStageValue
        if (-not $script:p3ProcessStageTimeoutSeconds.Contains($stageName) -or
            $contractStageValue -ne [int]$script:p3ProcessStageTimeoutSeconds[$stageName]) {
            throw "Automated acceptance process exit stage contract differs at '$stageName'."
        }
    }
    if (($contractStageNames -join '|') -cne ($fixedStageNames -join '|') -or
        $contractStageTotal -gt [int]$contract.process_exit_total_timeout_seconds) {
        throw 'Automated acceptance process exit stage bounds exceed the shared deadline.'
    }
    foreach ($scriptPath in @($script:requiredAcceptanceInputFiles | Where-Object { $_ -like '*.ps1' } | ForEach-Object { Join-Path $PSScriptRoot $_ })) {
        $parseErrors = $null
        [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$null, [ref]$parseErrors) | Out-Null
        if (@($parseErrors).Count -ne 0) { throw "PowerShell preflight parse failed for '$scriptPath'." }
    }
    $python = [IO.Path]::GetFullPath((Get-Command python.exe -ErrorAction Stop).Source)
    $pythonCompileExpression = 'compile(t,str(p),"exec")'
    $pythonCompileCode = "import ast,pathlib,sys;p=pathlib.Path(sys.argv[1]);s=p.read_bytes().decode();t=ast.parse(s);$($pythonCompileExpression.Replace([char]34, [char]39))"
    foreach ($pythonPath in @($script:requiredAcceptanceInputFiles | Where-Object { $_ -like '*.py' } | ForEach-Object { Join-Path $PSScriptRoot $_ })) {
        $pythonOutput = & $python -I -c $pythonCompileCode ([IO.Path]::GetFullPath($pythonPath)) 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Python AST/compile preflight failed for '$pythonPath': $($pythonOutput -join [Environment]::NewLine)"
        }
    }
    [pscustomobject]@{
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        status = 'ready-for-automated-run'
        source_head = $head
        dotnet = $dotnet
        python = $python
        devpreview_process_count = 0
    } | ConvertTo-Json -Depth 5
}

$script:repo = Get-RepositoryRoot
if ($Mode -eq 'DryRun') { Invoke-DryRun; exit 0 }
if ($Mode -eq 'RecoveryTest') { Invoke-RecoveryTest; exit 0 }
if ($Mode -eq 'ValidateExistingRun') {
    if (-not (Test-IsAbsolutePath $RunRoot)) { throw 'ValidateExistingRun requires an absolute RunRoot.' }
    $resolvedRunRoot = [IO.Path]::GetFullPath($RunRoot)
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedRunRoot 'run-manifest.json') -PathType Leaf)) {
        throw 'ValidateExistingRun requires a sealed P3 automated run root.'
    }
    $revalidationBase = Join-Path $script:repo '.validation'
    $logDirectory = Join-Path $revalidationBase ("P3-Automated-Revalidation-{0}-{1}" -f `
        [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'), [guid]::NewGuid().ToString('N').Substring(0, 12))
    if (Test-PathWithin $logDirectory $resolvedRunRoot) {
        throw 'The revalidation log directory must be outside the sealed run root.'
    }
    $fingerprintBefore = Get-RunTreeFingerprint $resolvedRunRoot
    $stateFingerprintBefore = Get-RunTreeStateFingerprint $resolvedRunRoot
    [IO.Directory]::CreateDirectory($revalidationBase) | Out-Null
    # Revalidating a sealed run must not add, replace, or rewrite anything in
    # that run root. Wrapper logs therefore live in a new ignored sibling root.
    try {
        [void](Invoke-Validator $resolvedRunRoot $logDirectory 'validator-read-only')
    } finally {
        $fingerprintAfter = Get-RunTreeFingerprint $resolvedRunRoot
        $stateFingerprintAfter = Get-RunTreeStateFingerprint $resolvedRunRoot
        if ($fingerprintAfter -cne $fingerprintBefore -or $stateFingerprintAfter -cne $stateFingerprintBefore) {
            throw 'ValidateExistingRun changed the sealed run tree.'
        }
    }
    Write-Output $resolvedRunRoot
    exit 0
}

$sourceHead = Assert-CleanCommit
Assert-NoDevPreview
$devPreviewGetProcessCountBefore = (Get-ProcessSnapshot).Items.Count
$devPreviewCimCountBefore = (Get-CimProcessSnapshot).Items.Count
$activeRunRoot = New-RunRoot
$acceptanceInputs = New-AcceptanceInputSnapshot $activeRunRoot
$logDirectory = Join-Path $activeRunRoot 'logs'
$runId = "p3-auto-$([guid]::NewGuid().ToString('N'))"
$validatorLogDirectory = Join-Path (Split-Path -Parent $activeRunRoot) "P3-Automated-Validator-$runId"
$runSealed = $false
$environmentBefore = @{}
foreach ($key in $script:environmentKeys) { $environmentBefore[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$manifestPath = Join-Path $activeRunRoot 'run-manifest.json'
$runCreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
$manifest = [ordered]@{
    schema_version = 'pixel-tart-p3-automated-run/v1'
    validation_mode = 'automated'
    owner_manual_ux_smoke = 'waived'
    manual_evidence_claimed = $false
    historical_manual_gate = 'not_closed_superseded_as_release_blocker'
    automated_capture_status = 'running'
    run_id = $runId
    run_root = $activeRunRoot
    repository_root = $script:repo
    branch = $script:expectedBranch
    source_head = $sourceHead
    created_at = $runCreatedAtUtc
    started_at = $runCreatedAtUtc
    failure_model_schema = 'pixel-tart-p3-run-failure-model/v1'
    primary_failure = $null
    cleanup_failures = @()
    observation_failures = @()
    acceptance_inputs = $acceptanceInputs
    run_seal = [ordered]@{
        schema = 'pixel-tart-p3-run-seal/v1'
        path = 'runner/run-seal.json'
        inventory_excludes_seal_file = $true
        read_only_required = $true
    }
}
$safetyStaticBefore = $null
$displayBefore = Get-DisplayObservation
$dotnetPidsBefore = @(Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
Write-JsonAtomic $manifestPath $manifest

$firstFailure = $null
$cleanupFailures = [Collections.Generic.List[object]]::new()
$observationFailures = [Collections.Generic.List[object]]::new()
$runCompleted = $false
try {
    $safetyStaticBefore = New-SafetyStaticScanInput $activeRunRoot
    $fixture = New-P3SyntheticFixture $activeRunRoot $acceptanceInputs
    $dotnet = Get-DotNetPath
    $restore = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'restore', 'RAWSelectionAssistant.sln',
        '-nodeReuse:false', '-p:UseSharedCompilation=false'
    ) -Name 'solution-restore' -LogDirectory $logDirectory -Timeout 1800
    $buildOutputBase = Join-Path $script:repo "src\RAWSelectionAssistant\bin\P3Automated\$runId"
    if (Test-Path -LiteralPath $buildOutputBase) { throw "The isolated automated build output is not fresh: $buildOutputBase" }
    $build = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'build', 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj', '-c', 'Debug', '--no-restore', '-t:Rebuild',
        '-nodeReuse:false', '-p:UseSharedCompilation=false', '-p:TreatWarningsAsErrors=true',
        '-p:ModularHarnessDevPreview=true', '-p:InputRoutingDiagnostics=true',
        '-p:AssetLibraryP3AutomatedAcceptance=true', '-p:ContinuousIntegrationBuild=true',
        "-p:SourceRevisionId=$sourceHead", "-p:BaseOutputPath=$buildOutputBase\"
    ) -Name 'devpreview-build' -LogDirectory $logDirectory -Timeout 1800

    $postBuildHead = Assert-CleanCommit
    if ($postBuildHead -cne $sourceHead) { throw 'The clean source HEAD changed during the automated acceptance build.' }

    $buildSourceExecutable = Join-Path $buildOutputBase 'Debug\net10.0-windows10.0.19041.0\win-x64\PixelTart_ModularHarness_V1_DevPreview.exe'
    $buildSourceModuleDll = Join-Path (Split-Path -Parent $buildSourceExecutable) 'PixelTart.Modules.AssetLibrary.dll'
    $buildSourceApplicationDll = Join-Path (Split-Path -Parent $buildSourceExecutable) 'PixelTart_ModularHarness_V1_DevPreview.dll'
    foreach ($path in @($buildSourceExecutable, $buildSourceApplicationDll, $buildSourceModuleDll)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build output missing: $path" } }
    $buildSourceExecutableHash = Get-FileSha256 $buildSourceExecutable
    $buildSourceApplicationHash = Get-FileSha256 $buildSourceApplicationDll
    $buildSourceModuleHash = Get-FileSha256 $buildSourceModuleDll
    $binarySnapshot = New-BinarySnapshot (Split-Path -Parent $buildSourceExecutable) (Join-Path $activeRunRoot 'binaries')
    $executable = Join-Path $binarySnapshot.directory ([IO.Path]::GetFileName($buildSourceExecutable))
    $applicationDll = Join-Path $binarySnapshot.directory ([IO.Path]::GetFileName($buildSourceApplicationDll))
    $moduleDll = Join-Path $binarySnapshot.directory ([IO.Path]::GetFileName($buildSourceModuleDll))
    if ((Get-FileSha256 $executable) -cne $buildSourceExecutableHash -or
        (Get-FileSha256 $applicationDll) -cne $buildSourceApplicationHash -or
        (Get-FileSha256 $moduleDll) -cne $buildSourceModuleHash) {
        throw 'The sealed executable, application assembly, or Asset Library module differs from its just-built source.'
    }
    $buildManifest = [ordered]@{
        schema_version = 'pixel-tart-p3-automated-build/v1'
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        historical_manual_gate = 'not_closed_superseded_as_release_blocker'
        automated_capture_status = 'captured'
        run_id = $runId
        source_head = $sourceHead
        configuration = 'Debug'
        repository_clean = $true
        executable_path = [IO.Path]::GetFullPath($executable)
        executable_sha256 = Get-FileSha256 $executable
        application_path = [IO.Path]::GetFullPath($applicationDll)
        application_sha256 = Get-FileSha256 $applicationDll
        asset_module_path = [IO.Path]::GetFullPath($moduleDll)
        asset_module_sha256 = Get-FileSha256 $moduleDll
        build_source_executable_path = [IO.Path]::GetFullPath($buildSourceExecutable)
        build_source_executable_sha256 = $buildSourceExecutableHash
        build_source_application_path = [IO.Path]::GetFullPath($buildSourceApplicationDll)
        build_source_application_sha256 = $buildSourceApplicationHash
        build_source_asset_module_path = [IO.Path]::GetFullPath($buildSourceModuleDll)
        build_source_asset_module_sha256 = $buildSourceModuleHash
        binary_snapshot = $binarySnapshot
        executable_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
        executable_product_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
        application_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).FileVersion
        application_product_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).ProductVersion
        asset_module_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($moduleDll).FileVersion
        asset_module_product_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($moduleDll).ProductVersion
        restore = $restore
        source_audit = @(
            (Get-GitBlobAudit $sourceHead 'src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml')
            (Get-GitBlobAudit $sourceHead 'src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml')
            (Get-GitBlobAudit $sourceHead 'src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml')
            (Get-GitBlobAudit $sourceHead 'src/RAWSelectionAssistant/Resources/DesignSystem/Theme.HighContrast.xaml')
        )
        build = $build
    }
    Write-JsonAtomic (Join-Path $activeRunRoot 'build-manifest.json') $buildManifest

    $primaryScenarios = @(
        'scope-switch/v1',
        'ime-cancellation/v1',
        'search-suggestions-history/v1',
        'folder-any-all-not/v1',
        'tag-any-all-not/v1',
        'scalar-null-composition/v1',
        'visual-composition/v1',
        'nested-canonical-query/v1',
        'invalid-query-fail-closed/v1',
        'smart-folder-lifecycle-preview/v1',
        'smart-folder-invalid-migration/v1',
        'tag-manager-lifecycle/v1',
        'bulk-metadata-journal/v1',
        'four-view-resilience-layout/v1'
    )
    $sessions = [Collections.Generic.List[object]]::new()
    $sessionIndex = 0
    foreach ($scenarioId in $primaryScenarios) {
        $sessionIndex++
        $scenarioBase = $scenarioId -replace '/v1$', ''
        $scenarioToken = $scenarioBase -replace '[^a-zA-Z0-9.-]', '-'
        $sessionName = ('{0:D2}-{1}' -f $sessionIndex, $scenarioToken)
        $sessions.Add((Invoke-AppPhase 'primary' @($scenarioId) $sessionName $sourceHead $executable $activeRunRoot $runId `
            $manifest.created_at $logDirectory $binarySnapshot))
    }
    $restartScenarios = @(
        'search-suggestions-history/v1',
        'smart-folder-lifecycle-preview/v1',
        'bulk-metadata-journal/v1'
    )
    foreach ($restartScenario in $restartScenarios) {
        $sessionIndex++
        $restartToken = (($restartScenario -replace '/v1$', '') -replace '[^a-zA-Z0-9.-]', '-')
        $restartName = ('{0:D2}-{1}-restart' -f $sessionIndex, $restartToken)
        $sessions.Add((Invoke-AppPhase 'restart' @($restartScenario) $restartName $sourceHead $executable $activeRunRoot $runId `
            $manifest.created_at $logDirectory $binarySnapshot))
    }
    # Compare every closed active SQLite repository with its immutable evidence
    # backup before cleaning generated runtime databases.
    $preCleanupDatabaseAudit = Invoke-PreCleanupDatabaseAudit $activeRunRoot $logDirectory
    $runtimeDatabaseCleanup = Remove-RunOwnedRuntimeDatabases $activeRunRoot
    $displayAfter = Get-DisplayObservation
    $environmentResiduals = @($script:environmentKeys | Where-Object {
        -not [string]::Equals([Environment]::GetEnvironmentVariable($_, 'Process'), $environmentBefore[$_], [StringComparison]::Ordinal)
    })
    $dotnetPidsAfter = @(Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $dotnetResidualPids = @($dotnetPidsAfter | Where-Object { $_ -notin $dotnetPidsBefore })
    $processCleanup = [ordered]@{
        all_scenarios_closed_normally = $true
        devpreview_get_process_count_before = $devPreviewGetProcessCountBefore
        devpreview_cim_count_before = $devPreviewCimCountBefore
        devpreview_get_process_count_after = (Get-ProcessSnapshot).Items.Count
        devpreview_cim_count_after = (Get-CimProcessSnapshot).Items.Count
        dotnet_process_count_before = $dotnetPidsBefore.Count
        dotnet_process_count_after = $dotnetPidsAfter.Count
        dotnet_residual_pid_count = $dotnetResidualPids.Count
        dotnet_residual_pids = $dotnetResidualPids
        db_sidecar_count_after = @(Get-ChildItem -LiteralPath (Join-Path $activeRunRoot 'runtime') -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*-wal' -or $_.Name -like '*-shm' }).Count
        runtime_database_count_after = $runtimeDatabaseCleanup.runtime_database_count_after
        runtime_database_cleanup = $runtimeDatabaseCleanup
        environment_residual_count = $environmentResiduals.Count
        environment_residual_keys = $environmentResiduals
        display_settings_unchanged = Test-SameDisplayObservation $displayBefore $displayAfter
        display_before = $displayBefore
        display_after = $displayAfter
    }
    $safetyStaticAfter = Measure-SafetyStaticScan $safetyStaticBefore.snapshot_root @($safetyStaticBefore.targets)
    $pathConfinementPaths = [Collections.Generic.List[string]]::new()
    foreach ($path in @(
        $fixture.directory, $fixture.database_path, $fixture.legacy_database_path,
        $fixture.expectations_path, $fixture.generator_script_path,
        $binarySnapshot.directory, $logDirectory,
        (Join-Path $activeRunRoot 'runtime'), (Join-Path $activeRunRoot 'app'),
        (Join-Path $activeRunRoot 'runner'))) {
        if (-not [string]::IsNullOrWhiteSpace([string]$path)) { $pathConfinementPaths.Add([IO.Path]::GetFullPath([string]$path)) }
    }
    foreach ($session in @($sessions)) {
        foreach ($field in 'stdout','stderr','scenario_root','executable_path','application_path','asset_module_path',
            'process_exit_diagnostic_path','lifecycle_path','phase_summary_path','result_path') {
            $pathConfinementPaths.Add([IO.Path]::GetFullPath([string]$session.$field))
        }
    }
    $outsideRunRootPaths = @($pathConfinementPaths | Where-Object { -not (Test-PathWithin $_ $activeRunRoot) })
    $pathConfinement = [ordered]@{
        schema = 'pixel-tart-p3-run-owned-path-confinement/v1'
        run_root = $activeRunRoot
        observed_path_count = $pathConfinementPaths.Count
        outside_run_root_path_count = $outsideRunRootPaths.Count
        outside_run_root_paths = $outsideRunRootPaths
        source_path_observation = [string]$fixture.source_path_observation
        source_path_count = [int]$fixture.source_path_count
        source_paths_inside_fixture_count = [int]$fixture.source_paths_inside_fixture_count
        source_paths_outside_fixture_count = [int]$fixture.source_paths_outside_fixture_count
        source_path_tree_sha256 = [string]$fixture.source_path_tree_sha256
        user_source_read_count = [int]$fixture.user_source_read_count
        user_source_write_count = [int]$fixture.user_source_write_count
        user_source_move_count = $outsideRunRootPaths.Count
        user_source_delete_count = $outsideRunRootPaths.Count
        user_source_rename_count = $outsideRunRootPaths.Count
        permanent_delete_count = $outsideRunRootPaths.Count
    }
    $environmentBeforeRows = @($script:environmentKeys | ForEach-Object {
        $value = $environmentBefore[$_]
        [ordered]@{ key = $_; is_null = $null -eq $value; value_sha256 = Get-TextSha256 $(if ($null -eq $value) { '' } else { [string]$value }) }
    })
    $environmentAfterRows = @($script:environmentKeys | ForEach-Object {
        $value = [Environment]::GetEnvironmentVariable($_, 'Process')
        [ordered]@{ key = $_; is_null = $null -eq $value; value_sha256 = Get-TextSha256 $(if ($null -eq $value) { '' } else { [string]$value }) }
    })
    $safetyMeasurement = [ordered]@{
        schema = 'pixel-tart-p3-safety-measurement/v1'
        static_scan_before = $safetyStaticBefore
        static_scan_after = $safetyStaticAfter
        source_snapshot_unchanged = [string]$safetyStaticBefore.snapshot_tree_sha256 -ceq [string]$safetyStaticAfter.snapshot_tree_sha256
        process_observation = $processCleanup
        environment_observation = [ordered]@{
            observed_keys = @($script:environmentKeys)
            before = $environmentBeforeRows
            after = $environmentAfterRows
            residual_count = $environmentResiduals.Count
            residual_keys = $environmentResiduals
        }
        display_observation = [ordered]@{
            before = $displayBefore
            after = $displayAfter
            unchanged = Test-SameDisplayObservation $displayBefore $displayAfter
        }
        path_confinement = $pathConfinement
    }
    $safety = [ordered]@{
        desktop_input_injection_count = Get-SafetyRuleCount $safetyStaticAfter 'desktop_input_injection'
        uia_invoke_count = Get-SafetyRuleCount $safetyStaticAfter 'uia_invoke'
        forced_foreground_count = Get-SafetyRuleCount $safetyStaticAfter 'forced_foreground'
        real_display_setting_write_count = Get-SafetyRuleCount $safetyStaticAfter 'real_display_setting_write'
        eagle_read_count = Get-SafetyRuleCount $safetyStaticAfter 'eagle_io'
        eagle_write_count = Get-SafetyRuleCount $safetyStaticAfter 'eagle_io'
        user_source_read_count = [int]$pathConfinement.user_source_read_count
        user_source_write_count = [int]$pathConfinement.user_source_write_count
        user_source_move_count = [int]$pathConfinement.user_source_move_count
        user_source_delete_count = [int]$pathConfinement.user_source_delete_count
        user_source_rename_count = [int]$pathConfinement.user_source_rename_count
        direct_width_mutation_count = Get-SafetyRuleCount $safetyStaticAfter 'direct_width_mutation'
        direct_settings_mutation_count = Get-SafetyRuleCount $safetyStaticAfter 'direct_settings_mutation'
        direct_sqlite_row_edit_count = Get-SafetyRuleCount $safetyStaticAfter 'direct_sqlite_row_edit'
        network_upload_count = Get-SafetyRuleCount $safetyStaticAfter 'network_upload'
        third_party_upload_count = Get-SafetyRuleCount $safetyStaticAfter 'network_upload'
        ai_upload_count = Get-SafetyRuleCount $safetyStaticAfter 'network_upload'
        mcp_upload_count = Get-SafetyRuleCount $safetyStaticAfter 'network_upload'
        permanent_delete_count = [int]$pathConfinement.permanent_delete_count
    }
    if ($processCleanup.devpreview_get_process_count_after -ne 0 -or $processCleanup.devpreview_cim_count_after -ne 0 -or
        $processCleanup.dotnet_residual_pid_count -ne 0 -or
        $processCleanup.db_sidecar_count_after -ne 0 -or $processCleanup.environment_residual_count -ne 0 -or
        $processCleanup.runtime_database_count_after -ne 0 -or
        -not $processCleanup.display_settings_unchanged -or
        -not $safetyMeasurement.source_snapshot_unchanged -or
        @($safety.Values | Where-Object { [int]$_ -ne 0 }).Count -ne 0) {
        throw 'Automated process/environment/display cleanup verification failed.'
    }
    $manifest.automated_capture_status = 'captured'
    $manifest.finished_at = [DateTimeOffset]::UtcNow.ToString('O')
    $manifest.fixture = $fixture
    $manifest.build_manifest = Join-Path $activeRunRoot 'build-manifest.json'
    $manifest.sessions = @($sessions)
    $manifest.pre_cleanup_database_audit = $preCleanupDatabaseAudit
    $manifest.process_cleanup_verified = $true
    $manifest.safety = $safety
    $manifest.safety_measurement = $safetyMeasurement
    $manifest.process_cleanup = $processCleanup
    Write-JsonAtomic $manifestPath $manifest
    [void](Assert-AcceptanceInputSnapshot $acceptanceInputs $activeRunRoot)
    Assert-TrackedCleanAndHead $sourceHead 'Validator preflight'
    [void](New-RunSeal $activeRunRoot $runId $sourceHead)
    $runSealed = $true
    [void](Invoke-Validator $activeRunRoot $validatorLogDirectory)
    $runCompleted = $true
} catch {
    $firstFailure = $_
    foreach ($binding in @(
        [pscustomobject]@{ key = 'cleanup_failures'; target = $cleanupFailures },
        [pscustomobject]@{ key = 'observation_failures'; target = $observationFailures })) {
        try {
            $serialized = [string]$firstFailure.Exception.Data[$binding.key]
            if (-not [string]::IsNullOrWhiteSpace($serialized)) {
                foreach ($failure in @($serialized | ConvertFrom-Json -ErrorAction Stop)) { $binding.target.Add($failure) }
            }
        } catch {
            $observationFailures.Add((ConvertTo-StructuredFailure 'observation' 'exception-diagnostic-deserialization' $_))
        }
    }
} finally {
    foreach ($key in $environmentBefore.Keys) {
        try { [Environment]::SetEnvironmentVariable($key, $environmentBefore[$key], 'Process') }
        catch {
            if ($null -eq $firstFailure) { $firstFailure = $_ }
            $cleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' "environment-restore:$key" $_))
        }
    }
    try {
        $primaryException = if ($null -ne $firstFailure) { $firstFailure.Exception } else { $null }
        [void](Invoke-FinalDevPreviewCheck $primaryException $cleanupFailures $observationFailures)
    } catch {
        if ($null -eq $firstFailure) { $firstFailure = $_ }
        else { $cleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' 'final-devpreview-check' $_)) }
    }
}

if ($null -ne $firstFailure) {
    if (-not $runSealed) {
        $manifest.automated_capture_status = 'failed'
        $manifest.finished_at = [DateTimeOffset]::UtcNow.ToString('O')
        $manifest.failure = $firstFailure.Exception.ToString()
        $manifest.primary_failure = ConvertTo-StructuredFailure 'primary' 'run' $firstFailure
        $manifest.cleanup_failures = @($cleanupFailures)
        $manifest.observation_failures = @($observationFailures)
        try { Write-JsonAtomic $manifestPath $manifest }
        catch {
            $cleanupFailures.Add((ConvertTo-StructuredFailure 'cleanup' 'failed-run-manifest-write' $_))
            $firstFailure.Exception.Data['failed_run_manifest_write'] = $_.Exception.ToString()
        }
    }
    throw (New-P3FinalFailureException $activeRunRoot $firstFailure.Exception @($cleanupFailures) @($observationFailures))
}

if (-not $runCompleted) { throw 'P3 automated acceptance ended without a success or failure outcome.' }
Write-Output $activeRunRoot
