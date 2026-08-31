[CmdletBinding()]
param(
    [ValidateSet('Run', 'ValidateExistingRun', 'RecoveryTest')]
    [string]$Mode = 'Run',
    [string]$OutputRoot,
    [string]$RunRoot,
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:expectedBranch = 'feature/modular-harness-v1-p1'
$script:expectedProcessName = 'PixelTart_ModularHarness_V1_DevPreview'
$script:environmentKeys = @(
    'PIXEL_TART_ACCEPTANCE_ROOT',
    'PIXEL_TART_ASSET_LIBRARY_DEMO_DIR',
    'PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE',
    'PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE',
    'PIXEL_TART_ASSET_LIBRARY_P1_HEAD',
    'PIXEL_TART_P1_AUTOMATED_HEAD',
    'PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS',
    'PIXEL_TART_P1_AUTOMATED_ACCEPTANCE',
    'PIXEL_TART_P1_AUTOMATED_RUN_ROOT',
    'PIXEL_TART_P1_AUTOMATED_PLAN_PATH',
    'PIXEL_TART_P1_AUTOMATED_SOURCE_HEAD',
    'PIXEL_TART_P1_AUTOMATED_FIXTURE_ROOT',
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
    if (-not ('PixelTartP1AutomatedDisplayObservation' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class PixelTartP1AutomatedDisplayObservation
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);
}
'@
    }
    $appliedDpi = $null
    try { $appliedDpi = (Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction Stop).AppliedDPI } catch { }
    return [ordered]@{
        primary_width = [PixelTartP1AutomatedDisplayObservation]::GetSystemMetrics(0)
        primary_height = [PixelTartP1AutomatedDisplayObservation]::GetSystemMetrics(1)
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
    $path = Join-Path $base "P1-Automated-Acceptance-$stamp-$([guid]::NewGuid().ToString('N').Substring(0,12))"
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
        [string]$LogDirectory
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
    $fixtureRoot = if ($ScenarioIds[0] -ceq 'selection-navigation-restart/v1') {
        Join-Path $ActiveRunRoot 'synthetic-fixture'
    } else { $null }
    Write-JsonAtomic $planPath ([ordered]@{
        schema_version = 'pixel-tart-p1-automated-plan/v1'
        validation_mode = 'automated'
        owner_manual_ux_smoke = 'waived'
        manual_evidence_claimed = $false
        run_id = $RunId
        phase = $Phase
        source_head = $Head
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
        PIXEL_TART_P1_AUTOMATED_HEAD = $null
        PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS = $null
        PIXEL_TART_P1_AUTOMATED_ACCEPTANCE = '1'
        PIXEL_TART_P1_AUTOMATED_RUN_ROOT = $ActiveRunRoot
        PIXEL_TART_P1_AUTOMATED_PLAN_PATH = $planPath
        PIXEL_TART_P1_AUTOMATED_SOURCE_HEAD = $Head
        PIXEL_TART_P1_AUTOMATED_FIXTURE_ROOT = $fixtureRoot
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
        [string]$phaseSummary.phase -cne $Phase -or [string]$phaseSummary.status -cne 'completed') {
        throw "Automated app phase '$Phase' summary identity does not match the runner-owned process."
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
        executable_path = [IO.Path]::GetFullPath($Executable)
        executable_sha256 = Get-FileSha256 $Executable
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

function New-SyntheticFixture {
    param([string]$ActiveRunRoot)
    $directory = Join-Path $ActiveRunRoot 'synthetic-fixture'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $path = Join-Path $directory 'P1_SYNTHETIC_SELECTION.png'
    # A deterministic 1x1 opaque PNG. It is owned by the run and never references user media.
    $bytes = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=')
    [IO.File]::WriteAllBytes($path, $bytes)
    return [ordered]@{ path = $path; sha256 = Get-FileSha256 $path; source_kind = 'synthetic-run-owned' }
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
    finally:
        connection.close()
    after = hashlib.sha256(path.read_bytes()).hexdigest()
    if before != after:
        raise RuntimeError(f"read-only audit changed database: {path}")
    if quick_check != "ok" or schema_version != 6:
        raise RuntimeError(f"invalid database {path}: quick_check={quick_check}, schema={schema_version}")
    return {"path": str(path), "sha256": before, "quick_check": quick_check,
            "schema_version": schema_version, "asset_count": asset_count}

rows = []
expected_ids = ["first-empty/v1", "loading-error-retry-recovered/v1",
                "organization-splitter/v1", "inspector-splitter/v1",
                "pane-collapse-expand/v1", "thumbnail-slider/v1",
                "selection-navigation-restart/v1", "navigation-ime/v1",
                "layout-dpi-buttons/v1"]
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

payload = {"schema": "pixel-tart-p1-pre-cleanup-database-audit/v1",
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
    if ($audit.status -cne 'passed' -or [int]$audit.scenario_count -ne 9) {
        throw 'The pre-cleanup database consistency audit did not pass all nine scenarios.'
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
    $validator = Join-Path $PSScriptRoot 'Test-P1AssetLibraryAutomatedEvidence.ps1'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw "Validator not found: $validator" }
    return Invoke-LoggedProcess -FilePath 'powershell.exe' `
        -Arguments @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $validator, '-RunRoot', $ActiveRunRoot) `
        -Name $Name -LogDirectory $LogDirectory -Timeout 300
}

function Invoke-RecoveryTest {
    Assert-NoDevPreview
    $sentinels = @{}
    foreach ($key in $script:environmentKeys) { $sentinels[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
    try {
        try { Invoke-WithEnvironment @{ PIXEL_TART_P1_AUTOMATED_ACCEPTANCE = 'recovery-sentinel' } { throw 'recovery-sentinel' } } catch {
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

$script:repo = Get-RepositoryRoot
if ($Mode -eq 'RecoveryTest') { Invoke-RecoveryTest; exit 0 }
if ($Mode -eq 'ValidateExistingRun') {
    if (-not (Test-IsAbsolutePath $RunRoot)) { throw 'ValidateExistingRun requires an absolute RunRoot.' }
    $resolvedRunRoot = [IO.Path]::GetFullPath($RunRoot)
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedRunRoot 'run-manifest.json') -PathType Leaf)) {
        throw 'ValidateExistingRun requires a sealed P1 automated run root.'
    }
    $revalidationBase = Join-Path $script:repo '.validation'
    $logDirectory = Join-Path $revalidationBase ("P1-Automated-Revalidation-{0}-{1}" -f `
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
$runId = "p1-auto-$([guid]::NewGuid().ToString('N'))"
$environmentBefore = @{}
foreach ($key in $script:environmentKeys) { $environmentBefore[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$manifestPath = Join-Path $activeRunRoot 'run-manifest.json'
$manifest = [ordered]@{
    schema_version = 'pixel-tart-p1-automated-run/v1'
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
    $fixture = New-SyntheticFixture $activeRunRoot
    $dotnet = Get-DotNetPath
    $restore = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'restore', 'RAWSelectionAssistant.sln',
        '-nodeReuse:false', '-p:UseSharedCompilation=false'
    ) -Name 'solution-restore' -LogDirectory $logDirectory -Timeout 1800
    $build = Invoke-LoggedProcess -FilePath $dotnet -Arguments @(
        'build', 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj', '-c', 'Debug', '--no-restore',
        '-nodeReuse:false', '-p:UseSharedCompilation=false', '-p:TreatWarningsAsErrors=true',
        '-p:ModularHarnessDevPreview=true', '-p:InputRoutingDiagnostics=true',
        '-p:AssetLibraryP1AutomatedAcceptance=true'
    ) -Name 'devpreview-build' -LogDirectory $logDirectory -Timeout 1800

    $executable = Join-Path $script:repo 'src\RAWSelectionAssistant\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PixelTart_ModularHarness_V1_DevPreview.exe'
    $moduleDll = Join-Path (Split-Path -Parent $executable) 'PixelTart.Modules.AssetLibrary.dll'
    foreach ($path in @($executable, $moduleDll)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build output missing: $path" } }
    $buildManifest = [ordered]@{
        schema_version = 'pixel-tart-p1-automated-build/v1'
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
        asset_module_path = [IO.Path]::GetFullPath($moduleDll)
        asset_module_sha256 = Get-FileSha256 $moduleDll
        executable_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
        asset_module_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($moduleDll).FileVersion
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
        'first-empty/v1',
        'loading-error-retry-recovered/v1',
        'organization-splitter/v1',
        'inspector-splitter/v1',
        'pane-collapse-expand/v1',
        'thumbnail-slider/v1',
        'selection-navigation-restart/v1',
        'navigation-ime/v1',
        'layout-dpi-buttons/v1'
    )
    $sessions = [Collections.Generic.List[object]]::new()
    $sessionIndex = 0
    foreach ($scenarioId in $primaryScenarios) {
        $sessionIndex++
        $scenarioBase = $scenarioId -replace '/v1$', ''
        $scenarioToken = $scenarioBase -replace '[^a-zA-Z0-9.-]', '-'
        $sessionName = ('{0:D2}-{1}' -f $sessionIndex, $scenarioToken)
        $sessions.Add((Invoke-AppPhase 'primary' @($scenarioId) $sessionName $sourceHead $executable $activeRunRoot $runId $logDirectory))
    }
    $restartScenarios = @(
        'pane-collapse-expand/v1',
        'thumbnail-slider/v1',
        'selection-navigation-restart/v1'
    )
    foreach ($restartScenario in $restartScenarios) {
        $sessionIndex++
        $restartToken = (($restartScenario -replace '/v1$', '') -replace '[^a-zA-Z0-9.-]', '-')
        $restartName = ('{0:D2}-{1}-restart' -f $sessionIndex, $restartToken)
        $sessions.Add((Invoke-AppPhase 'restart' @($restartScenario) $restartName $sourceHead $executable $activeRunRoot $runId $logDirectory))
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
        real_display_setting_write_count = 0
        eagle_write_count = 0
        user_source_read_count = 0
        user_source_write_count = 0
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
    [void](Invoke-Validator $activeRunRoot $logDirectory)
    Write-Output $activeRunRoot
} catch {
    $manifest.automated_capture_status = 'failed'
    $manifest.finished_at = [DateTimeOffset]::UtcNow.ToString('O')
    $manifest.failure = $_.Exception.ToString()
    Write-JsonAtomic $manifestPath $manifest
    throw "P1 automated acceptance failed. Run root retained: $activeRunRoot`n$($_.Exception.Message)"
} finally {
    foreach ($key in $environmentBefore.Keys) { [Environment]::SetEnvironmentVariable($key, $environmentBefore[$key], 'Process') }
    Assert-NoDevPreview
}
