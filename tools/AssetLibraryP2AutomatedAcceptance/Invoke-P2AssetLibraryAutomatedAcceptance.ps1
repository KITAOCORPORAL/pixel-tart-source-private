[CmdletBinding()]
param(
    [ValidateSet('Run', 'DryRun', 'ValidateExistingRun', 'RecoveryTest')]
    [string]$Mode = 'Run',
    [string]$OutputRoot,
    [string]$RunRoot,
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:expectedBranch = 'feature/asset-library-eagle-parity-p2'
$script:expectedProcessName = 'PixelTart_ModularHarness_V1_DevPreview'
$script:environmentKeys = @(
    'PIXEL_TART_ACCEPTANCE_ROOT',
    'PIXEL_TART_ASSET_LIBRARY_DEMO_DIR',
    'PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE',
    'PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE',
    'PIXEL_TART_ASSET_LIBRARY_P1_HEAD',
    'PIXEL_TART_P2_AUTOMATED_HEAD',
    'PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS',
    'PIXEL_TART_P2_AUTOMATED_ACCEPTANCE',
    'PIXEL_TART_P2_AUTOMATED_RUN_ROOT',
    'PIXEL_TART_P2_AUTOMATED_PLAN_PATH',
    'PIXEL_TART_P2_AUTOMATED_SOURCE_HEAD',
    'PIXEL_TART_P2_AUTOMATED_FIXTURE_ROOT',
    'MSBUILDDISABLENODEREUSE'
)

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

function Get-ProcessSnapshot {
    return @(Get-Process -Name $script:expectedProcessName -ErrorAction SilentlyContinue | ForEach-Object {
        [pscustomobject]@{ pid = $_.Id; path = $(try { $_.Path } catch { $null }) }
    })
}

function Get-CimProcessSnapshot {
    $executableName = "$($script:expectedProcessName).exe"
    return @(Get-CimInstance -ClassName Win32_Process -Filter "Name='$executableName'" -ErrorAction Stop | ForEach-Object {
        [pscustomobject]@{ pid = [int]$_.ProcessId; path = $_.ExecutablePath }
    })
}

function Assert-NoDevPreview {
    $processes = @(Get-ProcessSnapshot)
    $cimProcesses = @(Get-CimProcessSnapshot)
    if ($processes.Count -ne 0 -or $cimProcesses.Count -ne 0) {
        $pids = @($processes.pid) + @($cimProcesses.pid) | Sort-Object -Unique
        throw "Automated acceptance requires both DevPreview process tables to be empty; found PID(s): $($pids -join ', ')."
    }
}

function Get-DisplayObservation {
    if (-not ('PixelTartP2AutomatedDisplayObservation' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class PixelTartP2AutomatedDisplayObservation
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);
}
'@
    }
    $appliedDpi = $null
    try { $appliedDpi = (Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction Stop).AppliedDPI } catch { }
    return [ordered]@{
        primary_width = [PixelTartP2AutomatedDisplayObservation]::GetSystemMetrics(0)
        primary_height = [PixelTartP2AutomatedDisplayObservation]::GetSystemMetrics(1)
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
    $parent = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $temporary = "$Path.tmp"
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::Exists($Path)) {
        $backup = "$Path.bak"
        if ([IO.File]::Exists($backup)) { [IO.File]::Delete($backup) }
        try { [IO.File]::Replace($temporary, $Path, $backup) }
        finally { if ([IO.File]::Exists($backup)) { [IO.File]::Delete($backup) } }
    } else {
        [IO.File]::Move($temporary, $Path)
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
        schema = 'pixel-tart-p2-run-owned-binary-snapshot/v1'
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
    $path = Join-Path $base "P2-Automated-Acceptance-$stamp-$([guid]::NewGuid().ToString('N').Substring(0,12))"
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
        [string]$LogDirectory,
        [AllowNull()]$BinarySnapshot
    )
    Assert-NoDevPreview
    if ($ScenarioIds.Count -ne 1) { throw 'Each automated app process must own exactly one scenario.' }
    $scenarioDirectory = ($ScenarioIds[0] -replace '[^a-zA-Z0-9.-]', '-')
    # The sole restart phase deliberately reopens its scenario's isolated application
    # root. Every other scenario gets a fresh root and a separate process.
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
    Write-JsonAtomic $planPath ([ordered]@{
        schema_version = 'pixel-tart-p2-automated-plan/v1'
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        run_id = $RunId
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
    })
    $stdout = Join-Path $LogDirectory "app-$SessionName.stdout.log"
    $stderr = Join-Path $LogDirectory "app-$SessionName.stderr.log"
    $environment = @{
        PIXEL_TART_ACCEPTANCE_ROOT = $runtimeRoot
        PIXEL_TART_ASSET_LIBRARY_DEMO_DIR = $null
        PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE = $null
        PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE = $null
        PIXEL_TART_ASSET_LIBRARY_P1_HEAD = $null
        PIXEL_TART_P2_AUTOMATED_HEAD = $null
        PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS = $null
        PIXEL_TART_P2_AUTOMATED_ACCEPTANCE = '1'
        PIXEL_TART_P2_AUTOMATED_RUN_ROOT = $ActiveRunRoot
        PIXEL_TART_P2_AUTOMATED_PLAN_PATH = $planPath
        PIXEL_TART_P2_AUTOMATED_SOURCE_HEAD = $Head
        PIXEL_TART_P2_AUTOMATED_FIXTURE_ROOT = $fixtureRoot
    }
    $started = [DateTimeOffset]::UtcNow
    $process = Invoke-WithEnvironment $environment {
        Start-Process -FilePath $Executable -WorkingDirectory (Split-Path -Parent $Executable) `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    }
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
        throw "Automated app phase '$Phase' timed out; forced cleanup was required."
    }
    $process.WaitForExit()
    $phaseSummaryPath = Join-Path $ActiveRunRoot ("app\evidence\summary-{0}-{1}.json" -f $scenarioDirectory, $Phase)
    if (-not (Test-Path -LiteralPath $phaseSummaryPath -PathType Leaf)) {
        throw "Automated app phase '$Phase' did not write its immutable phase summary: $phaseSummaryPath"
    }
    $phaseSummary = Get-Content -LiteralPath $phaseSummaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $phaseScenario = @($phaseSummary.scenarios | Where-Object { $_.id -ceq $ScenarioIds[0] })
    if ($phaseScenario.Count -ne 1) { throw "Automated app phase '$Phase' has no unique scenario summary." }
    $processSessionId = [string]$phaseSummary.process_session_id
    if ($processSessionId -cnotmatch '^[0-9a-f]{32}$') { throw "Automated app phase '$Phase' has an invalid process_session_id." }
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
    $snapshotTreeAfter = Assert-BinarySnapshotState $BinarySnapshot
    if ($snapshotTreeAfter -cne $snapshotTreeBefore -or
        (Get-FileSha256 $expectedExecutablePath) -cne $expectedExecutableHash -or
        (Get-FileSha256 $expectedApplicationPath) -cne $expectedApplicationHash -or
        (Get-FileSha256 $expectedModulePath) -cne $expectedModuleHash) {
        throw "Automated app phase '$Phase' changed its sealed executable, application assembly, or module."
    }
    $result = [ordered]@{
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
    }
    Write-JsonAtomic (Join-Path $LogDirectory "app-$SessionName.result.json") $result
    if ($result.exit_code -ne 0) { throw "Automated app phase '$Phase' failed with exit $($result.exit_code)." }
    Start-Sleep -Milliseconds 500
    Assert-NoDevPreview
    return $result
}

function New-P2SyntheticFixture {
    param([string]$ActiveRunRoot)
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
    $generatorPath = [IO.Path]::GetFullPath((Join-Path $directory 'fixture-generator.py'))
    if (-not (Test-PathWithin $databasePath $directory) -or [IO.Path]::GetFileName($databasePath) -cne 'asset-library-v16.db') {
        throw "The synthetic fixture database path is invalid: $databasePath"
    }
    if (-not (Test-PathWithin $generatorPath $directory) -or [IO.Path]::GetFileName($generatorPath) -cne 'fixture-generator.py') {
        throw "The synthetic fixture generator path is invalid: $generatorPath"
    }
    if ((Test-Path -LiteralPath $databasePath -PathType Leaf) -or (Test-Path -LiteralPath $generatorPath)) {
        throw 'The synthetic fixture generator and database paths must not already exist.'
    }
    $python = [IO.Path]::GetFullPath((Get-Command python.exe -ErrorAction Stop).Source)
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
        throw "The Python executable is not a file: $python"
    }
    $pythonCode = @'
import datetime, hashlib, json, pathlib, sqlite3, sys, uuid
if len(sys.argv) != 3:
    raise RuntimeError(f"fixture-generator.py expects exactly 2 arguments (root, database), got {len(sys.argv) - 1}")
root_arg, db_arg = sys.argv[1], sys.argv[2]
root_path = pathlib.Path(root_arg)
db_path = pathlib.Path(db_arg)
if not root_path.is_absolute() or not db_path.is_absolute():
    raise RuntimeError("fixture root and database arguments must be absolute paths")
root = root_path.resolve()
db = db_path.resolve()
if not root.is_dir():
    raise RuntimeError(f"fixture root is not an existing directory: {root}")
if db.parent != root or db.name != "asset-library-v16.db":
    raise RuntimeError(f"database must be exactly <fixture-root>/asset-library-v16.db: {db}")
if db.exists():
    raise RuntimeError("P2 fixture database already exists")
connection = sqlite3.connect(db)
try:
    connection.executescript("""
    CREATE TABLE AssetLibrarySchemaInfo(
      Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);
    PRAGMA journal_mode=DELETE;
    CREATE TABLE AssetItems(
      AssetId TEXT NOT NULL PRIMARY KEY, SourcePath TEXT NOT NULL,
      NormalizedSourcePath TEXT NOT NULL, DuplicateDiscriminator TEXT NOT NULL DEFAULT '',
      DisplayName TEXT NOT NULL, Extension TEXT NOT NULL, MediaType TEXT NOT NULL,
      FileSize INTEGER NOT NULL DEFAULT 0 CHECK(FileSize >= 0), ContentHash TEXT NULL,
      Width INTEGER NULL, Height INTEGER NULL, Orientation TEXT NULL, CaptureTime TEXT NULL,
      AddedAt TEXT NOT NULL, ModifiedAt TEXT NOT NULL,
      Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5),
      Comment TEXT NOT NULL DEFAULT '', IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN(0,1)),
      IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
      ImportMode TEXT NOT NULL DEFAULT 'Reference', ManagedCopyPath TEXT NULL,
      UNIQUE(NormalizedSourcePath,DuplicateDiscriminator));
    CREATE TABLE AssetVisualAnalysis(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL, ContentHash TEXT NOT NULL,
      PaletteSize INTEGER NOT NULL DEFAULT 5, PaletteSort TEXT NOT NULL DEFAULT 'Weight',
      AnalysisSource TEXT NOT NULL, SourceProfile TEXT NOT NULL, AnalysisProfile TEXT NOT NULL,
      ResultJson TEXT NOT NULL, CreatedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,AnalysisVersion,PaletteSize,PaletteSort));
    CREATE TABLE AssetVisualFeatures(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL,
      PaletteSize INTEGER NOT NULL CHECK(PaletteSize=5), PaletteSort TEXT NOT NULL CHECK(PaletteSort='Weight'),
      ContentFingerprint TEXT NOT NULL, SourceContentHash TEXT NULL,
      Outcome TEXT NOT NULL, FailureReason TEXT NULL,
      AnalysisSource TEXT NOT NULL, SourceProfile TEXT NOT NULL, AnalysisProfile TEXT NOT NULL,
      Harmony TEXT NULL, ToneKey TEXT NULL, Contrast TEXT NULL, LuminanceSpan TEXT NULL,
      Saturation TEXT NULL, WarmCool TEXT NULL, DominantHue REAL NULL, SecondaryHue REAL NULL,
      AverageHue REAL NULL, AverageLuma REAL NULL, MedianLuma REAL NULL, ContrastMetric REAL NULL,
      LumaSpreadMetric REAL NULL, AverageSaturation REAL NULL, MedianSaturation REAL NULL,
      AverageLightness REAL NULL, WarmCoolMetric REAL NULL, DeepShadowRatio REAL NULL,
      ShadowRatio REAL NULL, MidtoneRatio REAL NULL, HighlightRatio REAL NULL, SpecularRatio REAL NULL,
      BlackClipRatio REAL NULL, WhiteClipRatio REAL NULL, HistogramLumaSignature TEXT NULL,
      PaletteSignature TEXT NULL, ResultJson TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,AnalysisVersion));
    CREATE TABLE AssetVisualPaletteColors(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL, ColorIndex INTEGER NOT NULL,
      Red INTEGER NOT NULL CHECK(Red BETWEEN 0 AND 255), Green INTEGER NOT NULL CHECK(Green BETWEEN 0 AND 255),
      Blue INTEGER NOT NULL CHECK(Blue BETWEEN 0 AND 255), LabL REAL NOT NULL, LabA REAL NOT NULL, LabB REAL NOT NULL,
      Hue REAL NOT NULL, Saturation REAL NOT NULL, Chroma REAL NOT NULL, Weight REAL NOT NULL CHECK(Weight>=0 AND Weight<=1),
      Hex TEXT NOT NULL, PRIMARY KEY(AssetId,AnalysisVersion,ColorIndex));
    CREATE TABLE AssetFolders(FolderId TEXT NOT NULL PRIMARY KEY,ParentFolderId TEXT NULL,Name TEXT NOT NULL,
      Description TEXT NOT NULL DEFAULT '',Icon TEXT NULL,Color TEXT NULL,SortOrder INTEGER NOT NULL DEFAULT 0,
      CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,IsArchived INTEGER NOT NULL DEFAULT 0,IsSystem INTEGER NOT NULL DEFAULT 0,
      AutoTagIdsJson TEXT NOT NULL DEFAULT '[]');
    CREATE TABLE AssetFolderMemberships(AssetId TEXT NOT NULL,FolderId TEXT NOT NULL,AddedAt TEXT NOT NULL,PRIMARY KEY(AssetId,FolderId));
    CREATE TABLE TagGroups(TagGroupId TEXT NOT NULL PRIMARY KEY,Name TEXT NOT NULL UNIQUE,SortOrder INTEGER NOT NULL DEFAULT 0,
      CreatedAt TEXT NOT NULL,IsArchived INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE AssetTags(TagId TEXT NOT NULL PRIMARY KEY,Name TEXT NOT NULL,TagGroupId TEXT NULL,SortOrder INTEGER NOT NULL DEFAULT 0,
      UsageCount INTEGER NOT NULL DEFAULT 0,CreatedAt TEXT NOT NULL,IsArchived INTEGER NOT NULL DEFAULT 0,UNIQUE(TagGroupId,Name));
    CREATE TABLE AssetTagMemberships(AssetId TEXT NOT NULL,TagId TEXT NOT NULL,AddedAt TEXT NOT NULL,PRIMARY KEY(AssetId,TagId));
    CREATE TABLE SmartFolders(SmartFolderId TEXT NOT NULL PRIMARY KEY,Name TEXT NOT NULL UNIQUE,Logic TEXT NOT NULL DEFAULT 'And',
      Description TEXT NOT NULL DEFAULT '',CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,IsArchived INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE SmartFolderRules(RuleId TEXT NOT NULL PRIMARY KEY,SmartFolderId TEXT NOT NULL,Field TEXT NOT NULL,Operator TEXT NOT NULL,
      Value TEXT NOT NULL DEFAULT '',Negated INTEGER NOT NULL DEFAULT 0,SortOrder INTEGER NOT NULL DEFAULT 0,GroupId TEXT NULL,GroupLogic TEXT NOT NULL DEFAULT 'And');
    """)
    now = datetime.datetime(2026, 9, 1, tzinfo=datetime.timezone.utc)
    connection.execute("INSERT INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(?,?)", (6, now.isoformat()))
    rows = []
    for index in range(512):
        archived = 1 if index >= 500 else 0
        missing = 1 if (30 <= index < 60 or 500 <= index < 502) else 0
        asset_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"pixel-tart-p2-fixture-{index:04d}"))
        source = str(root / "media" / f"P2_{index:04d}.jpg")
        display_name = f"P2_{index:04d} \u4eba\u7269\u53c2\u8003 {index % 7 + 1:02d}.jpg"
        content_hash = hashlib.sha256(f"pixel-tart-p2-source-{index:04d}".encode("ascii")).hexdigest()
        capture_time = (now + datetime.timedelta(days=index % 31, seconds=index)).isoformat()
        added_at = (now + datetime.timedelta(seconds=index)).isoformat()
        rows.append((asset_id, source, source.lower(), display_name, ".jpg", "Image",
                     4096 + index, content_hash, 640 + index % 7 * 80, 480 + index % 5 * 64,
                     "Landscape" if index % 2 == 0 else "Portrait", capture_time, added_at, added_at, index % 6,
                     f"synthetic fixture item {index:04d}", missing, archived, "Reference", None))
    connection.executemany("""
      INSERT INTO AssetItems(AssetId,SourcePath,NormalizedSourcePath,DisplayName,Extension,MediaType,
        FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath)
       VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""", rows)
    ids = [row[0] for row in rows]
    analysis_version = "visual-analysis-v2"
    feature_rows = []
    for index, asset_id in enumerate(ids):
        if index >= 192:
            continue
        outcome = "Succeeded" if index < 128 else "Failed"
        source_hash = rows[index][7]
        feature_rows.append((
            asset_id, analysis_version, 5, "Weight", source_hash, source_hash, outcome,
            None if outcome == "Succeeded" else "deterministic synthetic analysis failure",
            "RasterOriginal", "UnknownAssumedSrgb", "sRGB IEC61966-2.1",
            "Monochrome", "Mid", "Medium", "Medium", "Medium", "Warm",
            float((index * 13) % 360), float((index * 17) % 360), float((index * 19) % 360),
            96.0 + (index % 20), 92.0 + (index % 18), 0.45, 0.30,
            0.35, 0.22, 0.40, 0.12, 0.04, 0.20, 0.30, 0.18, 0.08,
            0.01, 0.02, "synthetic-luma", "synthetic-palette", None,
            now.isoformat(), now.isoformat()))
    connection.executemany("""
      INSERT INTO AssetVisualFeatures(
        AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,
        AnalysisSource,SourceProfile,AnalysisProfile,Harmony,ToneKey,Contrast,LuminanceSpan,Saturation,WarmCool,
        DominantHue,SecondaryHue,AverageHue,AverageLuma,MedianLuma,ContrastMetric,LumaSpreadMetric,AverageSaturation,
        MedianSaturation,AverageLightness,WarmCoolMetric,DeepShadowRatio,ShadowRatio,MidtoneRatio,HighlightRatio,
        SpecularRatio,BlackClipRatio,WhiteClipRatio,HistogramLumaSignature,PaletteSignature,ResultJson,CreatedAt,UpdatedAt)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""", feature_rows)
    palette_rows = []
    for index, asset_id in enumerate(ids[:128]):
        source_hash = rows[index][7]
        for color_index in range(5):
            red = (index * 17 + color_index * 31) % 256
            green = (index * 23 + color_index * 29) % 256
            blue = (index * 37 + color_index * 13) % 256
            palette_rows.append((asset_id, analysis_version, color_index, red, green, blue,
                                 50.0 + color_index, -10.0 + color_index, 12.0 + color_index,
                                 float((index * 13 + color_index * 7) % 360), 0.25, 20.0, 0.2,
                                 f"#{red:02X}{green:02X}{blue:02X}"))
    connection.executemany("""
      INSERT INTO AssetVisualPaletteColors(
        AssetId,AnalysisVersion,ColorIndex,Red,Green,Blue,LabL,LabA,LabB,Hue,Saturation,Chroma,Weight,Hex)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)""", palette_rows)
    uid = lambda name: str(uuid.uuid5(uuid.NAMESPACE_URL, "pixel-tart-p2-" + name))
    folder_rows = [(uid("folder-people"), None, "\u4eba\u7269", "\u4eba\u7269\u53c2\u8003", 0),
                   (uid("folder-portrait"), uid("folder-people"), "\u4eba\u50cf", "\u4eba\u50cf\u6837\u7247", 0),
                   (uid("folder-light"), None, "\u706f\u5149", "\u706f\u5149\u53c2\u8003", 1),
                   (uid("folder-archived"), None, "\u65e7\u9879\u76ee", "\u5f52\u6863\u6587\u4ef6\u5939", 2)]
    connection.executemany("INSERT INTO AssetFolders(FolderId,ParentFolderId,Name,Description,SortOrder,CreatedAt,UpdatedAt,IsArchived) VALUES(?,?,?,?,?,?,?,?)",
       [(fid,parent,name,desc,order,now.isoformat(),now.isoformat(),1 if name=="\u65e7\u9879\u76ee" else 0) for fid,parent,name,desc,order in folder_rows])
    connection.executemany("INSERT INTO AssetFolderMemberships VALUES(?,?,?)",
       [(ids[i], uid("folder-portrait") if i % 2 == 0 else uid("folder-light"), now.isoformat()) for i in range(300)])
    groups = [(uid("group-subject"),"\u4e3b\u9898",0,now.isoformat(),0),(uid("group-tone"),"\u8272\u8c03",1,now.isoformat(),0)]
    connection.executemany("INSERT INTO TagGroups VALUES(?,?,?,?,?)", groups)
    tag_defs = [("portrait","\u4eba\u50cf","group-subject"),("fashion","\u65f6\u5c1a","group-subject"),("warm","\u6696\u8272","group-tone"),("cool","\u51b7\u8272","group-tone")]
    tags = [(uid("tag-"+key),name,uid(group),order,0,now.isoformat(),0) for order,(key,name,group) in enumerate(tag_defs)]
    connection.executemany("INSERT INTO AssetTags VALUES(?,?,?,?,?,?,?)", tags)
    memberships = []
    for i, asset_id in enumerate(ids[:500]):
        memberships.append((asset_id, uid("tag-portrait" if i % 2 == 0 else "tag-fashion"), now.isoformat()))
        memberships.append((asset_id, uid("tag-warm" if i % 3 == 0 else "tag-cool"), now.isoformat()))
    connection.executemany("INSERT INTO AssetTagMemberships VALUES(?,?,?)", memberships)
    connection.execute("UPDATE AssetTags SET UsageCount=(SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=AssetTags.TagId)")
    smart = uid("smart-rating-four")
    connection.execute("INSERT INTO SmartFolders VALUES(?,?,?,?,?,?,?)", (smart,"\u56db\u661f\u7cbe\u9009","And","\u8bc4\u5206\u81f3\u5c11\u56db\u661f",now.isoformat(),now.isoformat(),0))
    connection.execute("INSERT INTO SmartFolderRules VALUES(?,?,?,?,?,?,?,?,?)", (uid("rule-rating-four"),smart,"Rating","GreaterThanOrEqual","4",0,0,None,"And"))
    connection.commit()
    counts = connection.execute("SELECT COUNT(*),SUM(IsArchived=0),SUM(IsArchived=1) FROM AssetItems").fetchone()
    if counts != (512, 500, 12):
        raise RuntimeError(f"fixture count mismatch: {counts}")
    if connection.execute("PRAGMA quick_check").fetchone()[0] != "ok":
        raise RuntimeError("fixture quick_check failed")
    visual_counts = connection.execute("SELECT Outcome,COUNT(*) FROM AssetVisualFeatures WHERE AnalysisVersion=? GROUP BY Outcome", (analysis_version,)).fetchall()
    visual_by_outcome = {outcome: count for outcome, count in visual_counts}
    print(json.dumps({
        "schema_version": 6,
        "total_count": counts[0],
        "active_count": counts[1],
        "archived_count": counts[2],
        "display_name_count": sum(1 for row in rows if any('\u3400' <= character <= '\u9fff' for character in row[3])),
        "content_hash_count": sum(1 for row in rows if row[7]),
        "missing_count": connection.execute("SELECT COUNT(*) FROM AssetItems WHERE IsMissing=1").fetchone()[0],
        "visual_feature_counts": {
            "analysis_version": analysis_version,
            "valid": visual_by_outcome.get("Succeeded", 0),
            "failed": visual_by_outcome.get("Failed", 0),
            "not_analyzed": counts[0] - sum(visual_by_outcome.values()),
            "feature_rows": sum(visual_by_outcome.values())
        }
    }, ensure_ascii=False, separators=(",", ":")))
finally:
    connection.close()
'@
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
    $generatorArguments = @('-I', $generatorPath, $directory, $databasePath)
    if ($generatorArguments.Count -ne 4) {
        throw "The fixture generator invocation must contain exactly 4 process arguments; actual count=$($generatorArguments.Count)."
    }
    if ($generatorArguments[0] -cne '-I' -or
        [IO.Path]::IsPathRooted([string]$generatorArguments[0]) -or
        [IO.Path]::GetFullPath([string]$generatorArguments[1]) -cne $generatorPath -or
        [IO.Path]::GetFullPath([string]$generatorArguments[2]) -cne $directory -or
        [IO.Path]::GetFullPath([string]$generatorArguments[3]) -cne $databasePath -or
        -not (Test-PathWithin ([string]$generatorArguments[1]) $directory) -or
        -not (Test-PathWithin ([string]$generatorArguments[2]) $runRoot) -or
        -not (Test-PathWithin ([string]$generatorArguments[3]) $directory)) {
        throw 'The fixture generator invocation contains an invalid or escaped argument path.'
    }
    $generatorLogDirectory = Join-Path $runRoot 'runner'
    $generatorResult = Invoke-LoggedProcess -FilePath $python `
        -Arguments $generatorArguments `
        -Name 'fixture-generator' -LogDirectory $generatorLogDirectory -Timeout 300
    if (-not (Test-Path -LiteralPath $databasePath -PathType Leaf)) {
        throw "The fixture generator completed without creating its database: $databasePath"
    }
    $generatorOutputLines = @(Get-Content -LiteralPath $generatorResult.stdout -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($generatorOutputLines.Count -ne 1) {
        throw "The fixture generator must emit exactly one metadata JSON record; actual count=$($generatorOutputLines.Count)."
    }
    try { $generatedMetadata = $generatorOutputLines[0] | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "The fixture generator metadata record is invalid JSON: $($_.Exception.Message)" }
    if ([int]$generatedMetadata.schema_version -ne 6 -or
        [int]$generatedMetadata.total_count -ne 512 -or
        [int]$generatedMetadata.active_count -ne 500 -or
        [int]$generatedMetadata.archived_count -ne 12 -or
        [int]$generatedMetadata.display_name_count -ne 512 -or
        [int]$generatedMetadata.content_hash_count -ne 512 -or
        [int]$generatedMetadata.missing_count -ne 32 -or
        [string]$generatedMetadata.visual_feature_counts.analysis_version -cne 'visual-analysis-v2' -or
        [int]$generatedMetadata.visual_feature_counts.valid -ne 128 -or
        [int]$generatedMetadata.visual_feature_counts.failed -ne 64 -or
        [int]$generatedMetadata.visual_feature_counts.not_analyzed -ne 320 -or
        [int]$generatedMetadata.visual_feature_counts.feature_rows -ne 192) {
        throw 'The fixture generator metadata does not satisfy the P2 fixture contract.'
    }
    $fixture = [ordered]@{
        schema = 'pixel-tart-p2-synthetic-fixture/v1'
        source_kind = 'synthetic-run-owned'
        directory = [IO.Path]::GetFullPath($directory)
        database_path = [IO.Path]::GetFullPath($databasePath)
        database_sha256 = Get-FileSha256 $databasePath
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
        user_source_read_count = 0
        user_source_write_count = 0
    }
    Write-JsonAtomic (Join-Path $directory 'fixture-manifest.json') $fixture
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

def inspect(path):
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
    if quick_check != "ok" or schema_version != 6 or (asset_count, active_count, archived_count) != (512, 500, 12):
        raise RuntimeError(f"invalid database {path}: quick_check={quick_check}, schema={schema_version}, counts={(asset_count,active_count,archived_count)}")
    return {"path": str(path), "sha256": before, "quick_check": quick_check,
            "schema_version": schema_version, "asset_count": asset_count,
            "active_count": active_count, "archived_count": archived_count}

rows = []
expected_ids = ["fixture-integrity/v1", "organization-browser/v1",
                "smart-tag-query/v1", "four-views-query-sort/v1",
                "selection-large/v1", "metadata-drag-command/v1",
                "inspector-states/v1", "resilience-states/v1",
                "restart-persistence/v1", "layout-dpi-performance/v1"]
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
    active = inspect(declared_active)
    evidence_refs = database["evidence_paths"]
    if not evidence_refs:
        raise RuntimeError(f"scenario has no evidence database: {scenario['id']}")
    if evidence_refs[-1] != database["path"]:
        raise RuntimeError(f"final evidence reference differs for {scenario['id']}")
    evidence_path = inside((run_root / evidence_refs[-1]).resolve())
    if evidence_path in seen_evidence:
        raise RuntimeError(f"evidence database path is reused: {evidence_path}")
    seen_evidence.add(evidence_path)
    evidence = inspect(evidence_path)
    expected = int(database["asset_count"])
    if active["asset_count"] != expected or evidence["asset_count"] != expected:
        raise RuntimeError(f"asset count differs for {scenario['id']}: expected={expected}, active={active['asset_count']}, evidence={evidence['asset_count']}")
    if active["schema_version"] != evidence["schema_version"]:
        raise RuntimeError(f"schema differs for {scenario['id']}")
    if evidence["sha256"] != database["sha256"]:
        raise RuntimeError(f"evidence hash differs for {scenario['id']}")
    rows.append({"scenario_id": scenario["id"], "scenario_root": str(scenario_root),
                 "status": "matched", "expected_asset_count": expected,
                 "active": active, "evidence": evidence})

payload = {"schema": "pixel-tart-p2-pre-cleanup-database-audit/v1",
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
    if ($audit.status -cne 'passed' -or [int]$audit.scenario_count -ne 10) {
        throw 'The pre-cleanup database consistency audit did not pass all ten scenarios.'
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
    $validator = Join-Path $PSScriptRoot 'Test-P2AssetLibraryAutomatedEvidence.ps1'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw "Validator not found: $validator" }
    $targetRoot = [IO.Path]::GetFullPath($ActiveRunRoot).TrimEnd('\', '/')
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
    $result = Invoke-LoggedProcess -FilePath 'powershell.exe' `
        -Arguments @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $validator, '-RunRoot', $ActiveRunRoot) `
        -Name $Name -LogDirectory $LogDirectory -Timeout 300
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
    if ([string]$validation.schema -cne 'pixel-tart-p2-automated-validation-result/v1' -or
        [string]$validation.status -cne 'passed' -or
        $validationRoot -cne $targetRoot -or
        [string]$validation.source_head -cne $targetHead) {
        throw "Validator stdout failed the result contract. See $($result.stdout)."
    }
    return $result
}

function Invoke-RecoveryTest {
    Assert-NoDevPreview
    $sentinels = @{}
    foreach ($key in $script:environmentKeys) { $sentinels[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
    try {
        try { Invoke-WithEnvironment @{ PIXEL_TART_P2_AUTOMATED_ACCEPTANCE = 'recovery-sentinel' } { throw 'recovery-sentinel' } } catch {
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
    $validatorPath = Join-Path $PSScriptRoot 'Test-P2AssetLibraryAutomatedEvidence.ps1'
    foreach ($requiredPath in @($contractPath, $validatorPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Automated acceptance preflight file is missing: $requiredPath" }
    }
    $contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$contract.schema -cne 'pixel-tart-asset-library-p2-automated-acceptance-contract/v1' -or
        [string]$contract.validation_mode -cne 'automated' -or
        [string]$contract.owner_manual_ux_smoke -cne 'waived' -or
        [bool]$contract.manual_evidence_claimed) {
        throw 'Automated acceptance contract preflight failed.'
    }
    foreach ($scriptPath in @($PSCommandPath, $validatorPath)) {
        $parseErrors = $null
        [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$null, [ref]$parseErrors) | Out-Null
        if (@($parseErrors).Count -ne 0) { throw "PowerShell preflight parse failed for '$scriptPath'." }
    }
    [pscustomobject]@{
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        status = 'ready-for-automated-run'
        source_head = $head
        dotnet = $dotnet
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
        throw 'ValidateExistingRun requires a sealed P2 automated run root.'
    }
    $revalidationBase = Join-Path $script:repo '.validation'
    $logDirectory = Join-Path $revalidationBase ("P2-Automated-Revalidation-{0}-{1}" -f `
        [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'), [guid]::NewGuid().ToString('N').Substring(0, 12))
    if (Test-PathWithin $logDirectory $resolvedRunRoot) {
        throw 'The revalidation log directory must be outside the sealed run root.'
    }
    $fingerprintBefore = Get-RunTreeFingerprint $resolvedRunRoot
    [IO.Directory]::CreateDirectory($revalidationBase) | Out-Null
    # Revalidating a sealed run must not add, replace, or rewrite anything in
    # that run root. Wrapper logs therefore live in a new ignored sibling root.
    try {
        [void](Invoke-Validator $resolvedRunRoot $logDirectory 'validator-read-only')
    } finally {
        $fingerprintAfter = Get-RunTreeFingerprint $resolvedRunRoot
        if ($fingerprintAfter -cne $fingerprintBefore) {
            throw 'ValidateExistingRun changed the sealed run tree.'
        }
    }
    Write-Output $resolvedRunRoot
    exit 0
}

$sourceHead = Assert-CleanCommit
Assert-NoDevPreview
$activeRunRoot = New-RunRoot
$logDirectory = Join-Path $activeRunRoot 'logs'
$runId = "p2-auto-$([guid]::NewGuid().ToString('N'))"
$validatorLogDirectory = Join-Path (Split-Path -Parent $activeRunRoot) "P2-Automated-Validator-$runId"
$environmentBefore = @{}
foreach ($key in $script:environmentKeys) { $environmentBefore[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$manifestPath = Join-Path $activeRunRoot 'run-manifest.json'
$manifest = [ordered]@{
    schema_version = 'pixel-tart-p2-automated-run/v1'
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
    started_at = [DateTimeOffset]::UtcNow.ToString('O')
}
$displayBefore = Get-DisplayObservation
$dotnetPidsBefore = @(Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
Write-JsonAtomic $manifestPath $manifest

try {
    $fixture = New-P2SyntheticFixture $activeRunRoot
    $dotnet = Get-DotNetPath
    $restore = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'restore', 'RAWSelectionAssistant.sln',
        '-nodeReuse:false', '-p:UseSharedCompilation=false'
    ) -Name 'solution-restore' -LogDirectory $logDirectory -Timeout 1800
    $buildOutputBase = Join-Path $script:repo "src\RAWSelectionAssistant\bin\P2Automated\$runId"
    if (Test-Path -LiteralPath $buildOutputBase) { throw "The isolated automated build output is not fresh: $buildOutputBase" }
    $build = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'build', 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj', '-c', 'Debug', '--no-restore', '-t:Rebuild',
        '-nodeReuse:false', '-p:UseSharedCompilation=false', '-p:TreatWarningsAsErrors=true',
        '-p:ModularHarnessDevPreview=true', '-p:InputRoutingDiagnostics=true',
        '-p:AssetLibraryP2AutomatedAcceptance=true', '-p:ContinuousIntegrationBuild=true',
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
        schema_version = 'pixel-tart-p2-automated-build/v1'
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
        'fixture-integrity/v1',
        'organization-browser/v1',
        'smart-tag-query/v1',
        'four-views-query-sort/v1',
        'selection-large/v1',
        'metadata-drag-command/v1',
        'inspector-states/v1',
        'resilience-states/v1',
        'restart-persistence/v1',
        'layout-dpi-performance/v1'
    )
    $sessions = [Collections.Generic.List[object]]::new()
    $sessionIndex = 0
    foreach ($scenarioId in $primaryScenarios) {
        $sessionIndex++
        $scenarioBase = $scenarioId -replace '/v1$', ''
        $scenarioToken = $scenarioBase -replace '[^a-zA-Z0-9.-]', '-'
        $sessionName = ('{0:D2}-{1}' -f $sessionIndex, $scenarioToken)
        $sessions.Add((Invoke-AppPhase 'primary' @($scenarioId) $sessionName $sourceHead $executable $activeRunRoot $runId $logDirectory $binarySnapshot))
    }
    $restartScenarios = @('restart-persistence/v1')
    foreach ($restartScenario in $restartScenarios) {
        $sessionIndex++
        $restartToken = (($restartScenario -replace '/v1$', '') -replace '[^a-zA-Z0-9.-]', '-')
        $restartName = ('{0:D2}-{1}-restart' -f $sessionIndex, $restartToken)
        $sessions.Add((Invoke-AppPhase 'restart' @($restartScenario) $restartName $sourceHead $executable $activeRunRoot $runId $logDirectory $binarySnapshot))
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
        devpreview_get_process_count_after = @(Get-ProcessSnapshot).Count
        devpreview_cim_count_after = @(Get-CimProcessSnapshot).Count
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
    $safety = [ordered]@{
        desktop_input_injection_count = 0
        uia_invoke_count = 0
        forced_foreground_count = 0
        real_display_setting_write_count = 0
        eagle_read_count = 0
        eagle_write_count = 0
        user_source_read_count = 0
        user_source_write_count = 0
        user_source_move_count = 0
        user_source_delete_count = 0
        user_source_rename_count = 0
        direct_width_mutation_count = 0
        direct_settings_mutation_count = 0
        direct_sqlite_row_edit_count = 0
    }
    if ($processCleanup.devpreview_get_process_count_after -ne 0 -or $processCleanup.devpreview_cim_count_after -ne 0 -or
        $processCleanup.dotnet_residual_pid_count -ne 0 -or
        $processCleanup.db_sidecar_count_after -ne 0 -or $processCleanup.environment_residual_count -ne 0 -or
        $processCleanup.runtime_database_count_after -ne 0 -or
        -not $processCleanup.display_settings_unchanged) {
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
    $manifest.process_cleanup = $processCleanup
    Write-JsonAtomic $manifestPath $manifest
    Assert-TrackedCleanAndHead $sourceHead 'Validator preflight'
    [void](Invoke-Validator $activeRunRoot $validatorLogDirectory)
    Write-Output $activeRunRoot
} catch {
    $manifest.automated_capture_status = 'failed'
    $manifest.finished_at = [DateTimeOffset]::UtcNow.ToString('O')
    $manifest.failure = $_.Exception.ToString()
    Write-JsonAtomic $manifestPath $manifest
    throw "P2 automated acceptance failed. Run root retained: $activeRunRoot`n$($_.Exception.Message)"
} finally {
    foreach ($key in $environmentBefore.Keys) { [Environment]::SetEnvironmentVariable($key, $environmentBefore[$key], 'Process') }
    Assert-NoDevPreview
}
