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
    Write-JsonAtomic $planPath ([ordered]@{
        schema_version = 'pixel-tart-p3-automated-plan/v1'
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
    if ([string]$validation.schema -cne 'pixel-tart-p3-automated-validation-result/v1' -or
        [string]$validation.status -cne 'passed' -or
        [bool]$validation.negative_proofs_skipped -or
        $validationRoot -cne $targetRoot -or
        [string]$validation.source_head -cne $targetHead -or
        [int]$validation.negative_fixture_proof_count -ne 50 -or
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
$devPreviewGetProcessCountBefore = @(Get-ProcessSnapshot).Count
$devPreviewCimCountBefore = @(Get-CimProcessSnapshot).Count
$activeRunRoot = New-RunRoot
$acceptanceInputs = New-AcceptanceInputSnapshot $activeRunRoot
$logDirectory = Join-Path $activeRunRoot 'logs'
$runId = "p3-auto-$([guid]::NewGuid().ToString('N'))"
$validatorLogDirectory = Join-Path (Split-Path -Parent $activeRunRoot) "P3-Automated-Validator-$runId"
$runSealed = $false
$environmentBefore = @{}
foreach ($key in $script:environmentKeys) { $environmentBefore[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$manifestPath = Join-Path $activeRunRoot 'run-manifest.json'
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
    started_at = [DateTimeOffset]::UtcNow.ToString('O')
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
        $sessions.Add((Invoke-AppPhase 'primary' @($scenarioId) $sessionName $sourceHead $executable $activeRunRoot $runId $logDirectory $binarySnapshot))
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
        devpreview_get_process_count_before = $devPreviewGetProcessCountBefore
        devpreview_cim_count_before = $devPreviewCimCountBefore
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
        foreach ($field in 'stdout','stderr','scenario_root','executable_path','application_path','asset_module_path') {
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
    Write-Output $activeRunRoot
} catch {
    if (-not $runSealed) {
        $manifest.automated_capture_status = 'failed'
        $manifest.finished_at = [DateTimeOffset]::UtcNow.ToString('O')
        $manifest.failure = $_.Exception.ToString()
        Write-JsonAtomic $manifestPath $manifest
    }
    throw "P3 automated acceptance failed. Run root retained: $activeRunRoot`n$($_.Exception.Message)"
} finally {
    foreach ($key in $environmentBefore.Keys) { [Environment]::SetEnvironmentVariable($key, $environmentBefore[$key], 'Process') }
    Assert-NoDevPreview
}
