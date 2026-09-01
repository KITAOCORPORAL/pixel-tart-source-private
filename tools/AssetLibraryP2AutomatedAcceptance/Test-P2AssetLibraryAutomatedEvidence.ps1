[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RunRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$Message) { throw "P2 automated evidence rejected: $Message" }
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') }
function Inside([string]$Path, [string]$Parent) {
    $child = Full $Path; $root = Full $Parent
    return $child.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
function Require-File([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "$Name is missing: $Path" }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail "$Name is a reparse point." }
    return $item
}
function Read-Json([string]$Path, [string]$Name) {
    [void](Require-File $Path $Name)
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Fail "$Name is not valid JSON: $($_.Exception.Message)" }
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Require-Equal($Actual, $Expected, [string]$Name) {
    if (-not [object]::Equals($Actual, $Expected)) { Fail "$Name differs (actual='$Actual', expected='$Expected')." }
}
function Require-String($Value, [string]$Name, [string]$Pattern = '^.+$') {
    if ($Value -isnot [string] -or $Value -cnotmatch $Pattern) { Fail "$Name is missing or invalid." }
}
function Require-ZeroFields($Object, [string[]]$Names, [string]$Owner) {
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -eq $property -or [int64]$property.Value -ne 0) { Fail "$Owner.$name must exist and equal zero." }
    }
}
function Tree-Fingerprint([string]$Root) {
    $rows = Get-ChildItem -LiteralPath $Root -Recurse -Force -File | Sort-Object FullName | ForEach-Object {
        "{0}|{1}|{2}" -f ([IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\','/')), $_.Length, (Hash $_.FullName)
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

if (-not [IO.Path]::IsPathFullyQualified($RunRoot)) { Fail 'RunRoot must be absolute.' }
$root = Full $RunRoot
if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail 'RunRoot does not exist.' }
if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { Fail 'RunRoot may not be a reparse point.' }
$fingerprintBefore = Tree-Fingerprint $root
$contractPath = Join-Path $PSScriptRoot 'automated-acceptance-contract.json'
$contract = Read-Json $contractPath 'contract'
Require-Equal $contract.schema 'pixel-tart-asset-library-p2-automated-acceptance-contract/v1' 'contract.schema'
Require-Equal $contract.validation_mode 'automated' 'contract.validation_mode'
Require-Equal $contract.owner_manual_ux_smoke 'waived' 'contract.owner_manual_ux_smoke'
Require-Equal ([bool]$contract.manual_evidence_claimed) $false 'contract.manual_evidence_claimed'

$expectedScenarios = @($contract.required_scenario_order | ForEach-Object { [string]$_ })
if ($expectedScenarios.Count -ne 10 -or ($expectedScenarios -join '|') -cne 'fixture-integrity/v1|organization-browser/v1|smart-tag-query/v1|four-views-query-sort/v1|selection-large/v1|metadata-drag-command/v1|inspector-states/v1|resilience-states/v1|restart-persistence/v1|layout-dpi-performance/v1') { Fail 'contract scenario order is not the fixed P2 order.' }
Require-Equal ([int]$contract.fixture.total_count) 512 'fixture total'
Require-Equal ([int]$contract.fixture.active_count) 500 'fixture active'
Require-Equal ([int]$contract.fixture.archived_count) 12 'fixture archived'

$negativeGuardMap = [ordered]@{
    'missing-screenshot'='required screenshot artifact and PNG signature'; 'mutated-hash'='every file hash is recomputed';
    'wrong-scenario-order'='exact fixed scenario array'; 'fixture-count-mismatch'='fixture and DB 512/500/12';
    'fixture-path-escape'='every fixture path is beneath run root'; 'folder-cycle-accepted'='organization snapshot and product contract check';
    'duplicate-automation-id'='bounds identity uniqueness'; 'smart-result-mismatch'='smart query positive count';
    'query-plan-divergence'='query snapshot identity and counts'; 'stale-cancelled-query'='resilience one-retry evidence';
    'view-state-lost'='four views plus restart persistence'; 'virtualization-realizes-all'='virtualizing state and realized-count bound';
    'sort-unstable'='sort snapshot and threshold'; 'selection-truncated'='100 distinct selection IDs';
    'invalid-drop-accepted'='command preview/result evidence'; 'undo-mismatch'='undo/redo command evidence';
    'prohibited-command-present'='safety and no-delete contract'; 'inspector-mode-mismatch'='query/single/multiple snapshot';
    'restart-identity-reused'='restart PID/HWND/process-session differ'; 'dpi-overflow'='four bounds snapshots with no overflow';
    'performance-threshold-exceeded'='all fixed duration thresholds'; 'ui-block-exceeded'='100ms dispatcher threshold';
    'user-source-write'='safety zero counters'; 'eagle-write'='Eagle read/write zero counters';
    'residual-process'='runner process cleanup'; 'database-not-v6'='read-only SQLite schema audit';
    'cross-run-splice'='run id/root/head on every record'; 'runner-session-splice'='11 unique runner sessions';
    'process-session-splice'='event/artifact process-session ownership'; 'binary-hash-mismatch'='run-owned binary tree and live hashes';
    'input-tree-mutated'='validator before/after tree fingerprint'
}
$negativeNames = @($contract.required_negative_fixtures | ForEach-Object { [string]$_ })
if ((@($negativeGuardMap.Keys) -join '|') -cne ($negativeNames -join '|')) { Fail 'negative fixture list has no exact validator guard map.' }

$manifest = Read-Json (Join-Path $root 'run-manifest.json') 'run manifest'
Require-Equal $manifest.schema_version 'pixel-tart-p2-automated-run/v1' 'manifest schema'
Require-Equal $manifest.validation_mode 'automated' 'manifest validation mode'
Require-Equal $manifest.owner_manual_ux_smoke 'waived' 'manifest owner smoke'
Require-Equal ([bool]$manifest.manual_evidence_claimed) $false 'manifest manual claim'
Require-Equal $manifest.automated_capture_status 'captured' 'manifest status'
Require-Equal (Full $manifest.run_root) $root 'manifest run root'
Require-String $manifest.run_id 'run id' '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$'
Require-String $manifest.source_head 'source head' '^[0-9a-f]{40}$'
Require-Equal $manifest.branch 'feature/asset-library-eagle-parity-p2' 'branch'

$fixture = $manifest.fixture
Require-Equal $fixture.schema 'pixel-tart-p2-synthetic-fixture/v1' 'fixture schema'
Require-Equal ([int]$fixture.total_count) 512 'fixture total count'
Require-Equal ([int]$fixture.active_count) 500 'fixture active count'
Require-Equal ([int]$fixture.archived_count) 12 'fixture archived count'
Require-Equal ([int]$fixture.user_source_read_count) 0 'fixture user source read count'
Require-Equal ([int]$fixture.user_source_write_count) 0 'fixture user source write count'
$fixtureDb = Full $fixture.database_path
if (-not (Inside $fixtureDb $root)) { Fail 'fixture database escapes the run root.' }
[void](Require-File $fixtureDb 'fixture database')
Require-Equal (Hash $fixtureDb) $fixture.database_sha256 'fixture database hash'

$sessions = @($manifest.sessions)
Require-Equal $sessions.Count 11 'runner session count'
$expectedSessionScenarios = @($expectedScenarios + 'restart-persistence/v1')
$seenSessions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
for ($index = 0; $index -lt $sessions.Count; $index++) {
    $session = $sessions[$index]
    Require-Equal $session.scenario_id $expectedSessionScenarios[$index] "session[$index] scenario"
    Require-Equal ([int]$session.exit_code) 0 "session[$index] exit"
    Require-String $session.process_session_id "session[$index] process session" '^[0-9a-f]{32}$'
    if (-not $seenSessions.Add([string]$session.process_session_id)) { Fail 'runner process session id is reused.' }
    Require-Equal $session.source_head $manifest.source_head "session[$index] head"
    foreach ($field in 'stdout','stderr','scenario_root','executable_path','application_path','asset_module_path') {
        $value = Full $session.$field
        if (-not (Inside $value $root)) { Fail "session[$index].$field escapes the run root." }
    }
    foreach ($pair in @(@('stdout','stdout_sha256'),@('stderr','stderr_sha256'),@('executable_path','executable_sha256'),@('application_path','application_sha256'),@('asset_module_path','asset_module_sha256'))) {
        [void](Require-File $session.($pair[0]) "session[$index] $($pair[0])")
        Require-Equal (Hash $session.($pair[0])) $session.($pair[1]) "session[$index] $($pair[1])"
    }
}

$build = Read-Json (Join-Path $root 'build-manifest.json') 'build manifest'
Require-Equal $build.schema_version 'pixel-tart-p2-automated-build/v1' 'build schema'
Require-Equal $build.source_head $manifest.source_head 'build source head'
foreach ($pair in @(@('executable_path','executable_sha256'),@('application_path','application_sha256'),@('asset_module_path','asset_module_sha256'))) {
    $path = Full $build.($pair[0]); if (-not (Inside $path (Join-Path $root 'binaries'))) { Fail "build $($pair[0]) is not run-owned." }
    [void](Require-File $path "build $($pair[0])"); Require-Equal (Hash $path) $build.($pair[1]) "build $($pair[1])"
}

$summary = Read-Json (Join-Path $root 'app/evidence/summary.json') 'application summary'
Require-Equal $summary.schema 'pixel-tart-p2-automated-summary/v1' 'summary schema'
Require-Equal $summary.status 'completed' 'summary status'
Require-Equal $summary.run_id $manifest.run_id 'summary run id'
Require-Equal $summary.source_head $manifest.source_head 'summary head'
$scenarios = @($summary.scenarios)
Require-Equal $scenarios.Count 10 'summary scenario count'
for ($index = 0; $index -lt 10; $index++) {
    $scenario = $scenarios[$index]
    Require-Equal $scenario.id $expectedScenarios[$index] "scenario[$index] id"
    Require-Equal ([int]$scenario.sequence) ($index + 1) "scenario[$index] sequence"
    Require-Equal $scenario.status 'passed' "scenario[$index] status"
    Require-String $scenario.primary_process_session_id "scenario[$index] primary session" '^[0-9a-f]{32}$'
    $scenarioRoot = Full $scenario.scenario_root; if (-not (Inside $scenarioRoot $root)) { Fail "scenario[$index] root escapes run root." }
    Require-Equal ([int]$scenario.database.schema_version) 6 "scenario[$index] DB schema"
    Require-Equal ([int]$scenario.database.asset_count) 512 "scenario[$index] DB total"
    Require-Equal ([int]$scenario.database.active_asset_count) 500 "scenario[$index] DB active"
    Require-Equal ([int]$scenario.database.archived_asset_count) 12 "scenario[$index] DB archived"
    Require-Equal ([bool]$scenario.database.real_repository) $true "scenario[$index] real repository"
    if (@($scenario.screenshot_paths).Count -lt 1 -or @($scenario.bounds_paths).Count -lt 1) { Fail "scenario[$index] lacks screenshot/bounds evidence." }
}
$restart = $scenarios[8]
Require-String $restart.restart_process_session_id 'restart process session' '^[0-9a-f]{32}$'
if ($restart.restart_process_session_id -ceq $restart.primary_process_session_id -or [int]$restart.restart_pid -eq [int]$restart.pid -or [int64]$restart.restart_hwnd_numeric -eq [int64]$restart.hwnd_numeric) { Fail 'restart reused primary process identity.' }

$eventsPath = Join-Path $root 'app/evidence/events.ndjson'
[void](Require-File $eventsPath 'event journal')
$eventLines = @(Get-Content -LiteralPath $eventsPath -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($eventLines.Count -lt 22) { Fail 'event journal is unexpectedly short.' }
$previous = '0' * 64; $eventSessions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in $eventLines) {
    try { $event = $line | ConvertFrom-Json } catch { Fail 'event journal contains invalid JSON.' }
    Require-Equal $event.schema 'pixel-tart-p2-automated-event/v1' 'event schema'
    Require-Equal $event.run_id $manifest.run_id 'event run id'; Require-Equal $event.source_head $manifest.source_head 'event head'
    Require-Equal $event.previous_event_hash $previous 'event hash link'; Require-String $event.event_hash 'event hash' '^[0-9a-f]{64}$'
    Require-Equal $event.event_hash $event.record_sha256 'event record hash alias'
    [void]$eventSessions.Add([string]$event.process_session_id); $previous = [string]$event.event_hash
}
if ($eventSessions.Count -ne 11) { Fail 'event journal is not owned by exactly 11 process sessions.' }

$artifacts = @($summary.artifacts)
if ($artifacts.Count -lt 30) { Fail 'artifact set is unexpectedly small.' }
$kinds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$artifactPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$artifactJson = @{}
foreach ($artifact in $artifacts) {
    $relative = [string]$artifact.path; Require-String $relative 'artifact path'
    $path = Full (Join-Path $root $relative); if (-not (Inside $path $root)) { Fail 'artifact escapes run root.' }
    if (-not $artifactPaths.Add($path)) { Fail 'artifact path is reused.' }
    [void](Require-File $path 'artifact'); Require-Equal (Hash $path) $artifact.sha256 'artifact hash'
    Require-Equal $artifact.run_id $manifest.run_id 'artifact run id'; Require-Equal $artifact.source_head $manifest.source_head 'artifact head'
    Require-String $artifact.process_session_id 'artifact process session' '^[0-9a-f]{32}$'
    $kind = [string]$artifact.kind
    $category = if ($kind -match 'bounds') {'bounds'} elseif ($kind -match 'query') {'queries'} elseif ($kind -match 'selection') {'selections'} elseif ($kind -match 'view') {'views'} elseif ($kind -match 'command') {'commands'} elseif ($kind -match 'inspector') {'inspectors'} elseif ($kind -match 'performance') {'performance'} elseif ($kind -match 'database') {'databases'} else {'screenshots'}
    [void]$kinds.Add($category)
    if ($category -eq 'screenshots') {
        $bytes = [IO.File]::ReadAllBytes($path); if ($bytes.Length -lt 8 -or $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4e -or $bytes[3] -ne 0x47) { Fail 'screenshot is not a nonempty PNG.' }
    } elseif ($category -ne 'databases') { $artifactJson[$relative] = Read-Json $path "artifact $relative" }
}
foreach ($required in @($contract.required_evidence_kinds)) { if (-not $kinds.Contains([string]$required)) { Fail "required artifact category '$required' is absent." } }

$boundsPayloads = @($artifactJson.GetEnumerator() | Where-Object { $_.Key -match '/bounds/' } | ForEach-Object { $_.Value.payload })
foreach ($bounds in $boundsPayloads) {
    if ([bool]$bounds.has_overflow -or [bool]$bounds.real_display_settings_changed) { Fail 'bounds evidence reports overflow or a real display write.' }
    $ids = @($bounds.elements | ForEach-Object { [string]$_.Identity } | Where-Object { $_ })
    if (@($ids | Group-Object | Where-Object Count -gt 1).Count -gt 0) { Fail 'bounds evidence contains duplicate automation identities.' }
}
$dpiBounds = @($boundsPayloads | Where-Object { [int]$_.viewport.scale_percent -in @(100,125,150,175) })
foreach ($dpi in 100,125,150,175) { if (@($dpiBounds | Where-Object { [int]$_.viewport.scale_percent -eq $dpi }).Count -lt 1) { Fail "DPI $dpi evidence is absent." } }

$selectionEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'selection-large/v1' }) | Select-Object -First 1
if ($null -eq $selectionEvidence -or [int]$selectionEvidence.payload.count -ne 100 -or @($selectionEvidence.payload.asset_ids | Select-Object -Unique).Count -ne 100 -or [double]$selectionEvidence.payload.elapsed_ms -gt 250) { Fail '100-item selection evidence failed.' }
$organizationEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'organization-browser/v1' -and $_.payload.before }) | Select-Object -First 1
if ($null -eq $organizationEvidence -or -not [bool]$organizationEvidence.payload.before.FolderTreeAcyclic -or [int]$organizationEvidence.payload.before.FolderNodeCount -lt 3) { Fail 'organization tree cycle/count evidence failed.' }
$smartEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'smart-tag-query/v1' -and $_.payload.tag }) | Select-Object -First 1
if ($null -eq $smartEvidence -or [int]$smartEvidence.payload.tag.QueryTotalCount -ne 250 -or [int]$smartEvidence.payload.smart.QueryTotalCount -ne 166) { Fail 'deterministic tag/smart query result counts differ.' }
$viewEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'four-views-query-sort/v1' -and $_.payload.views }) | Select-Object -First 1
$viewNames = @($viewEvidence.payload.views | ForEach-Object { [string]$_.mode })
if (($viewNames -join '|') -cne 'Grid|Masonry|Justified|List') { Fail 'four-view evidence differs.' }
foreach ($row in @($viewEvidence.payload.views)) {
    if (-not [bool]$row.snapshot.IsVirtualizing -or $row.snapshot.VirtualizationMode -cne 'Recycling' -or
        [int]$row.snapshot.RealizedItemCount -ge [int]$row.snapshot.QueryTotalCount) { Fail 'view virtualization realized the complete query or left recycling mode.' }
}
$commandEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'metadata-drag-command/v1' -and $_.payload.drop }) | Select-Object -First 1
if ($null -eq $commandEvidence -or -not [bool]$commandEvidence.payload.drop.CanUndo -or [double]$commandEvidence.payload.elapsed_ms -gt 750) { Fail 'metadata drag command evidence failed.' }
$inspectorEvidence = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'inspector-states/v1' }) | Select-Object -First 1
if ($inspectorEvidence.payload.query.InspectorMode -cne 'query' -or $inspectorEvidence.payload.single.InspectorMode -cne 'single' -or $inspectorEvidence.payload.multiple.InspectorMode -cne 'multiple') { Fail 'inspector states differ.' }
$performance = @($artifactJson.Values | Where-Object { $_.scenario_id -eq 'layout-dpi-performance/v1' -and $_.payload.first_screen_ms }) | Select-Object -First 1
if ($null -eq $performance -or [double]$performance.payload.first_screen_ms -gt 1500 -or [double]$performance.payload.ui_block_ms -gt 100) { Fail 'layout/UI performance threshold failed.' }

