[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias('RunRoot')]
    [string[]]$RunRoots,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateRange(30, 7200)]
    [int]$ValidatorTimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$Message) { throw "P3 automated run set rejected: $Message" }
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') }
function Test-AbsolutePath([AllowEmptyString()][string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path -notmatch '^(?:[A-Za-z]:[\\/]|\\\\)') { return $false }
    try { [void][IO.Path]::GetFullPath($Path); return $true } catch { return $false }
}
function Inside-Or-Equal([string]$Path, [string]$Parent) {
    $child = Full $Path
    $root = Full $Parent
    return $child.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $child.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
function Bytes-ToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { $_.ToString('x2', [Globalization.CultureInfo]::InvariantCulture) })
}
function Sha256-Bytes([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return $algorithm.ComputeHash($Bytes) }
    finally { $algorithm.Dispose() }
}
function Sha256-Text([AllowEmptyString()][string]$Text) {
    return (Bytes-ToHex (Sha256-Bytes ([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant()
}
function Hash-File([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return (Bytes-ToHex ($algorithm.ComputeHash($stream))).ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Read-Json([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "$Name is missing: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop }
    catch { Fail "$Name is not valid JSON: $Path`n$($_.Exception.Message)" }
}
function Quote-ProcessArgument([AllowEmptyString()][string]$Value) {
    if ($Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
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
function Invoke-NormalValidator([string]$ValidatorPath, [string]$RunRoot, [int]$TimeoutSeconds) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'powershell.exe'
    $startInfo.Arguments = (@(
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', $ValidatorPath, '-RunRoot', $RunRoot
    ) | ForEach-Object { Quote-ProcessArgument ([string]$_) }) -join ' '
    $startInfo.WorkingDirectory = Split-Path -Parent $ValidatorPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { Fail "normal validator did not start for $RunRoot" }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            Fail "normal validator timed out for $RunRoot"
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            Fail "normal validator failed for $RunRoot (exit=$($process.ExitCode)).`n$stdout`n$stderr"
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Fail "normal validator emitted unexpected stderr for $RunRoot.`n$stderr"
        }
        if ([string]::IsNullOrWhiteSpace($stdout)) { Fail "normal validator emitted empty stdout for $RunRoot" }
        try { $result = $stdout | ConvertFrom-Json -ErrorAction Stop }
        catch { Fail "normal validator stdout is not valid JSON for $RunRoot.`n$($_.Exception.Message)" }
        if ([string]$result.schema -cne 'pixel-tart-p3-automated-validation-result/v1' -or
            [string]$result.status -cne 'passed' -or [bool]$result.negative_proofs_skipped -or
            (Full ([string]$result.run_root)) -cne $RunRoot) {
            Fail "normal validator result contract differs for $RunRoot"
        }
        return $result
    }
    finally { $process.Dispose() }
}
function Add-RunOwnedEvidencePath {
    param(
        [Collections.Generic.HashSet[string]]$Paths,
        [string]$RunRoot,
        [AllowEmptyString()][string]$DeclaredPath,
        [string]$Name
    )
    if ([string]::IsNullOrWhiteSpace($DeclaredPath)) { Fail "$Name is empty for $RunRoot" }
    $candidate = if (Test-AbsolutePath $DeclaredPath) { Full $DeclaredPath } else { Full (Join-Path $RunRoot $DeclaredPath) }
    if (-not (Inside-Or-Equal $candidate $RunRoot) -or $candidate -ceq $RunRoot) {
        Fail "$Name escapes its run root: $candidate"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { Fail "$Name is missing: $candidate" }
    [void]$Paths.Add($candidate)
}

if ($RunRoots.Count -ne 3) { Fail "exactly 3 run roots are required; actual count=$($RunRoots.Count)" }
if (-not (Test-AbsolutePath $OutputDirectory)) { Fail 'OutputDirectory must be an absolute path.' }

$resolvedRoots = [Collections.Generic.List[string]]::new()
$rootSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($declaredRoot in $RunRoots) {
    if (-not (Test-AbsolutePath $declaredRoot)) { Fail "each run root must be absolute: $declaredRoot" }
    $root = Full $declaredRoot
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail "run root is missing: $root" }
    if (-not $rootSet.Add($root)) { Fail "run roots must be different absolute directories: $root" }
    $resolvedRoots.Add($root)
}

$outputRoot = Full $OutputDirectory
foreach ($root in $resolvedRoots) {
    if (Inside-Or-Equal $outputRoot $root) { Fail "OutputDirectory must be outside every run root: $outputRoot" }
}

$runIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$validatorHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$processSessions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$processIdentities = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$globalEvidencePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$expectedHead = $null
$runRows = [Collections.Generic.List[object]]::new()

for ($runIndex = 0; $runIndex -lt $resolvedRoots.Count; $runIndex++) {
    $root = $resolvedRoots[$runIndex]
    $validator = Full (Join-Path $root 'runner\acceptance-inputs\Test-P3AssetLibraryAutomatedEvidence.ps1')
    if (-not (Inside-Or-Equal $validator $root) -or -not (Test-Path -LiteralPath $validator -PathType Leaf)) {
        Fail "run[$runIndex] sealed normal validator is missing: $validator"
    }
    $validatorSha256 = Hash-File $validator
    [void]$validatorHashes.Add($validatorSha256)
    $validation = Invoke-NormalValidator $validator $root $ValidatorTimeoutSeconds
    $manifest = Read-Json (Join-Path $root 'run-manifest.json') "run[$runIndex] manifest"
    $summary = Read-Json (Join-Path $root 'app\evidence\summary.json') "run[$runIndex] application summary"
    if ((Full ([string]$manifest.run_root)) -cne $root) { Fail "run[$runIndex] manifest root differs" }
    $runId = [string]$manifest.run_id
    $head = [string]$manifest.source_head
    if ($runId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { Fail "run[$runIndex] has an invalid run_id" }
    if ($head -cnotmatch '^[0-9a-f]{40}$') { Fail "run[$runIndex] has an invalid source_head" }
    if (-not $runIds.Add($runId)) { Fail "run_id is reused across the three runs: $runId" }
    if ($null -eq $expectedHead) { $expectedHead = $head }
    elseif ($head -cne $expectedHead) { Fail "the three runs do not use the same HEAD: expected=$expectedHead actual=$head" }
    if ([string]$validation.run_id -cne $runId -or [string]$validation.source_head -cne $head -or
        [string]$summary.source_head -cne $head -or
        [string]$summary.run_id -cne $runId) {
        Fail "run[$runIndex] validator/manifest/summary identity differs"
    }

    $sessions = @($manifest.sessions)
    if ($sessions.Count -eq 0) { Fail "run[$runIndex] has no process sessions" }
    $runProcessSessions = [Collections.Generic.List[string]]::new()
    $runProcessIdentities = [Collections.Generic.List[string]]::new()
    $evidencePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($session in $sessions) {
        $processSessionId = [string]$session.process_session_id
        if ($processSessionId -cnotmatch '^[0-9a-f]{32}$') { Fail "run[$runIndex] has an invalid process_session_id" }
        if (-not $processSessions.Add($processSessionId)) { Fail "process_session_id is reused across the three runs: $processSessionId" }
        $runProcessSessions.Add($processSessionId)
        $processId = [int]$session.pid
        $hwnd = [string]$session.hwnd
        $startedAt = [string]$session.started_at
        if ($processId -le 0 -or $hwnd -cnotmatch '^0x[0-9a-fA-F]+$' -or [string]::IsNullOrWhiteSpace($startedAt)) {
            Fail "run[$runIndex] has an incomplete process identity"
        }
        $processIdentity = "$processId|$hwnd|$startedAt"
        if (-not $processIdentities.Add($processIdentity)) { Fail "process identity is reused across the three runs: $processIdentity" }
        $runProcessIdentities.Add($processIdentity)
        foreach ($field in 'stdout','stderr','plan_path','phase_summary_path') {
            Add-RunOwnedEvidencePath $evidencePaths $root ([string]$session.$field) "run[$runIndex] session.$field"
        }
    }

    foreach ($relative in 'app/evidence/summary.json','app/evidence/events.ndjson','app/evidence/summary.ndjson') {
        Add-RunOwnedEvidencePath $evidencePaths $root $relative "run[$runIndex] evidence journal"
    }
    foreach ($artifact in @($summary.artifacts)) {
        Add-RunOwnedEvidencePath $evidencePaths $root ([string]$artifact.path) "run[$runIndex] artifact.path"
    }
    foreach ($scenario in @($summary.scenarios)) {
        foreach ($path in @($scenario.screenshot_paths)) {
            Add-RunOwnedEvidencePath $evidencePaths $root ([string]$path) "run[$runIndex] screenshot path"
        }
        foreach ($path in @($scenario.bounds_paths)) {
            Add-RunOwnedEvidencePath $evidencePaths $root ([string]$path) "run[$runIndex] bounds path"
        }
        foreach ($path in @($scenario.database.evidence_paths)) {
            Add-RunOwnedEvidencePath $evidencePaths $root ([string]$path) "run[$runIndex] database evidence path"
        }
    }
    foreach ($path in $evidencePaths) {
        if (-not $globalEvidencePaths.Add($path)) { Fail "evidence file path is reused across runs: $path" }
    }

    $sortedSessionIds = @($runProcessSessions | Sort-Object)
    $sortedProcessIdentities = @($runProcessIdentities | Sort-Object)
    $sortedEvidencePaths = @($evidencePaths | Sort-Object)
    $runRows.Add([ordered]@{
        ordinal = $runIndex + 1
        run_root = $root
        run_id = $runId
        source_head = $head
        validator_sha256 = $validatorSha256
        validation_status = [string]$validation.status
        process_session_count = $sortedSessionIds.Count
        process_session_set_sha256 = Sha256-Text ($sortedSessionIds -join "`n")
        process_identity_count = $sortedProcessIdentities.Count
        process_identity_set_sha256 = Sha256-Text ($sortedProcessIdentities -join "`n")
        evidence_file_count = $sortedEvidencePaths.Count
        evidence_path_set_sha256 = Sha256-Text ($sortedEvidencePaths -join "`n")
    })
}

if ($validatorHashes.Count -ne 1) { Fail 'the three run-owned validators do not have the same hash.' }

[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
foreach ($root in $resolvedRoots) {
    if (Inside-Or-Equal $outputRoot $root) { Fail "resolved OutputDirectory entered a run root: $outputRoot" }
}
$createdAt = [DateTimeOffset]::UtcNow
$fileName = 'p3-automated-run-set-{0}-{1}-{2}.json' -f $expectedHead.Substring(0, 12),
    $createdAt.ToString('yyyyMMddTHHmmssfffZ', [Globalization.CultureInfo]::InvariantCulture),
    [guid]::NewGuid().ToString('N')
$summaryPath = Full (Join-Path $outputRoot $fileName)
$payload = [ordered]@{
    schema = 'pixel-tart-p3-automated-run-set/v1'
    status = 'passed'
    validation_mode = 'automated'
    created_at = $createdAt.ToString('O')
    source_head = $expectedHead
    run_count = 3
    all_run_roots_unique = $true
    all_run_ids_unique = $true
    all_process_identities_unique = $true
    all_process_sessions_unique = $true
    evidence_paths_disjoint = $true
    process_identity_count = $processIdentities.Count
    process_session_count = $processSessions.Count
    evidence_file_path_count = $globalEvidencePaths.Count
    validator_source = 'runner/acceptance-inputs/Test-P3AssetLibraryAutomatedEvidence.ps1'
    validator_sha256 = @($validatorHashes)[0]
    output_directory = $outputRoot
    summary_path = $summaryPath
    runs = @($runRows)
}
$json = $payload | ConvertTo-Json -Depth 8
$temporaryPath = "$summaryPath.tmp-$([guid]::NewGuid().ToString('N'))"
try {
    [IO.File]::WriteAllText($temporaryPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $summaryPath -ErrorAction Stop
}
finally {
    if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) { Remove-Item -LiteralPath $temporaryPath -Force }
}
$payload | ConvertTo-Json -Depth 8
