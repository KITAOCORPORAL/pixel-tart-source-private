[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunRoot,
    [switch]$SkipNegativeProofs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$Message) { throw "P3 automated evidence rejected: $Message" }
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') }
function Test-AbsolutePath([AllowEmptyString()][string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path -notmatch '^(?:[A-Za-z]:[\\/]|\\\\)') { return $false }
    try { [void][IO.Path]::GetFullPath($Path); return $true } catch { return $false }
}
function Relative-Path([string]$Root, [string]$Path) {
    $rootFull = Full $Root
    $pathFull = Full $Path
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the fingerprint root: $Path"
    }
    return $pathFull.Substring($prefix.Length).Replace('\', '/')
}
function Bytes-ToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { $_.ToString('x2', [Globalization.CultureInfo]::InvariantCulture) })
}
function Sha256Bytes([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return $algorithm.ComputeHash($Bytes) }
    finally { $algorithm.Dispose() }
}
function Sha256Text([AllowEmptyString()][string]$Text) {
    return (Bytes-ToHex (Sha256Bytes ([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant()
}
function Get-JournalCanonicalText([string]$Line, [ValidateSet('event','summary')][string]$Kind) {
    # The production controller serializes an insertion-ordered dictionary once
    # without the two terminal hash aliases, hashes those exact UTF-8 bytes, then
    # serializes the same dictionary after appending the aliases.  Recovering the
    # prefix from the final line avoids PowerShell JSON date/escaping differences
    # and therefore reproduces the producer's byte contract exactly on PS 5.1.
    $primary = if ($Kind -ceq 'event') { 'event_hash' } else { 'summary_hash' }
    $pattern = '^(?<prefix>\{.*),"' + [regex]::Escape($primary) +
        '":"(?<primary>[0-9a-f]{64})","record_sha256":"(?<alias>[0-9a-f]{64})"\}$'
    $match = [regex]::Match($Line, $pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) { Fail "$Kind journal record does not use the production terminal hash layout." }
    if ($match.Groups['primary'].Value -cne $match.Groups['alias'].Value) {
        Fail "$Kind journal hash aliases differ."
    }
    return [pscustomobject]@{
        canonical = $match.Groups['prefix'].Value + '}'
        claimed_hash = $match.Groups['primary'].Value
    }
}
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
function Hash([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { return (($algorithm.ComputeHash($stream) | ForEach-Object { $_.ToString('x2', [Globalization.CultureInfo]::InvariantCulture) }) -join '').ToLowerInvariant() }
        finally { $algorithm.Dispose() }
    }
    finally { $stream.Dispose() }
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
function Invoke-ReadonlyFixtureAudit([string]$DatabasePath, [string]$Variant) {
    $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) { Fail 'python.exe is required for the independent fixture content audit.' }
    $python = [IO.Path]::GetFullPath($pythonCommand.Source)
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { Fail "python.exe is not a file: $python" }
    $auditCode = @'
import hashlib, json, pathlib, re, sqlite3, sys
db = pathlib.Path(sys.argv[2]).resolve()
variant = sys.argv[3]
if variant not in ("current-v7", "legacy-v6"):
    raise RuntimeError(f"unsupported fixture variant: {variant}")
if not db.is_file():
    raise RuntimeError(f"fixture database is missing: {db}")
before = hashlib.sha256(db.read_bytes()).hexdigest()
uri = db.as_uri() + "?mode=ro&immutable=1"
connection = sqlite3.connect(uri, uri=True)
try:
    connection.execute("PRAGMA query_only=ON")
    quick_check = connection.execute("PRAGMA quick_check").fetchone()[0]
    schema_version = connection.execute("SELECT MAX(Version) FROM AssetLibrarySchemaInfo").fetchone()[0]
    rows = connection.execute("SELECT AssetId,SourcePath,DisplayName,ContentHash,IsMissing,IsArchived FROM AssetItems ORDER BY SourcePath").fetchall()
    expected = (10128, 10000, 128, 7) if variant == "current-v7" else (64, 60, 4, 6)
    if len(rows) != expected[0]:
        raise RuntimeError(f"fixture asset row count mismatch: {len(rows)}")
    active_count = sum(1 for row in rows if row[5] == 0)
    archived_count = sum(1 for row in rows if row[5] == 1)
    if (active_count, archived_count) != expected[1:3]:
        raise RuntimeError(f"fixture archive split mismatch: active={active_count}, archived={archived_count}")
    source_paths = [str(pathlib.Path(row[1]).resolve()) for row in rows]
    source_paths_inside_fixture_count = 0
    for source_path in source_paths:
        try:
            pathlib.Path(source_path).relative_to(db.parent)
            source_paths_inside_fixture_count += 1
        except ValueError:
            pass
    source_path_sha256 = hashlib.sha256("\n".join(source_paths).encode("utf-8")).hexdigest()
    han = re.compile(r"[\u3400-\u9fff]")
    display_name_count = sum(1 for row in rows if isinstance(row[2], str) and han.search(row[2]))
    if display_name_count != expected[0]:
        raise RuntimeError(f"fixture display names are not all Chinese: {display_name_count}/{expected[0]}")
    hash_pattern = re.compile(r"^[0-9a-f]{64}$")
    content_hash_count = 0
    for row in rows:
        value = row[3]
        if not isinstance(value, str) or not hash_pattern.fullmatch(value):
            raise RuntimeError(f"fixture content hash is missing or malformed for {row[0]}")
        content_hash_count += 1
        if variant == "current-v7":
            match = re.search(r"P3_(\d{5})\.[^.]+$", str(row[1]), re.IGNORECASE)
            if match is None:
                raise RuntimeError(f"fixture source path has no deterministic index for {row[0]}")
            index = int(match.group(1))
            deterministic = hashlib.sha256(f"pixel-tart-p3-source-{index:05d}".encode("ascii")).hexdigest()
        else:
            match = re.search(r"legacy-(\d{3})\.jpg$", str(row[1]), re.IGNORECASE)
            if match is None:
                raise RuntimeError(f"legacy fixture source path has no deterministic index for {row[0]}")
            index = int(match.group(1))
            deterministic = hashlib.sha256(f"legacy-{index:03d}".encode("ascii")).hexdigest()
        if value != deterministic:
            raise RuntimeError(f"fixture content hash is not deterministic for index {index}")
    missing_count = sum(1 for row in rows if row[4] == 1)
    expected_missing = 512 if variant == "current-v7" else 0
    if missing_count != expected_missing:
        raise RuntimeError(f"fixture missing count mismatch: {missing_count}")
    feature_rows = connection.execute("SELECT AssetId,AnalysisVersion,SourceContentHash,Outcome FROM AssetVisualFeatures WHERE AnalysisVersion='visual-analysis-v2'").fetchall()
    valid_count = sum(1 for row in feature_rows if row[3] == 'Succeeded')
    failed_count = sum(1 for row in feature_rows if row[3] == 'Failed')
    not_analyzed_count = expected[0] - len(feature_rows)
    expected_visual = (3072, 1024, 6032) if variant == "current-v7" else (0, 0, 64)
    if (valid_count, failed_count, not_analyzed_count) != expected_visual:
        raise RuntimeError(f"fixture visual state mismatch: valid={valid_count}, failed={failed_count}, not_analyzed={not_analyzed_count}")
    hashes = {row[0]: row[3] for row in rows}
    for asset_id, version, source_hash, outcome in feature_rows:
        if outcome not in ('Succeeded', 'Failed') or source_hash != hashes.get(asset_id):
            raise RuntimeError(f"fixture visual row is not tied to its deterministic source hash: {asset_id}")
    result = {
        "quick_check": quick_check,
        "schema_version": schema_version,
        "asset_count": len(rows),
        "active_count": active_count,
        "archived_count": archived_count,
        "source_path_count": len(source_paths),
        "source_paths_inside_fixture_count": source_paths_inside_fixture_count,
        "source_paths_outside_fixture_count": len(source_paths) - source_paths_inside_fixture_count,
        "source_path_sha256": source_path_sha256,
        "display_name_count": display_name_count,
        "content_hash_count": content_hash_count,
        "missing_count": missing_count,
        "visual_feature_rows": len(feature_rows),
        "visual_valid_count": valid_count,
        "visual_failed_count": failed_count,
        "visual_not_analyzed_count": not_analyzed_count
    }
finally:
    connection.close()
after = hashlib.sha256(db.read_bytes()).hexdigest()
if before != after:
    raise RuntimeError(f"read-only fixture audit changed database: {db}")
print(json.dumps(result, ensure_ascii=False, separators=(",", ":")))
'@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($auditCode))
    $bootstrap = 'import base64,sys;exec(base64.b64decode(sys.argv[1]))'
    $arguments = @('-I', '-c', $bootstrap, $encoded, [IO.Path]::GetFullPath($DatabasePath), $Variant)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $python
    $startInfo.Arguments = ($arguments | ForEach-Object { Quote-ProcessArgument ([string]$_) }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $startInfo
    if (-not $process.Start()) { Fail 'Could not start the independent fixture audit.' }
    $stdout = $process.StandardOutput.ReadToEnd(); $stderr = $process.StandardError.ReadToEnd(); $process.WaitForExit()
    if ($process.ExitCode -ne 0) { Fail "Independent fixture content audit failed: $($stderr.Trim())" }
    try { return $stdout | ConvertFrom-Json } catch { Fail "Independent fixture content audit returned invalid JSON: $($_.Exception.Message)" }
}
function Require-Equal($Actual, $Expected, [string]$Name) {
    if (-not [object]::Equals($Actual, $Expected)) { Fail "$Name differs (actual='$Actual', expected='$Expected')." }
}
function Require-String($Value, [string]$Name, [string]$Pattern = '^.+$') {
    if ($Value -isnot [string] -or $Value -cnotmatch $Pattern) { Fail "$Name is missing or invalid." }
}
function Property-Value($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}
function Require-ZeroFields($Object, [string[]]$Names, [string]$Owner) {
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -eq $property -or [int64]$property.Value -ne 0) { Fail "$Owner.$name must exist and equal zero." }
    }
}
function Tree-Fingerprint([string]$Root) {
    $rows = Get-ChildItem -LiteralPath $Root -Recurse -Force -File | Sort-Object FullName | ForEach-Object {
        "{0}|{1}|{2}" -f (Relative-Path $Root $_.FullName), $_.Length, (Hash $_.FullName)
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    return (Bytes-ToHex (Sha256Bytes $bytes)).ToLowerInvariant()
}
function Canonical-FileTreeHash([object[]]$Rows) {
    $lineList = [Collections.Generic.List[string]]::new()
    foreach ($row in $Rows) {
        $lineList.Add(("{0}|{1}|{2}" -f ([string]$row.path), ([int64]$row.byte_length), ([string]$row.sha256)))
    }
    $lines = $lineList.ToArray()
    [Array]::Sort($lines, [StringComparer]::Ordinal)
    return Sha256Text ($lines -join "`n")
}
function Assert-CanonicalRunFilePath([string]$RelativePath, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains(':') -or $RelativePath.Contains('\') -or
        $RelativePath -match '(^|/)\.\.?(/|$)') {
        Fail "$Name is not a canonical run-relative file path: '$RelativePath'."
    }
}
function Assert-SealedRun([string]$RunRoot, $Contract) {
    $sealRelativePath = [string]$Contract.run_seal_file
    Require-Equal $Contract.acceptance_input_snapshot_schema 'pixel-tart-p3-acceptance-input-snapshot/v1' 'contract acceptance input snapshot schema'
    Require-Equal $Contract.run_seal_schema 'pixel-tart-p3-run-seal/v1' 'contract run seal schema'
    Require-Equal $sealRelativePath 'runner/run-seal.json' 'contract run seal file'
    Require-Equal ([bool]$Contract.run_seal_inventory_excludes_seal_file) $true 'contract seal inventory exclusion'
    Require-Equal ([bool]$Contract.run_seal_requires_read_only) $true 'contract seal read-only requirement'

    $requiredInputFiles = @($Contract.required_acceptance_input_files | ForEach-Object { [string]$_ })
    $fixedInputFiles = @(
        'Invoke-P3AssetLibraryAutomatedAcceptance.ps1',
        'Test-P3AssetLibraryAutomatedEvidence.ps1',
        'Test-P3AssetLibraryAutomatedRunSet.ps1',
        'New-P3SyntheticFixture.py',
        'Invoke-P3NegativeEvidenceProofs.py',
        'automated-acceptance-contract.json',
        'README.md')
    if ((@($requiredInputFiles | Sort-Object) -join '|') -cne (@($fixedInputFiles | Sort-Object) -join '|')) {
        Fail 'contract acceptance input file set differs.'
    }

    $sealPath = Full (Join-Path $RunRoot $sealRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (-not (Inside $sealPath $RunRoot)) { Fail 'run seal path escapes the run root.' }
    $sealItem = Require-File $sealPath 'run seal'
    if (($sealItem.Attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) { Fail 'run seal is not read-only.' }
    $seal = Read-Json $sealPath 'run seal'
    Require-Equal $seal.schema 'pixel-tart-p3-run-seal/v1' 'run seal schema'
    Require-Equal (Full $seal.run_root) $RunRoot 'run seal root'
    Require-Equal $seal.seal_file $sealRelativePath 'run seal file identity'
    Require-Equal ([bool]$seal.inventory_excludes_seal_file) $true 'run seal inventory exclusion'
    Require-Equal ([bool]$seal.read_only_required) $true 'run seal read-only requirement'
    Require-String $seal.run_id 'run seal run id' '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$'
    Require-String $seal.source_head 'run seal source head' '^[0-9a-f]{40}$'
    Require-String $seal.tree_sha256 'run seal tree hash' '^[0-9a-f]{64}$'

    $manifest = Read-Json (Join-Path $RunRoot 'run-manifest.json') 'sealed run manifest'
    Require-Equal $seal.run_id $manifest.run_id 'run seal manifest run id'
    Require-Equal $seal.source_head $manifest.source_head 'run seal manifest source head'
    Require-Equal $manifest.run_seal.schema $seal.schema 'manifest run seal schema'
    Require-Equal $manifest.run_seal.path $sealRelativePath 'manifest run seal path'
    Require-Equal ([bool]$manifest.run_seal.inventory_excludes_seal_file) $true 'manifest run seal exclusion'
    Require-Equal ([bool]$manifest.run_seal.read_only_required) $true 'manifest run seal read-only requirement'

    $inputSnapshot = $manifest.acceptance_inputs
    Require-Equal $inputSnapshot.schema 'pixel-tart-p3-acceptance-input-snapshot/v1' 'acceptance input snapshot schema'
    Require-Equal ([bool]$inputSnapshot.copy_verified_before_execution) $true 'acceptance input copy verification'
    Require-Equal ([bool]$inputSnapshot.files_read_only_before_execution) $true 'acceptance input initial read-only state'
    Require-String $inputSnapshot.tree_sha256 'acceptance input tree hash' '^[0-9a-f]{64}$'
    $inputDirectory = Full $inputSnapshot.directory
    $expectedInputDirectory = Full (Join-Path $RunRoot 'runner/acceptance-inputs')
    Require-Equal $inputDirectory $expectedInputDirectory 'acceptance input directory'
    if (-not (Test-Path -LiteralPath $inputDirectory -PathType Container)) { Fail 'acceptance input directory is missing.' }
    $inputRows = @($inputSnapshot.files)
    Require-Equal ([int]$inputSnapshot.file_count) $inputRows.Count 'acceptance input file count metadata'
    Require-Equal $inputRows.Count $requiredInputFiles.Count 'acceptance input file count contract'
    $inputNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $requiredInputNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($requiredInputFile in $requiredInputFiles) { [void]$requiredInputNames.Add($requiredInputFile) }
    $liveInputRows = [Collections.Generic.List[object]]::new()
    foreach ($row in $inputRows) {
        $relative = [string]$row.path
        if ($relative.Contains('/') -or $relative.Contains('\') -or -not $inputNames.Add($relative)) {
            Fail "acceptance input path is non-canonical or reused: '$relative'."
        }
        if ($relative -cnotin $requiredInputFiles) { Fail "undeclared acceptance input: '$relative'." }
        $path = Full (Join-Path $inputDirectory $relative)
        if (-not (Inside $path $inputDirectory)) { Fail 'acceptance input escaped its directory.' }
        $item = Require-File $path 'acceptance input'
        if (($item.Attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) { Fail "acceptance input is not read-only: '$relative'." }
        $hash = Hash $path
        Require-Equal ([int64]$item.Length) ([int64]$row.byte_length) "acceptance input byte length '$relative'"
        Require-Equal $hash $row.sha256 "acceptance input hash '$relative'"
        $liveInputRows.Add([ordered]@{ path = $relative; byte_length = [int64]$item.Length; sha256 = $hash })
    }
    if (-not $inputNames.SetEquals($requiredInputNames)) { Fail 'acceptance input file names differ from the contract.' }
    $actualInputFiles = @(Get-ChildItem -LiteralPath $inputDirectory -Recurse -Force -File -ErrorAction Stop)
    Require-Equal $actualInputFiles.Count $inputRows.Count 'acceptance input exact file inventory'
    Require-Equal (Canonical-FileTreeHash @($liveInputRows)) $inputSnapshot.tree_sha256 'acceptance input recomputed tree hash'
    $sealedValidatorPath = Full (Join-Path $inputDirectory 'Test-P3AssetLibraryAutomatedEvidence.ps1')
    Require-Equal (Full $PSCommandPath) $sealedValidatorPath 'executing sealed validator path'

    $inventoryRows = @($seal.files)
    Require-Equal ([int]$seal.file_count) $inventoryRows.Count 'run seal inventory count metadata'
    $inventoryPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $liveInventoryRows = [Collections.Generic.List[object]]::new()
    foreach ($row in $inventoryRows) {
        $relative = [string]$row.path
        Assert-CanonicalRunFilePath $relative 'run seal inventory path'
        if ($relative -ceq $sealRelativePath -or -not $inventoryPaths.Add($relative)) {
            Fail "run seal inventory path is excluded or reused: '$relative'."
        }
        $path = Full (Join-Path $RunRoot $relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Inside $path $RunRoot)) { Fail 'run seal inventory path escapes the run root.' }
        $item = Require-File $path 'run seal inventory file'
        if (($item.Attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) { Fail "run seal inventory file is not read-only: '$relative'." }
        $hash = Hash $path
        Require-Equal ([int64]$item.Length) ([int64]$row.byte_length) "run seal byte length '$relative'"
        Require-Equal $hash $row.sha256 "run seal file hash '$relative'"
        $liveInventoryRows.Add([ordered]@{ path = $relative; byte_length = [int64]$item.Length; sha256 = $hash })
    }
    $allEntries = @(Get-ChildItem -LiteralPath $RunRoot -Recurse -Force -ErrorAction Stop)
    foreach ($entry in $allEntries) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail 'sealed run contains a reparse point.' }
    }
    $actualInventoryPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in @($allEntries | Where-Object { -not $_.PSIsContainer })) {
        $relative = Relative-Path $RunRoot $file.FullName
        if ($relative -ceq $sealRelativePath) { continue }
        [void]$actualInventoryPaths.Add($relative)
    }
    Require-Equal $actualInventoryPaths.Count $inventoryPaths.Count 'run seal exact live inventory count'
    if (-not $actualInventoryPaths.SetEquals($inventoryPaths)) { Fail 'run seal exact file inventory differs.' }
    Require-Equal (Canonical-FileTreeHash @($liveInventoryRows)) $seal.tree_sha256 'run seal recomputed tree hash'
    return [pscustomobject]@{ manifest = $manifest; seal = $seal; acceptance_inputs = $inputSnapshot }
}
function Get-SafetyScanRules {
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
function Measure-SealedSafetyScan($Scan, [string]$RunRoot) {
    Require-Equal $Scan.schema 'pixel-tart-p3-safety-static-scan/v1' 'safety static scan schema'
    $snapshotRoot = Full $Scan.snapshot_root
    if (-not (Inside $snapshotRoot $RunRoot) -or -not (Test-Path -LiteralPath $snapshotRoot -PathType Container)) {
        Fail 'safety source snapshot is absent or outside the sealed run.'
    }
    Require-Equal (Tree-Fingerprint $snapshotRoot) $Scan.snapshot_tree_sha256 'safety source snapshot tree hash'
    $targets = @($Scan.targets)
    if ($targets.Count -ne 3) { Fail 'safety source snapshot must contain the three acceptance source seams.' }
    $targetPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($target in $targets) {
        $path = Full $target.path
        if (-not (Inside $path $snapshotRoot) -or -not $targetPaths.Add($path)) { Fail 'safety source target escaped or was reused.' }
        [void](Require-File $path 'safety source target')
        Require-Equal (Hash $path) $target.sha256 'safety source target hash'
        Require-Equal ([int64](Get-Item -LiteralPath $path -Force).Length) ([int64]$target.byte_length) 'safety source target byte length'
        Require-Equal (Relative-Path $RunRoot $path) ([string]$target.relative_path) 'safety source target relative path'
    }
    $rules = Get-SafetyScanRules
    $counts = [ordered]@{}
    foreach ($ruleId in $rules.Keys) {
        $count = 0
        foreach ($target in $targets) {
            foreach ($line in [IO.File]::ReadAllLines((Full $target.path), [Text.Encoding]::UTF8)) {
                $count += [regex]::Matches($line, [string]$rules[$ruleId], [Text.RegularExpressions.RegexOptions]::CultureInvariant).Count
            }
        }
        $claimed = @($Scan.rules | Where-Object { [string]$_.rule_id -ceq $ruleId })
        if ($claimed.Count -ne 1) { Fail "safety static scan rule '$ruleId' is missing or duplicated." }
        Require-Equal ([int]$claimed[0].match_count) $count "safety static scan rule $ruleId"
        $counts[$ruleId] = $count
    }
    if (@($Scan.rules).Count -ne $rules.Count) { Fail 'safety static scan contains an unknown rule.' }
    return $counts
}
function Invoke-NegativeEvidenceProofs([string]$RunRoot, [string[]]$Names) {
    $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) { Fail 'python.exe is required for real negative evidence proofs.' }
    $python = Full $pythonCommand.Source
    [void](Require-File $python 'negative proof Python runtime')
    $proofScript = Join-Path $RunRoot 'runner\acceptance-inputs\Invoke-P3NegativeEvidenceProofs.py'
    [void](Require-File $proofScript 'sealed negative evidence proof harness')
    $workspaceName = '.p3-negative-proof-' + [IO.Path]::GetFileName($RunRoot) + '-' + [Guid]::NewGuid().ToString('N')
    $workspace = Join-Path (Split-Path -Parent $RunRoot) $workspaceName
    if ((Inside $workspace $RunRoot) -or (Inside $RunRoot $workspace)) {
        Fail 'negative proof workspace must be a sibling outside the sealed run root.'
    }
    $namesJson = ConvertTo-Json @($Names) -Compress
    $arguments = @('-I', $proofScript, '--run-root', $RunRoot, '--workspace', $workspace, '--names-json', $namesJson)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $python
    $startInfo.Arguments = ($arguments | ForEach-Object { Quote-ProcessArgument ([string]$_) }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { Fail 'real negative evidence proof harness did not start.' }
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        if (-not $process.WaitForExit(3600000)) {
            try { $process.Kill() } catch { }
            Fail 'real negative evidence proof harness exceeded 60 minutes.'
        }
        if ($process.ExitCode -ne 0) {
            Fail "real negative evidence proof harness failed with exit $($process.ExitCode): $stdout $stderr"
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Fail "real negative evidence proof harness emitted stderr: $stderr"
        }
        try { $proof = $stdout.Trim() | ConvertFrom-Json -ErrorAction Stop }
        catch { Fail "real negative evidence proof output is not one JSON document: $($_.Exception.Message)" }
        Require-Equal $proof.schema 'pixel-tart-p3-negative-evidence-proof/v1' 'negative evidence proof schema'
        Require-Equal ([int]$proof.count) $Names.Count 'negative evidence proof count'
        Require-String $proof.proof_sha256 'negative evidence proof hash' '^[0-9a-f]{64}$'
        $proofRows = @($proof.proofs)
        Require-Equal $proofRows.Count $Names.Count 'negative evidence proof row count'
        for ($index = 0; $index -lt $Names.Count; $index++) {
            Require-Equal $proofRows[$index].name $Names[$index] "negative evidence proof name[$index]"
            if (@($proofRows[$index].changed_paths).Count -lt 1) { Fail "negative evidence proof changed no real file: $($Names[$index])" }
            if ([int]$proofRows[$index].exit_code -eq 0) { Fail "negative evidence proof validator accepted: $($Names[$index])" }
            Require-String $proofRows[$index].rejection_sha256 "negative evidence proof rejection hash[$index]" '^[0-9a-f]{64}$'
        }
        $canonicalProofRows = ConvertTo-Json $proofRows -Depth 20 -Compress
        Require-Equal (Sha256Text $canonicalProofRows) $proof.proof_sha256 'negative evidence proof recomputed hash'
        return [pscustomobject]@{ count = [int]$proof.count; sha256 = [string]$proof.proof_sha256 }
    } finally { $process.Dispose() }
}

if (-not (Test-AbsolutePath $RunRoot)) { Fail 'RunRoot must be absolute.' }
$root = Full $RunRoot
if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail 'RunRoot does not exist.' }
if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { Fail 'RunRoot may not be a reparse point.' }
if ($SkipNegativeProofs) {
    $proofWorkspace = Split-Path -Parent $root
    if ([IO.Path]::GetFileName($proofWorkspace) -notmatch '^\.p3-negative-proof-[A-Za-z0-9._-]+$') {
        Fail 'recursive negative-proof validation is restricted to a named sibling proof workspace.'
    }
}
$sealedContractPath = Join-Path $root 'runner\acceptance-inputs\automated-acceptance-contract.json'
$contract = Read-Json $sealedContractPath 'sealed contract'
Require-Equal $contract.schema 'pixel-tart-asset-library-p3-automated-acceptance-contract/v1' 'contract.schema'
Require-Equal $contract.validation_mode 'automated' 'contract.validation_mode'
Require-Equal $contract.owner_manual_ux_smoke 'waived' 'contract.owner_manual_ux_smoke'
Require-Equal ([bool]$contract.manual_evidence_claimed) $false 'contract.manual_evidence_claimed'
Require-Equal $contract.safety_measurement_schema 'pixel-tart-p3-safety-measurement/v1' 'contract safety measurement schema'
Require-Equal $contract.safety_static_scan_schema 'pixel-tart-p3-safety-static-scan/v1' 'contract safety static scan schema'
Require-Equal $contract.safety_path_confinement_schema 'pixel-tart-p3-run-owned-path-confinement/v1' 'contract safety path schema'

$expectedScenarios = @($contract.required_scenario_order | ForEach-Object { [string]$_ })
$fixedScenarios = @(
    'scope-switch/v1', 'ime-cancellation/v1', 'search-suggestions-history/v1',
    'folder-any-all-not/v1', 'tag-any-all-not/v1', 'scalar-null-composition/v1',
    'visual-composition/v1', 'nested-canonical-query/v1', 'invalid-query-fail-closed/v1',
    'smart-folder-lifecycle-preview/v1', 'smart-folder-invalid-migration/v1',
    'tag-manager-lifecycle/v1', 'bulk-metadata-journal/v1', 'four-view-resilience-layout/v1')
if ($expectedScenarios.Count -ne 14 -or ($expectedScenarios -join '|') -cne ($fixedScenarios -join '|')) {
    Fail 'contract scenario order is not the fixed 14-scenario P3 order.'
}
$expectedRestarts = @($contract.required_restart_scenarios | ForEach-Object { [string]$_ })
$fixedRestarts = @('search-suggestions-history/v1', 'smart-folder-lifecycle-preview/v1', 'bulk-metadata-journal/v1')
if (($expectedRestarts -join '|') -cne ($fixedRestarts -join '|')) { Fail 'contract restart scenario order differs.' }
Require-Equal ([int]$contract.required_runner_session_count) 17 'runner session contract'
Require-Equal ([int]$contract.repository.schema_version) 7 'repository schema contract'
Require-Equal ([int]$contract.repository.legacy_schema_version) 6 'legacy repository schema contract'
Require-Equal ([int]$contract.fixture.total_count) 10128 'fixture total'
Require-Equal ([int]$contract.fixture.active_count) 10000 'fixture active'
Require-Equal ([int]$contract.fixture.archived_count) 128 'fixture archived'
Require-Equal ([int]$contract.fixture.schema_version) 7 'fixture schema version'
Require-Equal ([int]$contract.fixture.display_name_count) 10128 'fixture display-name count'
Require-Equal $contract.fixture.display_name_language 'zh-CN' 'fixture display-name language'
Require-Equal ([int]$contract.fixture.content_hash_count) 10128 'fixture content-hash count'
Require-Equal $contract.fixture.content_hash_algorithm 'sha256' 'fixture content-hash algorithm'
Require-Equal ([bool]$contract.fixture.content_hash_deterministic) $true 'fixture content-hash determinism'
Require-Equal ([int]$contract.fixture.missing_count) 512 'fixture missing count'
Require-Equal $contract.fixture.visual_feature_counts.analysis_version 'visual-analysis-v2' 'fixture visual analysis version'
Require-Equal ([int]$contract.fixture.visual_feature_counts.valid) 3072 'fixture visual valid count'
Require-Equal ([int]$contract.fixture.visual_feature_counts.failed) 1024 'fixture visual failed count'
Require-Equal ([int]$contract.fixture.visual_feature_counts.not_analyzed) 6032 'fixture visual not-analyzed count'
Require-Equal ([int]$contract.fixture.visual_feature_counts.feature_rows) 4096 'fixture visual feature-row count'
Require-Equal ([int]$contract.fixture.legacy_variant.schema_version) 6 'legacy fixture schema version'
Require-Equal ([int]$contract.fixture.legacy_variant.total_count) 64 'legacy fixture total'
Require-Equal ([int]$contract.fixture.legacy_variant.active_count) 60 'legacy fixture active'
Require-Equal ([int]$contract.fixture.legacy_variant.archived_count) 4 'legacy fixture archived'

$requiredKinds = @('screenshots','bounds','query-documents','query-plans','result-hashes','histories',
    'smart-folders','tags','memberships','journals','commands','selections','views','performance','databases')
if ((@($contract.required_evidence_kinds | ForEach-Object { [string]$_ }) -join '|') -cne ($requiredKinds -join '|')) {
    Fail 'required evidence kinds differ from the fixed P3 contract.'
}
$requiredDpi = @(100,125,150,200)
if ((@($contract.required_dpi_matrix | ForEach-Object { [int]$_.scale_percent }) -join '|') -cne ($requiredDpi -join '|')) {
    Fail 'required DPI matrix differs from 100/125/150/200.'
}
$fixedThresholds = [ordered]@{
    first_screen_10000=1500; search_suggestion=200; single_filter_update=300;
    nested_8_rule_query=600; smart_folder_preview=750; scope_switch=400;
    batch_tag_100=750; batch_tag_500=2000; ui_block=100
}
foreach ($name in $fixedThresholds.Keys) {
    Require-Equal ([int](Property-Value $contract.performance_thresholds_ms $name)) ([int]$fixedThresholds[$name]) "performance threshold $name"
}

$negativeGuardMap = [ordered]@{
    'missing-screenshot'='required screenshots and PNG signature'; 'mutated-hash'='recompute every evidence hash';
    'wrong-scenario-order'='exact 14-scenario order'; 'wrong-restart-order'='exact 3-restart order';
    'fixture-count-mismatch'='independent 10128/10000/128 audit';
    'fixture-content-hash-mismatch'='deterministic source hashes'; 'fixture-schema-marker-mismatch'='v7 schema marker';
    'fixture-path-escape'='all fixture paths remain under run root'; 'legacy-fixture-missing'='independent v6 fixture audit';
    'duplicate-automation-id'='unique bounds identities'; 'canonical-query-hash-mismatch'='canonical document SHA-256';
    'query-result-hash-mismatch'='result asset-id SHA-256'; 'query-plan-parameter-mismatch'='plan parameter count';
    'unparameterized-sql'='zero unparameterized SQL'; 'scope-result-mismatch'='scope result hashes';
    'stale-cancelled-query'='cancelled generation cannot publish'; 'search-history-not-persisted'='restart history handshake';
    'folder-any-all-not-mismatch'='folder composition result hashes'; 'tag-any-all-not-mismatch'='tag composition result hashes';
    'scalar-null-mismatch'='scalar/null composition result hashes'; 'visual-query-mismatch'='visual composition result hashes';
    'nested-query-mismatch'='eight-rule canonical result'; 'invalid-query-expanded'='fail-closed invalid query';
    'smart-folder-roundtrip-mismatch'='smart-folder canonical roundtrip';
    'smart-folder-invalid-ref-expanded'='invalid reference fail-closed'; 'smart-folder-migration-mismatch'='v6 to v7 migration evidence';
    'tag-merge-membership-duplicate'='deduplicated merged memberships'; 'tag-group-cycle-accepted'='acyclic group validation';
    'batch-partial-commit'='atomic 100/500 tag batches'; 'journal-chain-mismatch'='journal previous-hash chain';
    'undo-redo-mismatch'='command undo/redo snapshots'; 'restart-identity-reused'='restart process identity differs';
    'view-result-divergence'='four view result hashes'; 'selection-hash-divergence'='selection preserved across views';
    'dpi-overflow'='four DPI bounds with no overflow'; 'contrast-threshold-failed'='contrast evidence pass';
    'accessibility-identity-missing'='nonempty unique identities'; 'performance-threshold-exceeded'='fixed metric thresholds';
    'ui-block-exceeded'='100ms UI block threshold'; 'user-source-write'='safety source counters';
    'eagle-write'='Eagle counters'; 'network-upload'='upload counters'; 'permanent-delete'='delete counter';
    'residual-process'='runner cleanup'; 'database-not-v7'='read-only SQLite v7 audit';
    'cross-run-splice'='run and head binding'; 'runner-session-splice'='17 unique sessions';
    'process-session-splice'='event/artifact session ownership'; 'binary-hash-mismatch'='sealed binary hashes';
    'input-tree-mutated'='before/after run-tree fingerprint'
}
$negativeNames = @($contract.required_negative_fixtures | ForEach-Object { [string]$_ })
if ((@($negativeGuardMap.Keys) -join '|') -cne ($negativeNames -join '|')) { Fail 'negative fixture list has no exact validator guard map.' }
$sealedRun = Assert-SealedRun $root $contract
$fingerprintBefore = Tree-Fingerprint $root

$manifest = Read-Json (Join-Path $root 'run-manifest.json') 'run manifest'
Require-Equal $manifest.schema_version 'pixel-tart-p3-automated-run/v1' 'manifest schema'
Require-Equal $manifest.validation_mode 'automated' 'manifest validation mode'
Require-Equal $manifest.owner_manual_ux_smoke 'waived' 'manifest owner smoke'
Require-Equal ([bool]$manifest.manual_evidence_claimed) $false 'manifest manual claim'
Require-Equal $manifest.automated_capture_status 'captured' 'manifest status'
Require-Equal (Full $manifest.run_root) $root 'manifest run root'
Require-String $manifest.run_id 'run id' '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$'
Require-String $manifest.source_head 'source head' '^[0-9a-f]{40}$'
Require-Equal $manifest.branch 'feature/asset-library-eagle-parity-p3-query-metadata' 'branch'

$fixture = $manifest.fixture
Require-Equal $fixture.schema 'pixel-tart-p3-synthetic-fixture/v1' 'fixture schema'
Require-Equal ([int]$fixture.total_count) 10128 'fixture total count'
Require-Equal ([int]$fixture.active_count) 10000 'fixture active count'
Require-Equal ([int]$fixture.archived_count) 128 'fixture archived count'
Require-Equal ([int]$fixture.schema_version) 7 'fixture schema version'
Require-Equal ([int]$fixture.display_name_count) 10128 'fixture display-name count'
Require-Equal $fixture.display_name_language 'zh-CN' 'fixture display-name language'
Require-Equal ([int]$fixture.content_hash_count) 10128 'fixture content-hash count'
Require-Equal $fixture.content_hash_algorithm 'sha256' 'fixture content-hash algorithm'
Require-Equal ([bool]$fixture.content_hash_deterministic) $true 'fixture content-hash determinism'
Require-Equal ([int]$fixture.missing_count) 512 'fixture missing count'
Require-Equal $fixture.visual_feature_counts.analysis_version 'visual-analysis-v2' 'fixture visual analysis version'
Require-Equal ([int]$fixture.visual_feature_counts.valid) 3072 'fixture visual valid count'
Require-Equal ([int]$fixture.visual_feature_counts.failed) 1024 'fixture visual failed count'
Require-Equal ([int]$fixture.visual_feature_counts.not_analyzed) 6032 'fixture visual not-analyzed count'
Require-Equal ([int]$fixture.visual_feature_counts.feature_rows) 4096 'fixture visual feature-row count'
Require-Equal ([int]$fixture.legacy_variant.schema_version) 6 'legacy fixture schema version'
Require-Equal ([int]$fixture.legacy_variant.total_count) 64 'legacy fixture total count'
Require-Equal ([int]$fixture.legacy_variant.active_count) 60 'legacy fixture active count'
Require-Equal ([int]$fixture.legacy_variant.archived_count) 4 'legacy fixture archived count'
Require-Equal $fixture.source_path_observation 'sqlite-sourcepath-enumeration/v1' 'fixture source path observation'
Require-Equal ([int]$fixture.source_path_count) 10192 'fixture source path count'
Require-Equal ([int]$fixture.source_paths_inside_fixture_count) 10192 'fixture source paths inside fixture count'
Require-Equal ([int]$fixture.source_paths_outside_fixture_count) 0 'fixture source paths outside fixture count'
Require-String $fixture.current_source_path_sha256 'fixture current source path hash' '^[0-9a-f]{64}$'
Require-String $fixture.legacy_source_path_sha256 'fixture legacy source path hash' '^[0-9a-f]{64}$'
Require-String $fixture.source_path_tree_sha256 'fixture source path tree hash' '^[0-9a-f]{64}$'
Require-Equal ([int]$fixture.user_source_read_count) 0 'fixture user source read count'
Require-Equal ([int]$fixture.user_source_write_count) 0 'fixture user source write count'
$fixtureDirectory = Full $fixture.directory
if (-not (Inside $fixtureDirectory $root) -or -not (Test-Path -LiteralPath $fixtureDirectory -PathType Container)) {
    Fail 'fixture directory is absent or escapes the run root.'
}
$fixtureManifestPath = Full $fixture.fixture_manifest_path
if (-not (Inside $fixtureManifestPath $fixtureDirectory) -or
    [IO.Path]::GetFileName($fixtureManifestPath) -cne 'fixture-manifest.json') {
    Fail 'fixture manifest path is not bound to the fixture directory.'
}
[void](Require-File $fixtureManifestPath 'fixture manifest')
Require-Equal (Hash $fixtureManifestPath) $fixture.fixture_manifest_sha256 'fixture manifest hash'
$fixtureSnapshot = Read-Json $fixtureManifestPath 'fixture manifest'
$fixtureWithoutBinding = ($fixture | ConvertTo-Json -Depth 100 -Compress) | ConvertFrom-Json
[void]$fixtureWithoutBinding.PSObject.Properties.Remove('fixture_manifest_path')
[void]$fixtureWithoutBinding.PSObject.Properties.Remove('fixture_manifest_sha256')
Require-Equal (Sha256Text ($fixtureSnapshot | ConvertTo-Json -Depth 100 -Compress)) `
    (Sha256Text ($fixtureWithoutBinding | ConvertTo-Json -Depth 100 -Compress)) 'fixture manifest semantic binding'
$fixtureGeneratedRows = @($fixture.generated_files)
Require-Equal ([int]$fixture.generated_file_count) $fixtureGeneratedRows.Count 'fixture generated input file count metadata'
Require-Equal $fixtureGeneratedRows.Count 4 'fixture generated input file count'
$fixtureGeneratedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$liveFixtureGeneratedRows = [Collections.Generic.List[object]]::new()
foreach ($row in $fixtureGeneratedRows) {
    $relative = [string]$row.path
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('/') -or $relative.Contains('\') -or
        -not $fixtureGeneratedNames.Add($relative)) { Fail 'fixture generated input path is non-canonical or reused.' }
    $path = Full (Join-Path $fixtureDirectory $relative)
    $item = Require-File $path 'fixture generated input'
    Require-Equal ([int64]$item.Length) ([int64]$row.byte_length) "fixture generated input byte length '$relative'"
    $hash = Hash $path
    Require-Equal $hash $row.sha256 "fixture generated input hash '$relative'"
    $liveFixtureGeneratedRows.Add([ordered]@{ path = $relative; byte_length = [int64]$item.Length; sha256 = $hash })
}
$actualFixtureGeneratedFiles = @(Get-ChildItem -LiteralPath $fixtureDirectory -Force -File -ErrorAction Stop |
    Where-Object { $_.Name -cne 'fixture-manifest.json' })
Require-Equal $actualFixtureGeneratedFiles.Count $fixtureGeneratedRows.Count 'fixture generated input exact inventory count'
$expectedFixtureGeneratedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in 'asset-library-v16.db','asset-library-v16-legacy-v6.db','fixture-expectations.json','fixture-generator.py') {
    [void]$expectedFixtureGeneratedNames.Add($name)
}
if (-not $fixtureGeneratedNames.SetEquals($expectedFixtureGeneratedNames)) { Fail 'fixture generated input exact file names differ.' }
Require-Equal (Canonical-FileTreeHash @($liveFixtureGeneratedRows)) $fixture.generated_tree_sha256 'fixture generated input tree hash'
$fixtureDb = Full $fixture.database_path
if (-not (Inside $fixtureDb $root)) { Fail 'fixture database escapes the run root.' }
[void](Require-File $fixtureDb 'fixture database')
Require-Equal (Hash $fixtureDb) $fixture.database_sha256 'fixture database hash'
$legacyFixtureDb = Full $fixture.legacy_database_path
if (-not (Inside $legacyFixtureDb $root)) { Fail 'legacy fixture database escapes the run root.' }
[void](Require-File $legacyFixtureDb 'legacy fixture database')
Require-Equal (Hash $legacyFixtureDb) $fixture.legacy_database_sha256 'legacy fixture database hash'
$expectationsPath = Full $fixture.expectations_path
if (-not (Inside $expectationsPath $root)) { Fail 'fixture expectations escape the run root.' }
[void](Require-File $expectationsPath 'fixture expectations')
Require-Equal (Hash $expectationsPath) $fixture.expectations_sha256 'fixture expectations hash'
$expectations = Read-Json $expectationsPath 'fixture expectations'
Require-Equal $expectations.schema 'pixel-tart-p3-synthetic-fixture-expectations/v1' 'fixture expectations schema'
Require-Equal ([int]$expectations.current.schema_version) 7 'fixture expectations current schema'
Require-Equal ([int]$expectations.current.total_count) 10128 'fixture expectations current total'
Require-Equal ([int]$expectations.current.active_count) 10000 'fixture expectations current active'
Require-Equal ([int]$expectations.current.archived_count) 128 'fixture expectations current archived'
Require-Equal ([int]$expectations.legacy.schema_version) 6 'fixture expectations legacy schema'
Require-Equal ([int]$expectations.legacy.total_count) 64 'fixture expectations legacy total'
$generatorPath = Full $fixture.generator_script_path
if (-not (Inside $generatorPath $root)) { Fail 'fixture generator script escapes the run root.' }
[void](Require-File $generatorPath 'fixture generator script')
Require-Equal (Hash $generatorPath) $fixture.generator_script_sha256 'fixture generator script hash'
Require-Equal ([int64](Get-Item -LiteralPath $generatorPath -Force).Length) ([int64]$fixture.generator_script_byte_length) 'fixture generator script byte length'
$generatorArguments = @($fixture.generator_arguments | ForEach-Object { [string]$_ })
if ($generatorArguments.Count -ne 5 -or $generatorArguments[0] -cne '-I' -or
    (Full $generatorArguments[1]) -cne $generatorPath -or (Full $generatorArguments[2]) -cne (Full $fixture.directory) -or
    (Full $generatorArguments[3]) -cne $fixtureDb -or (Full $generatorArguments[4]) -cne $legacyFixtureDb) {
    Fail 'fixture generator arguments are not the sealed absolute invocation.'
}
$generatorProcess = $fixture.generator_process_result
Require-Equal ([int]$generatorProcess.exit_code) 0 'fixture generator exit code'
foreach ($pair in @(@('stdout','stdout_sha256'), @('stderr','stderr_sha256'))) {
    $logPath = Full $generatorProcess.($pair[0])
    if (-not (Inside $logPath $root)) { Fail "fixture generator $($pair[0]) escapes the run root." }
    [void](Require-File $logPath "fixture generator $($pair[0])")
    Require-Equal (Hash $logPath) $generatorProcess.($pair[1]) "fixture generator $($pair[1])"
}
$generatorOutputLines = @(Get-Content -LiteralPath (Full $generatorProcess.stdout) -Encoding UTF8 |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
Require-Equal $generatorOutputLines.Count 1 'fixture generator metadata record count'
try { $generatorMetadata = $generatorOutputLines[0] | ConvertFrom-Json -ErrorAction Stop }
catch { Fail "fixture generator metadata is not valid JSON: $($_.Exception.Message)" }
foreach ($field in 'schema_version','total_count','active_count','archived_count','display_name_count','content_hash_count','missing_count',
    'source_path_count','source_paths_inside_fixture_count','source_paths_outside_fixture_count') {
    Require-Equal ([int](Property-Value $generatorMetadata $field)) ([int](Property-Value $fixture $field)) "fixture generator metadata $field binding"
}
foreach ($field in 'source_path_observation','current_source_path_sha256','legacy_source_path_sha256','source_path_tree_sha256') {
    Require-Equal (Property-Value $generatorMetadata $field) (Property-Value $fixture $field) "fixture generator metadata $field binding"
}
Require-Equal (Full $generatorMetadata.expectations_path) $expectationsPath 'fixture generator expectations path binding'
$fixtureAudit = Invoke-ReadonlyFixtureAudit $fixtureDb 'current-v7'
Require-Equal ([string]$fixtureAudit.quick_check) 'ok' 'fixture independent quick_check'
Require-Equal ([int]$fixtureAudit.schema_version) 7 'fixture independent schema version'
Require-Equal ([int]$fixtureAudit.asset_count) 10128 'fixture independent total count'
Require-Equal ([int]$fixtureAudit.active_count) 10000 'fixture independent active count'
Require-Equal ([int]$fixtureAudit.archived_count) 128 'fixture independent archived count'
Require-Equal ([int]$fixtureAudit.display_name_count) 10128 'fixture independent display-name count'
Require-Equal ([int]$fixtureAudit.content_hash_count) 10128 'fixture independent content-hash count'
Require-Equal ([int]$fixtureAudit.missing_count) 512 'fixture independent missing count'
Require-Equal ([int]$fixtureAudit.visual_feature_rows) 4096 'fixture independent visual feature-row count'
Require-Equal ([int]$fixtureAudit.visual_valid_count) 3072 'fixture independent visual valid count'
Require-Equal ([int]$fixtureAudit.visual_failed_count) 1024 'fixture independent visual failed count'
Require-Equal ([int]$fixtureAudit.visual_not_analyzed_count) 6032 'fixture independent visual not-analyzed count'
Require-Equal ([int]$fixtureAudit.source_path_count) 10128 'fixture independent source path count'
Require-Equal ([int]$fixtureAudit.source_paths_inside_fixture_count) 10128 'fixture independent source paths inside count'
Require-Equal ([int]$fixtureAudit.source_paths_outside_fixture_count) 0 'fixture independent source paths outside count'
Require-Equal $fixtureAudit.source_path_sha256 $fixture.current_source_path_sha256 'fixture independent source path hash'
$legacyAudit = Invoke-ReadonlyFixtureAudit $legacyFixtureDb 'legacy-v6'
Require-Equal ([string]$legacyAudit.quick_check) 'ok' 'legacy fixture independent quick_check'
Require-Equal ([int]$legacyAudit.schema_version) 6 'legacy fixture independent schema version'
Require-Equal ([int]$legacyAudit.asset_count) 64 'legacy fixture independent total count'
Require-Equal ([int]$legacyAudit.active_count) 60 'legacy fixture independent active count'
Require-Equal ([int]$legacyAudit.archived_count) 4 'legacy fixture independent archived count'
Require-Equal ([int]$legacyAudit.source_path_count) 64 'legacy fixture independent source path count'
Require-Equal ([int]$legacyAudit.source_paths_inside_fixture_count) 64 'legacy fixture independent source paths inside count'
Require-Equal ([int]$legacyAudit.source_paths_outside_fixture_count) 0 'legacy fixture independent source paths outside count'
Require-Equal $legacyAudit.source_path_sha256 $fixture.legacy_source_path_sha256 'legacy fixture independent source path hash'
Require-Equal (Sha256Text ($fixtureAudit.source_path_sha256 + "`n" + $legacyAudit.source_path_sha256)) `
    $fixture.source_path_tree_sha256 'fixture independent source path tree hash'

$sessions = @($manifest.sessions)
Require-Equal $sessions.Count 17 'runner session count'
$expectedSessionScenarios = @($expectedScenarios + $expectedRestarts)
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
Require-Equal $build.schema_version 'pixel-tart-p3-automated-build/v1' 'build schema'
Require-Equal $build.source_head $manifest.source_head 'build source head'
foreach ($pair in @(@('executable_path','executable_sha256'),@('application_path','application_sha256'),@('asset_module_path','asset_module_sha256'))) {
    $path = Full $build.($pair[0]); if (-not (Inside $path (Join-Path $root 'binaries'))) { Fail "build $($pair[0]) is not run-owned." }
    [void](Require-File $path "build $($pair[0])"); Require-Equal (Hash $path) $build.($pair[1]) "build $($pair[1])"
}

$summary = Read-Json (Join-Path $root 'app/evidence/summary.json') 'application summary'
Require-Equal $summary.schema 'pixel-tart-p3-automated-summary/v1' 'summary schema'
Require-Equal $summary.status 'completed' 'summary status'
Require-Equal $summary.run_id $manifest.run_id 'summary run id'
Require-Equal $summary.source_head $manifest.source_head 'summary head'
$scenarios = @($summary.scenarios)
Require-Equal $scenarios.Count 14 'summary scenario count'
for ($index = 0; $index -lt 14; $index++) {
    $scenario = $scenarios[$index]
    Require-Equal $scenario.id $expectedScenarios[$index] "scenario[$index] id"
    Require-Equal ([int]$scenario.sequence) ($index + 1) "scenario[$index] sequence"
    Require-Equal $scenario.status 'passed' "scenario[$index] status"
    Require-String $scenario.primary_process_session_id "scenario[$index] primary session" '^[0-9a-f]{32}$'
    if (-not $seenSessions.Contains([string]$scenario.primary_process_session_id)) {
        Fail "scenario[$index] primary process session is not runner-owned."
    }
    $scenarioRoot = Full $scenario.scenario_root; if (-not (Inside $scenarioRoot $root)) { Fail "scenario[$index] root escapes run root." }
    $isLegacyMigration = $scenario.id -ceq 'smart-folder-invalid-migration/v1'
    $expectedTotal = if ($isLegacyMigration) { 64 } else { 10128 }
    $expectedActive = if ($isLegacyMigration) { 60 } else { 10000 }
    $expectedArchived = if ($isLegacyMigration) { 4 } else { 128 }
    Require-Equal ([int]$scenario.database.schema_version) 7 "scenario[$index] DB schema"
    Require-Equal ([int]$scenario.database.asset_count) $expectedTotal "scenario[$index] DB total"
    Require-Equal ([int]$scenario.database.active_asset_count) $expectedActive "scenario[$index] DB active"
    Require-Equal ([int]$scenario.database.archived_asset_count) $expectedArchived "scenario[$index] DB archived"
    Require-Equal ([bool]$scenario.database.real_repository) $true "scenario[$index] real repository"
    Require-Equal ([bool]$scenario.database.wal_present_after_close) $false "scenario[$index] DB WAL after close"
    Require-Equal ([bool]$scenario.database.shm_present_after_close) $false "scenario[$index] DB SHM after close"
    $databaseRelativePath = [string]$scenario.database.path
    Assert-CanonicalRunFilePath $databaseRelativePath "scenario[$index] DB evidence path"
    $databaseEvidencePath = Full (Join-Path $root $databaseRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (-not (Inside $databaseEvidencePath $scenarioRoot)) { Fail "scenario[$index] DB evidence escapes scenario root." }
    [void](Require-File $databaseEvidencePath "scenario[$index] DB evidence")
    Require-Equal (Full $scenario.database.absolute_path) $databaseEvidencePath "scenario[$index] DB evidence absolute path"
    Require-Equal (Hash $databaseEvidencePath) $scenario.database.sha256 "scenario[$index] DB evidence hash"
    $databaseEvidencePaths = @($scenario.database.evidence_paths | ForEach-Object { [string]$_ })
    if ($databaseEvidencePaths.Count -lt 1 -or $databaseEvidencePaths[-1] -cne $databaseRelativePath) {
        Fail "scenario[$index] final DB evidence is not the final database reference."
    }
    foreach ($databaseEvidenceRelativePath in $databaseEvidencePaths) {
        Assert-CanonicalRunFilePath $databaseEvidenceRelativePath "scenario[$index] DB evidence history path"
        $databaseEvidenceHistoryPath = Full (Join-Path $root $databaseEvidenceRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Inside $databaseEvidenceHistoryPath $scenarioRoot)) { Fail "scenario[$index] DB evidence history escapes scenario root." }
        [void](Require-File $databaseEvidenceHistoryPath "scenario[$index] DB evidence history")
    }
    $activeDatabasePath = Full $scenario.database.active_database_absolute_path
    if (-not (Inside $activeDatabasePath $scenarioRoot)) { Fail "scenario[$index] active DB path escapes scenario root." }
    if (@($scenario.screenshot_paths).Count -lt 1 -or @($scenario.bounds_paths).Count -lt 1) { Fail "scenario[$index] lacks screenshot/bounds evidence." }
}
foreach ($restartId in $expectedRestarts) {
    $restart = @($scenarios | Where-Object { [string]$_.id -ceq $restartId }) | Select-Object -First 1
    if ($null -eq $restart) { Fail "restart scenario is absent: $restartId" }
    Require-String $restart.restart_process_session_id "restart $restartId process session" '^[0-9a-f]{32}$'
    if (-not $seenSessions.Contains([string]$restart.restart_process_session_id)) {
        Fail "restart process session is not runner-owned: $restartId"
    }
    if ($restart.restart_process_session_id -ceq $restart.primary_process_session_id -or
        [int]$restart.restart_pid -eq [int]$restart.pid -or
        [int64]$restart.restart_hwnd_numeric -eq [int64]$restart.hwnd_numeric) {
        Fail "restart reused primary process identity: $restartId"
    }
}

$eventsPath = Join-Path $root 'app/evidence/events.ndjson'
[void](Require-File $eventsPath 'event journal')
$eventLines = @(Get-Content -LiteralPath $eventsPath -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($eventLines.Count -lt 34) { Fail 'event journal is unexpectedly short.' }
$previous = '0' * 64; $eventSessions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in $eventLines) {
    try { $event = $line | ConvertFrom-Json } catch { Fail 'event journal contains invalid JSON.' }
    $journalHash = Get-JournalCanonicalText $line 'event'
    Require-Equal $event.schema 'pixel-tart-p3-automated-event/v1' 'event schema'
    Require-Equal $event.run_id $manifest.run_id 'event run id'; Require-Equal $event.source_head $manifest.source_head 'event head'
    Require-Equal $event.previous_event_hash $previous 'event hash link'
    Require-Equal $event.previous_record_sha256 $previous 'event previous record hash alias'
    Require-String $event.event_hash 'event hash' '^[0-9a-f]{64}$'
    Require-Equal $event.event_hash $event.record_sha256 'event record hash alias'
    Require-Equal $event.event_hash $journalHash.claimed_hash 'event terminal claimed hash'
    Require-Equal (Sha256Text $journalHash.canonical) $event.event_hash 'event recomputed record hash'
    [void]$eventSessions.Add([string]$event.process_session_id); $previous = [string]$event.event_hash
}
if ($eventSessions.Count -ne 17 -or -not $eventSessions.SetEquals($seenSessions)) {
    Fail 'event journal is not owned by exactly the 17 runner process sessions.'
}

$summaryJournalPath = Join-Path $root 'app/evidence/summary.ndjson'
[void](Require-File $summaryJournalPath 'summary journal')
$summaryLines = @(Get-Content -LiteralPath $summaryJournalPath -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($summaryLines.Count -ne 17) { Fail 'summary journal must contain exactly one record per runner process session.' }
$previousSummary = '0' * 64
$summarySessions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$finalSummaryRecord = $null
foreach ($line in $summaryLines) {
    try { $record = $line | ConvertFrom-Json } catch { Fail 'summary journal contains invalid JSON.' }
    $journalHash = Get-JournalCanonicalText $line 'summary'
    Require-Equal $record.schema 'pixel-tart-p3-automated-summary/v1' 'summary journal schema'
    Require-Equal $record.run_id $manifest.run_id 'summary journal run id'
    Require-Equal $record.source_head $manifest.source_head 'summary journal head'
    Require-Equal $record.previous_summary_hash $previousSummary 'summary journal hash link'
    Require-Equal $record.previous_record_sha256 $previousSummary 'summary journal previous record hash alias'
    Require-String $record.summary_hash 'summary journal record hash' '^[0-9a-f]{64}$'
    Require-Equal $record.summary_hash $record.record_sha256 'summary journal record hash alias'
    Require-Equal $record.summary_hash $journalHash.claimed_hash 'summary journal terminal claimed hash'
    Require-Equal (Sha256Text $journalHash.canonical) $record.summary_hash 'summary journal recomputed record hash'
    Require-String $record.process_session_id 'summary journal process session' '^[0-9a-f]{32}$'
    [void]$summarySessions.Add([string]$record.process_session_id)
    $finalSummaryRecord = $record
    $previousSummary = [string]$record.summary_hash
}
if ($summarySessions.Count -ne 17 -or -not $summarySessions.SetEquals($seenSessions)) {
    Fail 'summary journal is not owned by exactly the 17 runner process sessions.'
}
if ($null -eq $finalSummaryRecord -or $null -eq (Property-Value $finalSummaryRecord 'summary')) {
    Fail 'final summary journal record does not embed the published summary.'
}
$publishedSummaryCanonical = ConvertTo-Json $summary -Depth 100 -Compress
$journalSummaryCanonical = ConvertTo-Json $finalSummaryRecord.summary -Depth 100 -Compress
Require-Equal (Sha256Text $journalSummaryCanonical) (Sha256Text $publishedSummaryCanonical) 'final summary journal embedded summary binding'

$artifacts = @($summary.artifacts)
if ($artifacts.Count -lt 30) { Fail 'artifact set is unexpectedly small.' }
$kinds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$artifactPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$artifactJson = @{}
$artifactCategories = @{}
foreach ($artifact in $artifacts) {
    $relative = [string]$artifact.path; Require-String $relative 'artifact path'
    $path = Full (Join-Path $root $relative); if (-not (Inside $path $root)) { Fail 'artifact escapes run root.' }
    if (-not $artifactPaths.Add($path)) { Fail 'artifact path is reused.' }
    [void](Require-File $path 'artifact'); Require-Equal (Hash $path) $artifact.sha256 'artifact hash'
    Require-Equal $artifact.run_id $manifest.run_id 'artifact run id'; Require-Equal $artifact.source_head $manifest.source_head 'artifact head'
    Require-String $artifact.process_session_id 'artifact process session' '^[0-9a-f]{32}$'
    if (-not $seenSessions.Contains([string]$artifact.process_session_id)) { Fail 'artifact process session is not runner-owned.' }
    $kind = [string]$artifact.kind
    $category = if ($kind -match '(?:screenshot|png)') {'screenshots'}
        elseif ($kind -match '(?:bounds|accessibility)') {'bounds'}
        elseif ($kind -match '(?:query[-_]?plan|explain)') {'query-plans'}
        elseif ($kind -match '(?:query[-_]?document|canonical)') {'query-documents'}
        elseif ($kind -match '(?:result[-_]?hash)') {'result-hashes'}
        elseif ($kind -match '(?:history|histories)') {'histories'}
        elseif ($kind -match '(?:smart[-_]?folder)') {'smart-folders'}
        elseif ($kind -match '(?:membership)') {'memberships'}
        elseif ($kind -match '(?:tag)') {'tags'}
        elseif ($kind -match '(?:journal)') {'journals'}
        elseif ($kind -match '(?:command|undo|redo)') {'commands'}
        elseif ($kind -match '(?:selection)') {'selections'}
        elseif ($kind -match '(?:view)') {'views'}
        elseif ($kind -match '(?:performance|timing)') {'performance'}
        elseif ($kind -match '(?:database|sqlite)') {'databases'}
        else { Fail "unknown evidence artifact kind: $kind" }
    [void]$kinds.Add($category)
    $artifactCategories[$relative] = $category
    if ($category -eq 'screenshots') {
        $bytes = [IO.File]::ReadAllBytes($path); if ($bytes.Length -lt 8 -or $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4e -or $bytes[3] -ne 0x47) { Fail 'screenshot is not a nonempty PNG.' }
    } elseif ($category -eq 'databases') {
        $stream = [IO.File]::OpenRead($path)
        try {
            $header = [byte[]]::new(16); if ($stream.Read($header, 0, 16) -ne 16) { Fail 'database artifact header is truncated.' }
            if ([Text.Encoding]::ASCII.GetString($header) -cne "SQLite format 3`0") { Fail 'database artifact is not SQLite.' }
        } finally { $stream.Dispose() }
    } else { $artifactJson[$relative] = Read-Json $path "artifact $relative" }
}
foreach ($required in @($contract.required_evidence_kinds)) { if (-not $kinds.Contains([string]$required)) { Fail "required artifact category '$required' is absent." } }

$boundsPayloads = @($artifactJson.GetEnumerator() | Where-Object {
    $artifactCategories[$_.Key] -ceq 'bounds'
} | ForEach-Object { $_.Value.payload })
foreach ($bounds in $boundsPayloads) {
    if ([bool](Property-Value $bounds 'has_overflow') -or
        [bool](Property-Value $bounds 'real_display_settings_changed') -or
        -not [bool](Property-Value $bounds 'contrast_passed')) {
        Fail 'bounds evidence reports overflow, a real display write, or failed contrast.'
    }
    $ids = @($bounds.elements | ForEach-Object { [string]$_.Identity } | Where-Object { $_ })
    if ($ids.Count -lt 1) { Fail 'bounds evidence has no accessibility identities.' }
    if (@($ids | Group-Object | Where-Object Count -gt 1).Count -gt 0) { Fail 'bounds evidence contains duplicate automation identities.' }
}
$dpiBounds = @($boundsPayloads | Where-Object { [int]$_.viewport.scale_percent -in @(100,125,150,200) })
foreach ($dpi in 100,125,150,200) {
    if (@($dpiBounds | Where-Object { [int]$_.viewport.scale_percent -eq $dpi }).Count -lt 1) {
        Fail "DPI $dpi evidence is absent."
    }
}

function Get-EvidenceJson(
    [string]$Category,
    [AllowEmptyString()][string]$ScenarioId = '',
    [AllowEmptyString()][string]$Phase = '',
    [AllowEmptyString()][string]$FileName = '') {
    return @($artifactJson.GetEnumerator() | Where-Object {
        $artifactCategories[$_.Key] -ceq $Category -and
        ([string]::IsNullOrEmpty($ScenarioId) -or [string](Property-Value $_.Value 'scenario_id') -ceq $ScenarioId) -and
        ([string]::IsNullOrEmpty($Phase) -or $_.Key.Replace('\','/') -match ('/' + [regex]::Escape($Phase) + '/')) -and
        ([string]::IsNullOrEmpty($FileName) -or [IO.Path]::GetFileName($_.Key) -ceq $FileName)
    } | Sort-Object Key | ForEach-Object { $_.Value })
}
function Require-SingleEvidenceJson(
    [string]$Category,
    [string]$ScenarioId,
    [string]$Phase,
    [string]$FileName) {
    $matches = @(Get-EvidenceJson $Category $ScenarioId $Phase $FileName)
    if ($matches.Count -ne 1) {
        Fail "evidence must be unique: category=$Category scenario=$ScenarioId phase=$Phase file=$FileName count=$($matches.Count)."
    }
    return $matches[0]
}

$queryDocuments = @(Get-EvidenceJson 'query-documents')
foreach ($document in $queryDocuments) {
    $payload = Property-Value $document 'payload'
    Require-String (Property-Value $payload 'canonical_json') 'canonical query JSON'
    Require-String (Property-Value $payload 'canonical_sha256') 'canonical query hash' '^[0-9a-f]{64}$'
    Require-Equal (Sha256Text ([string](Property-Value $payload 'canonical_json'))) ([string](Property-Value $payload 'canonical_sha256')) 'canonical query hash'
}
$queryScenarioIds = @(
    'scope-switch/v1','ime-cancellation/v1','search-suggestions-history/v1','folder-any-all-not/v1',
    'tag-any-all-not/v1','scalar-null-composition/v1','visual-composition/v1','nested-canonical-query/v1',
    'invalid-query-fail-closed/v1','smart-folder-lifecycle-preview/v1','smart-folder-invalid-migration/v1')
foreach ($scenarioId in $queryScenarioIds) {
    if (@(Get-EvidenceJson 'query-documents' $scenarioId).Count -lt 1 -or
        @(Get-EvidenceJson 'result-hashes' $scenarioId).Count -lt 1) {
        Fail "query scenario lacks canonical document or result hash: $scenarioId"
    }
}

$queryPlans = @(Get-EvidenceJson 'query-plans')
foreach ($plan in $queryPlans) {
    $payload = Property-Value $plan 'payload'
    $sqlTemplate = [string](Property-Value $payload 'sql_template')
    Require-String $sqlTemplate 'query plan SQL template'
    Require-Equal (Sha256Text $sqlTemplate) ([string](Property-Value $payload 'sql_template_sha256')) 'query plan SQL template hash'
    $placeholderNames = @([regex]::Matches($sqlTemplate, '\$[A-Za-z][A-Za-z0-9_]*') |
        ForEach-Object { $_.Value } | Sort-Object -Unique)
    $parameterNames = @((Property-Value $payload 'parameter_names') | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $parameterValueHashes = @((Property-Value $payload 'parameter_value_sha256'))
    if (-not [bool](Property-Value $payload 'parameterized') -or
        [int](Property-Value $payload 'unparameterized_sql_count') -ne 0 -or
        [int](Property-Value $payload 'parameter_count') -ne $parameterNames.Count -or
        ($placeholderNames -join '|') -cne ($parameterNames -join '|') -or
        $parameterValueHashes.Count -ne $parameterNames.Count -or
        @($parameterValueHashes | Where-Object { [string]$_ -cnotmatch '^[0-9a-f]{64}$' }).Count -ne 0) {
        Fail 'query plan is not fully parameterized.'
    }
    Require-String (Property-Value $payload 'explain_query_plan') 'EXPLAIN QUERY PLAN evidence'
    if (@((Property-Value $payload 'explain_rows')).Count -lt 1) { Fail 'EXPLAIN QUERY PLAN returned no real SQLite rows.' }
}

$resultEvidence = @(Get-EvidenceJson 'result-hashes')
foreach ($result in $resultEvidence) {
    $payload = Property-Value $result 'payload'
    $ids = @((Property-Value $payload 'asset_ids') | ForEach-Object { [string]$_ })
    if (@($ids | Select-Object -Unique).Count -ne $ids.Count) { Fail 'result hash evidence contains duplicate asset ids.' }
    Require-String (Property-Value $payload 'asset_id_sha256') 'result asset-id hash' '^[0-9a-f]{64}$'
    Require-Equal (Sha256Text ($ids -join "`n")) ([string](Property-Value $payload 'asset_id_sha256')) 'result asset-id hash'
    Require-Equal ([int](Property-Value $payload 'result_count')) $ids.Count 'result count'
    $resultKind = [string](Property-Value $payload 'result_kind')
    $totalCount = [int](Property-Value $payload 'total_count')
    if ($resultKind -ceq 'complete') {
        Require-Equal $totalCount $ids.Count 'complete result total count'
    } elseif ($resultKind -ceq 'published-page') {
        if ($totalCount -lt $ids.Count -or
            [int](Property-Value $payload 'published_page_count') -ne $ids.Count -or
            -not [bool](Property-Value $payload 'viewmodel_oracle_match') -or
            [string](Property-Value $payload 'oracle_asset_id_sha256') -cne [string](Property-Value $payload 'asset_id_sha256')) {
            Fail 'published ViewModel page differs from its independent repository oracle.'
        }
    } else { Fail 'result evidence has no recognized result_kind.' }
}

$scopeResults = @(Get-EvidenceJson 'result-hashes' 'scope-switch/v1')
if (@($scopeResults | ForEach-Object { [string](Property-Value $_.payload 'scope') } | Select-Object -Unique).Count -lt 3) {
    Fail 'scope switch evidence does not cover all-library, folder, and smart-folder scopes.'
}
$imeEvidence = Require-SingleEvidenceJson 'query-documents' 'ime-cancellation/v1' 'primary' 'ime-cancellation-query.json'
if ($null -eq $imeEvidence -or
    [bool](Property-Value $imeEvidence.payload 'cancelled_generation_published') -or
    -not [bool](Property-Value $imeEvidence.payload 'query_cancellation_observed') -or
    [bool](Property-Value $imeEvidence.payload 'cancelled_query_generation_published') -or
    [long](Property-Value $imeEvidence.payload 'published_query_generation') -le
        [long](Property-Value $imeEvidence.payload 'cancelled_query_generation')) {
    Fail 'cancelled IME suggestion or real query generation was published or not evidenced.'
}
$historyEvidence = Require-SingleEvidenceJson 'histories' 'search-suggestions-history/v1' 'restart' 'history-restart.json'
if ($null -eq $historyEvidence -or
    -not [bool](Property-Value $historyEvidence.payload 'persisted_after_restart') -or
    -not [bool](Property-Value $historyEvidence.payload 'suggestions_suppressed_during_composition')) {
    Fail 'search suggestion/history restart evidence failed.'
}

$compositionRequirements = [ordered]@{
    'folder-any-all-not/v1'=@('any','all','not')
    'tag-any-all-not/v1'=@('any','all','not')
    'scalar-null-composition/v1'=@('value','null','not-null')
    'visual-composition/v1'=@('valid','failed','not-analyzed')
}
foreach ($scenarioId in $compositionRequirements.Keys) {
    $variants = @(Get-EvidenceJson 'result-hashes' $scenarioId | ForEach-Object {
        [string](Property-Value $_.payload 'predicate_variant')
    } | Select-Object -Unique)
    foreach ($variant in @($compositionRequirements[$scenarioId])) {
        if ($variant -cnotin $variants) { Fail "$scenarioId lacks result evidence for '$variant'." }
    }
}
$nestedEvidence = Require-SingleEvidenceJson 'query-documents' 'nested-canonical-query/v1' 'primary' 'nested-eight-rule-query.json'
if ($null -eq $nestedEvidence -or [int](Property-Value $nestedEvidence.payload 'rule_count') -ne 8 -or
    -not [bool](Property-Value $nestedEvidence.payload 'canonical_roundtrip')) {
    Fail 'nested canonical query does not evidence exactly eight rules.'
}
$invalidEvidence = Require-SingleEvidenceJson 'result-hashes' 'invalid-query-fail-closed/v1' 'primary' 'invalid-reference-results.json'
if ($null -eq $invalidEvidence -or
    -not [bool](Property-Value $invalidEvidence.payload 'fail_closed') -or
    [int](Property-Value $invalidEvidence.payload 'result_count') -ne 0) {
    Fail 'invalid query did not fail closed to zero results.'
}

$smartLifecycle = Require-SingleEvidenceJson 'smart-folders' 'smart-folder-lifecycle-preview/v1' 'primary' 'smart-folder-lifecycle.json'
if ($null -eq $smartLifecycle -or
    -not [bool](Property-Value $smartLifecycle.payload 'canonical_roundtrip') -or
    -not [bool](Property-Value $smartLifecycle.payload 'preview_isolated')) {
    Fail 'smart-folder save/load/preview roundtrip evidence failed.'
}
$smartMigration = Require-SingleEvidenceJson 'smart-folders' 'smart-folder-invalid-migration/v1' 'primary' 'smart-folder-migration.json'
if ($null -eq $smartMigration -or
    [int](Property-Value $smartMigration.payload 'migrated_schema_version') -ne 7 -or
    -not [bool](Property-Value $smartMigration.payload 'invalid_reference_fail_closed')) {
    Fail 'smart-folder v6 migration or invalid-reference evidence failed.'
}
$tagLifecycle = Require-SingleEvidenceJson 'tags' 'tag-manager-lifecycle/v1' 'primary' 'tag-manager-lifecycle.json'
if ($null -eq $tagLifecycle -or
    [int](Property-Value $tagLifecycle.payload 'merge_duplicate_membership_count') -ne 0 -or
    -not [bool](Property-Value $tagLifecycle.payload 'group_cycle_rejected') -or
    -not [bool](Property-Value $tagLifecycle.payload 'rename_preserved_memberships')) {
    Fail 'tag manager lifecycle evidence failed.'
}

foreach ($size in 100,500) {
    $batch = Require-SingleEvidenceJson 'commands' 'bulk-metadata-journal/v1' 'primary' "batch-$size.json"
    if ($null -eq $batch -or -not [bool](Property-Value $batch.payload 'atomic') -or
        [int](Property-Value $batch.payload 'committed_count') -ne $size -or
        -not [bool](Property-Value $batch.payload 'undo_passed') -or
        -not [bool](Property-Value $batch.payload 'redo_passed')) {
        Fail "atomic batch tag evidence failed for $size items."
    }
}
$journalEvidence = Require-SingleEvidenceJson 'journals' 'bulk-metadata-journal/v1' 'primary' 'undo-journal-chain.json'
if ($null -eq $journalEvidence -or -not [bool](Property-Value $journalEvidence.payload 'chain_valid')) {
    Fail 'bulk metadata journal hash chain is absent or invalid.'
}
$membershipEvidence = Require-SingleEvidenceJson 'memberships' 'bulk-metadata-journal/v1' 'primary' 'batch-memberships.json'
if ($null -eq $membershipEvidence -or -not [bool](Property-Value $membershipEvidence.payload 'deduplicated')) {
    Fail 'bulk tag membership deduplication evidence failed.'
}

$viewEvidence = Require-SingleEvidenceJson 'views' 'four-view-resilience-layout/v1' 'primary' 'four-view-result-stability.json'
if ($null -eq $viewEvidence) { Fail 'four-view evidence is absent.' }
$viewRows = @($viewEvidence.payload.views)
$viewNames = @($viewRows | ForEach-Object { ([string]$_.mode).ToLowerInvariant() })
if (($viewNames -join '|') -cne 'grid|waterfall|justified|list') { Fail 'four-view order differs.' }
$resultHashes = @($viewRows | ForEach-Object { [string]$_.result_sha256 } | Select-Object -Unique)
$selectionHashes = @($viewRows | ForEach-Object { [string]$_.selection_sha256 } | Select-Object -Unique)
if ($resultHashes.Count -ne 1 -or $selectionHashes.Count -ne 1 -or
    $resultHashes[0] -cnotmatch '^[0-9a-f]{64}$' -or $selectionHashes[0] -cnotmatch '^[0-9a-f]{64}$') {
    Fail 'four views diverge in result or selection hash.'
}
foreach ($row in $viewRows) {
    if (-not [bool]$row.is_virtualizing -or [string]$row.virtualization_mode -cne 'Recycling' -or
        [int]$row.realized_item_count -ge [int]$row.query_total_count) {
        Fail 'a view realized the complete query or left recycling virtualization mode.'
    }
}
$selectionEvidence = Require-SingleEvidenceJson 'selections' 'four-view-resilience-layout/v1' 'primary' 'four-view-selection-stability.json'
if ($null -eq $selectionEvidence -or
    [string](Property-Value $selectionEvidence.payload 'selection_sha256') -cne $selectionHashes[0]) {
    Fail 'selection hash does not match the four-view snapshot.'
}
$contentStateEvidence = Require-SingleEvidenceJson 'views' 'four-view-resilience-layout/v1' 'primary' 'content-state-recovery.json'
$contentState = $contentStateEvidence.payload
if (-not [bool](Property-Value $contentState 'emptyStateObserved') -or
    -not [bool](Property-Value $contentState 'errorStateObserved') -or
    -not [bool](Property-Value $contentState 'loadingObservedDuringRetry') -or
    -not [bool](Property-Value $contentState 'cancelledStateObserved') -or
    -not [bool](Property-Value $contentState 'retryRecoveredReadyState') -or
    [string]::IsNullOrWhiteSpace([string](Property-Value $contentState 'retryButtonAccessibleIdentity'))) {
    Fail 'real empty, error, loading, cancelled, and recovered ViewModel states were not all evidenced.'
}
$buttonMatrixEvidence = Require-SingleEvidenceJson 'views' 'four-view-resilience-layout/v1' 'primary' 'live-button-state-matrix.json'
$buttonMatrixPayload = $buttonMatrixEvidence.payload
Require-Equal ([bool](Property-Value $buttonMatrixPayload 'live_visual_tree')) $true 'button matrix live visual-tree ownership'
Require-Equal ([bool](Property-Value $buttonMatrixPayload 'real_display_settings_changed')) $false 'button matrix display setting mutation'
Require-Equal ([bool](Property-Value $buttonMatrixPayload 'contrast_passed')) $true 'button matrix contrast result'
$buttonThemes = @((Property-Value $buttonMatrixPayload 'themes') | ForEach-Object { [string]$_ })
if (($buttonThemes -join '|') -cne 'dark|high-contrast') { Fail 'button matrix does not cover exactly dark and high-contrast themes.' }
$buttonStates = @((Property-Value $buttonMatrixPayload 'states') | ForEach-Object { [string]$_ })
if (($buttonStates -join '|') -cne 'normal|hover|pressed|focus|disabled|error') { Fail 'button matrix state order differs.' }
$buttonRows = @((Property-Value $buttonMatrixPayload 'matrix'))
if ($buttonRows.Count -ne 272) { Fail "button matrix row count differs: $($buttonRows.Count)." }
foreach ($theme in $buttonThemes) {
    foreach ($state in $buttonStates) {
        if (@($buttonRows | Where-Object { [string]$_.theme -ceq $theme -and [string]$_.state -ceq $state }).Count -lt 1) {
            Fail "button matrix lacks $theme/$state."
        }
    }
}
foreach ($row in $buttonRows) {
    if (-not [bool]$row.live_wpf_button_instance -or -not [bool]$row.source_declaration_probe -or
        -not [bool]$row.template_applied -or [string]$row.state_resolution -cnotmatch '^live-wpf-') {
        Fail 'button matrix contains a detached or unapplied WPF state probe.'
    }
    if ([string]$row.state -cne 'disabled' -and [bool]$row.text_contrast_applicable -and [double]$row.text_contrast -lt 4.5) {
        Fail 'button matrix text contrast is below 4.5:1.'
    }
    if ([string]$row.state -cne 'disabled' -and [bool]$row.non_text_contrast_applicable -and [double]$row.non_text_contrast -lt 3) {
        Fail 'button matrix non-text contrast is below 3:1.'
    }
    if ([string]$row.state -ceq 'focus' -and (-not [bool]$row.focus_visible -or [double]$row.focus_contrast -lt 3)) {
        Fail 'button matrix focus indication is absent or below 3:1.'
    }
}
$buttonScreenshotNames = @((Property-Value $buttonMatrixPayload 'screenshots') | ForEach-Object { [string]$_ })
if ($buttonScreenshotNames.Count -ne 12 -or @($buttonScreenshotNames | Select-Object -Unique).Count -ne 12) {
    Fail 'button state screenshot inventory must contain exactly 12 unique files.'
}
foreach ($fileName in $buttonScreenshotNames) {
    $matches = @($artifacts | Where-Object {
        [IO.Path]::GetFileName([string]$_.path) -ceq $fileName -and
        [string]$_.scenario_id -ceq 'four-view-resilience-layout/v1' -and
        [string]$_.kind -match '(?:screenshot|png)' -and
        [string]$_.path -match '/primary/'
    })
    if ($matches.Count -ne 1) { Fail "button state screenshot is absent or ambiguous: $fileName." }
}

$performance = Require-SingleEvidenceJson 'performance' 'four-view-resilience-layout/v1' 'primary' 'aggregate-performance.json'
if ($null -eq $performance) { Fail 'aggregate P3 performance evidence is absent.' }
$metrics = Property-Value $performance.payload 'metrics'
foreach ($name in $fixedThresholds.Keys) {
    $value = Property-Value $metrics $name
    if ($null -eq $value -or [double]$value -lt 0 -or [double]$value -gt [double]$fixedThresholds[$name]) {
        Fail "performance metric '$name' exceeds its P3 threshold or is absent."
    }
}

$auditRelativePath = [string]$contract.pre_cleanup_database_audit_file
Require-Equal $auditRelativePath 'runner/database-consistency-audit.json' 'pre-cleanup database audit contract path'
$auditPath = Full (Join-Path $root $auditRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
$auditBinding = $manifest.pre_cleanup_database_audit
Require-Equal (Full $auditBinding.path) $auditPath 'pre-cleanup database audit manifest path'
[void](Require-File $auditPath 'pre-cleanup database audit')
Require-Equal (Hash $auditPath) $auditBinding.sha256 'pre-cleanup database audit manifest hash'
Require-Equal ([int]$auditBinding.scenario_count) 14 'pre-cleanup database audit manifest scenario count'
foreach ($pair in @(@('stdout','stdout_sha256'), @('stderr','stderr_sha256'))) {
    $auditLogPath = Full $auditBinding.result.($pair[0])
    if (-not (Inside $auditLogPath $root)) { Fail "pre-cleanup database audit $($pair[0]) escapes the run root." }
    [void](Require-File $auditLogPath "pre-cleanup database audit $($pair[0])")
    Require-Equal (Hash $auditLogPath) $auditBinding.result.($pair[1]) "pre-cleanup database audit $($pair[1])"
}
$audit = Read-Json $auditPath 'pre-cleanup database audit'
Require-Equal $audit.schema 'pixel-tart-p3-pre-cleanup-database-audit/v1' 'database audit schema'
Require-Equal $audit.status 'passed' 'database audit status'; Require-Equal ([int]$audit.scenario_count) 14 'database audit scenario count'
$auditRows = @($audit.scenarios)
for ($auditIndex = 0; $auditIndex -lt $auditRows.Count; $auditIndex++) {
    $row = $auditRows[$auditIndex]
    $summaryScenario = $scenarios[$auditIndex]
    Require-Equal $row.scenario_id $expectedScenarios[$auditIndex] "database audit scenario[$auditIndex] id"
    Require-Equal $row.status 'matched' "database audit scenario[$auditIndex] status"
    Require-Equal (Full $row.scenario_root) (Full $summaryScenario.scenario_root) "database audit scenario[$auditIndex] root"
    $legacyRow = [string]$row.scenario_id -ceq 'smart-folder-invalid-migration/v1'
    $expectedTotal = if ($legacyRow) { 64 } else { 10128 }
    $expectedActive = if ($legacyRow) { 60 } else { 10000 }
    $expectedArchived = if ($legacyRow) { 4 } else { 128 }
    foreach ($side in 'active','evidence') {
        Require-Equal $row.$side.quick_check 'ok' "audit $side quick_check"
        Require-String $row.$side.sha256 "audit $side hash" '^[0-9a-f]{64}$'
        Require-Equal ([int]$row.$side.schema_version) 7 "audit $side schema"
        Require-Equal ([int]$row.$side.asset_count) $expectedTotal "audit $side total"
        Require-Equal ([int]$row.$side.active_count) $expectedActive "audit $side active"
        Require-Equal ([int]$row.$side.archived_count) $expectedArchived "audit $side archived"
    }
    Require-Equal (Full $row.active.path) (Full $summaryScenario.database.active_database_absolute_path) "audit active path[$auditIndex]"
    $summaryEvidencePath = Full (Join-Path $root ([string]$summaryScenario.database.path).Replace('/', [IO.Path]::DirectorySeparatorChar))
    Require-Equal (Full $row.evidence.path) $summaryEvidencePath "audit evidence path[$auditIndex]"
    Require-Equal (Hash $summaryEvidencePath) $row.evidence.sha256 "audit evidence live hash[$auditIndex]"
    Require-Equal $row.evidence.sha256 $summaryScenario.database.sha256 "audit/summary evidence hash[$auditIndex]"
}

Require-Equal ([bool]$manifest.process_cleanup_verified) $true 'process cleanup verified'
$cleanup = $manifest.process_cleanup
foreach ($field in 'devpreview_get_process_count_before','devpreview_cim_count_before','devpreview_get_process_count_after','devpreview_cim_count_after','dotnet_residual_pid_count','db_sidecar_count_after','runtime_database_count_after','environment_residual_count') { Require-Equal ([int]$cleanup.$field) 0 "cleanup.$field" }
Require-Equal ([bool]$cleanup.display_settings_unchanged) $true 'display settings unchanged'

$safetyMeasurement = $manifest.safety_measurement
Require-Equal $safetyMeasurement.schema 'pixel-tart-p3-safety-measurement/v1' 'safety measurement schema'
$safetyBeforeCounts = Measure-SealedSafetyScan $safetyMeasurement.static_scan_before $root
$safetyAfterCounts = Measure-SealedSafetyScan $safetyMeasurement.static_scan_after $root
Require-Equal $safetyMeasurement.static_scan_before.snapshot_tree_sha256 $safetyMeasurement.static_scan_after.snapshot_tree_sha256 'safety snapshot before/after hash'
Require-Equal ([bool]$safetyMeasurement.source_snapshot_unchanged) $true 'safety source snapshot unchanged'
foreach ($ruleId in (Get-SafetyScanRules).Keys) {
    Require-Equal ([int]$safetyBeforeCounts[$ruleId]) ([int]$safetyAfterCounts[$ruleId]) "safety scan before/after $ruleId"
}
$pathConfinement = $safetyMeasurement.path_confinement
Require-Equal $pathConfinement.schema 'pixel-tart-p3-run-owned-path-confinement/v1' 'safety path confinement schema'
Require-Equal (Full $pathConfinement.run_root) $root 'safety path confinement run root'
if ([int]$pathConfinement.observed_path_count -lt 100) { Fail 'safety path confinement observed too few runtime paths.' }
Require-Equal ([int]$pathConfinement.outside_run_root_path_count) 0 'safety outside run-root path count'
Require-Equal @($pathConfinement.outside_run_root_paths).Count 0 'safety outside run-root path list'
Require-Equal $pathConfinement.source_path_observation $fixture.source_path_observation 'safety source path observation binding'
Require-Equal ([int]$pathConfinement.source_path_count) ([int]$fixture.source_path_count) 'safety source path count binding'
Require-Equal ([int]$pathConfinement.source_paths_inside_fixture_count) ([int]$fixture.source_paths_inside_fixture_count) 'safety inside-fixture source path count binding'
Require-Equal ([int]$pathConfinement.source_paths_outside_fixture_count) ([int]$fixture.source_paths_outside_fixture_count) 'safety outside-fixture source path count binding'
Require-Equal $pathConfinement.source_path_tree_sha256 $fixture.source_path_tree_sha256 'safety source path tree hash binding'
$environmentObservation = $safetyMeasurement.environment_observation
if (@($environmentObservation.observed_keys).Count -lt 10) { Fail 'safety environment observation is incomplete.' }
$environmentBeforeRows = @($environmentObservation.before)
$environmentAfterRows = @($environmentObservation.after)
Require-Equal $environmentBeforeRows.Count @($environmentObservation.observed_keys).Count 'safety environment before row count'
Require-Equal $environmentAfterRows.Count @($environmentObservation.observed_keys).Count 'safety environment after row count'
for ($index = 0; $index -lt $environmentBeforeRows.Count; $index++) {
    Require-Equal $environmentBeforeRows[$index].key $environmentObservation.observed_keys[$index] "safety environment before key[$index]"
    Require-Equal $environmentAfterRows[$index].key $environmentObservation.observed_keys[$index] "safety environment after key[$index]"
    Require-String $environmentBeforeRows[$index].value_sha256 "safety environment before hash[$index]" '^[0-9a-f]{64}$'
    Require-String $environmentAfterRows[$index].value_sha256 "safety environment after hash[$index]" '^[0-9a-f]{64}$'
    Require-Equal ([bool]$environmentAfterRows[$index].is_null) ([bool]$environmentBeforeRows[$index].is_null) "safety environment null state[$index]"
    Require-Equal $environmentAfterRows[$index].value_sha256 $environmentBeforeRows[$index].value_sha256 "safety environment value hash[$index]"
}
Require-Equal ([int]$environmentObservation.residual_count) ([int]$cleanup.environment_residual_count) 'safety environment residual count'
Require-Equal @($environmentObservation.residual_keys).Count ([int]$environmentObservation.residual_count) 'safety environment residual list'
$displayObservation = $safetyMeasurement.display_observation
Require-Equal ([bool]$displayObservation.unchanged) ([bool]$cleanup.display_settings_unchanged) 'safety display observation'
Require-Equal ([int]$displayObservation.before.primary_width) ([int]$cleanup.display_before.primary_width) 'safety display before width'
Require-Equal ([int]$displayObservation.before.primary_height) ([int]$cleanup.display_before.primary_height) 'safety display before height'
Require-Equal ([int]$displayObservation.after.primary_width) ([int]$cleanup.display_after.primary_width) 'safety display after width'
Require-Equal ([int]$displayObservation.after.primary_height) ([int]$cleanup.display_after.primary_height) 'safety display after height'
$processObservation = $safetyMeasurement.process_observation
foreach ($field in 'devpreview_get_process_count_before','devpreview_cim_count_before','devpreview_get_process_count_after','devpreview_cim_count_after','dotnet_residual_pid_count','db_sidecar_count_after','runtime_database_count_after','environment_residual_count') {
    Require-Equal ([int]$processObservation.$field) ([int]$cleanup.$field) "safety process observation $field"
}
$derivedSafety = [ordered]@{
    desktop_input_injection_count = [int]$safetyAfterCounts.desktop_input_injection
    uia_invoke_count = [int]$safetyAfterCounts.uia_invoke
    forced_foreground_count = [int]$safetyAfterCounts.forced_foreground
    real_display_setting_write_count = [int]$safetyAfterCounts.real_display_setting_write
    eagle_read_count = [int]$safetyAfterCounts.eagle_io
    eagle_write_count = [int]$safetyAfterCounts.eagle_io
    user_source_read_count = [int]$pathConfinement.user_source_read_count
    user_source_write_count = [int]$pathConfinement.user_source_write_count
    user_source_move_count = [int]$pathConfinement.user_source_move_count
    user_source_delete_count = [int]$pathConfinement.user_source_delete_count
    user_source_rename_count = [int]$pathConfinement.user_source_rename_count
    direct_width_mutation_count = [int]$safetyAfterCounts.direct_width_mutation
    direct_settings_mutation_count = [int]$safetyAfterCounts.direct_settings_mutation
    direct_sqlite_row_edit_count = [int]$safetyAfterCounts.direct_sqlite_row_edit
    network_upload_count = [int]$safetyAfterCounts.network_upload
    third_party_upload_count = [int]$safetyAfterCounts.network_upload
    ai_upload_count = [int]$safetyAfterCounts.network_upload
    mcp_upload_count = [int]$safetyAfterCounts.network_upload
    permanent_delete_count = [int]$pathConfinement.permanent_delete_count
}
foreach ($name in @($contract.safety_zero_fields)) {
    Require-Equal ([int](Property-Value $manifest.safety ([string]$name))) ([int]$derivedSafety[[string]$name]) "manifest.safety.$name provenance"
    Require-Equal ([int]$derivedSafety[[string]$name]) 0 "derived safety.$name"
}
$applicationSafety = $summary.safety_measurement
Require-Equal $applicationSafety.owner 'independent-runner-after-process-exit' 'application safety measurement owner'
Require-Equal $applicationSafety.status 'pending' 'application safety measurement lifecycle'

$negativeFixtureProof = if ($SkipNegativeProofs) {
    [pscustomobject]@{ count = 0; sha256 = Sha256Text 'negative proofs skipped only by the recursive mutation validator' }
} else {
    Invoke-NegativeEvidenceProofs $root $negativeNames
}
if ($SkipNegativeProofs) {
    Require-Equal ([int]$negativeFixtureProof.count) 0 'recursive negative fixture proof count'
} else {
    Require-Equal ([int]$negativeFixtureProof.count) $negativeNames.Count 'negative fixture rejection proof count'
}
Require-String $negativeFixtureProof.sha256 'negative fixture rejection proof hash' '^[0-9a-f]{64}$'
$fingerprintAfter = Tree-Fingerprint $root
Require-Equal $fingerprintAfter $fingerprintBefore 'input tree fingerprint'
[pscustomobject]@{
    schema = 'pixel-tart-p3-automated-validation-result/v1';
    status = if ($SkipNegativeProofs) { 'passed-negative-baseline' } else { 'passed' };
    negative_proofs_skipped = [bool]$SkipNegativeProofs;
    validation_mode = 'automated';
    run_root = $root; run_id = $manifest.run_id; source_head = $manifest.source_head;
    scenario_count = 14; runner_session_count = 17; artifact_count = $artifacts.Count;
    fixture = '10128 total / 10000 active / 128 archived + legacy-v6 64/60/4'; input_tree_unchanged = $true;
    negative_fixture_proof_count = [int]$negativeFixtureProof.count; negative_fixture_proof_sha256 = $negativeFixtureProof.sha256;
    safety_measurement_schema = $safetyMeasurement.schema
} | ConvertTo-Json -Depth 5