$audit = Read-Json (Join-Path $root 'runner/database-consistency-audit.json') 'pre-cleanup database audit'
Require-Equal $audit.schema 'pixel-tart-p2-pre-cleanup-database-audit/v1' 'database audit schema'
Require-Equal $audit.status 'passed' 'database audit status'; Require-Equal ([int]$audit.scenario_count) 10 'database audit scenario count'
foreach ($row in @($audit.scenarios)) {
    foreach ($side in 'active','evidence') {
        Require-Equal ([int]$row.$side.schema_version) 6 "audit $side schema"
        Require-Equal ([int]$row.$side.asset_count) 512 "audit $side total"
        Require-Equal ([int]$row.$side.active_count) 500 "audit $side active"
        Require-Equal ([int]$row.$side.archived_count) 12 "audit $side archived"
    }
}

Require-ZeroFields $manifest.safety @($contract.safety_zero_fields) 'manifest.safety'
Require-Equal ([bool]$manifest.process_cleanup_verified) $true 'process cleanup verified'
$cleanup = $manifest.process_cleanup
foreach ($field in 'devpreview_get_process_count_after','devpreview_cim_count_after','dotnet_residual_pid_count','db_sidecar_count_after','runtime_database_count_after','environment_residual_count') { Require-Equal ([int]$cleanup.$field) 0 "cleanup.$field" }
Require-Equal ([bool]$cleanup.display_settings_unchanged) $true 'display settings unchanged'

$fingerprintAfter = Tree-Fingerprint $root
Require-Equal $fingerprintAfter $fingerprintBefore 'input tree fingerprint'
[pscustomobject]@{
    schema = 'pixel-tart-p2-automated-validation-result/v1'; status = 'passed'; validation_mode = 'automated';
    run_root = $root; run_id = $manifest.run_id; source_head = $manifest.source_head;
    scenario_count = 10; runner_session_count = 11; artifact_count = $artifacts.Count;
    fixture = '512 total / 500 active / 12 archived'; input_tree_unchanged = $true
} | ConvertTo-Json -Depth 5
