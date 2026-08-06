param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$releaseRoot = Join-Path $repoRoot 'artifacts\releases\2.3.0'
$publishRoot = Join-Path $releaseRoot 'publish\win-x64'
$installerRoot = Join-Path $releaseRoot 'installer'
$productName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oye'))
$installer = Join-Path $installerRoot ($productName + '_Setup_2.3.0_RC2_x64.exe')
$rc1 = Join-Path $installerRoot ($productName + '_Setup_2.3.0_RC1_x64.exe')

function Get-ScopedRelativePath([string]$BasePath, [string]$Path) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped expected release root: $full" }
    $full.Substring($base.Length + 1).Replace('\','/')
}

foreach ($path in @($releaseRoot,$installerRoot)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
if (-not (Test-Path -LiteralPath $rc1)) { throw 'RC1 installer is missing; RC2 must not replace the accepted candidate.' }
$rc1Before = [ordered]@{ Bytes=(Get-Item -LiteralPath $rc1).Length; Sha256=(Get-FileHash -LiteralPath $rc1 -Algorithm SHA256).Hash }
if (Test-Path -LiteralPath $installer) { throw 'RC2 installer already exists; refusing to overwrite a candidate build.' }
if (-not $SkipBuild) {
    & (Join-Path $repoRoot 'build_release.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe'))) { throw '2.3.0 publish output is missing.' }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup was not found.' }
& $iscc '/DCandidateBuild' '/DCandidateRc2' (Join-Path $repoRoot 'installer\RAWSelectionAssistant.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath $installer)) { throw 'RC2 installer was not generated with the required name.' }

$forbiddenNames = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object { $_.Name -match '(?i)(testhost|\.Tests\.|UiReview|Acceptance|Sony.*\.dll|Canon.*\.dll|Nikon.*\.dll|Fujifilm.*\.dll|\.pdb$|\.log$|\.db$|\.cube$|\.icc$|\.icm$)' })
if ($forbiddenNames.Count -gt 0) { throw ('Release scan found forbidden files: ' + (($forbiddenNames.FullName) -join ', ')) }
$configs = Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object { $_.Extension -in @('.json','.config','.xml','.txt') }
$networkMatches = @($configs | Select-String -Pattern 'localhost|127\.0\.0\.1' -ErrorAction SilentlyContinue)
if ($networkMatches.Count -gt 0) { throw 'Release scan found localhost or 127.0.0.1.' }
$license = Get-Content (Join-Path $publishRoot 'appsettings.license.json') -Raw -Encoding UTF8
if ($license -notmatch '"Provider"\s*:\s*"None"' -or $license -match 'Mock|Fake Camera') { throw 'Release provider gate failed.' }
$version = (Get-Item -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe')).VersionInfo
if ($version.ProductVersion -notlike '2.3.0*' -or $version.FileVersion -notlike '2.3.0.0*') { throw 'Published version gate failed.' }
$rc1After = [ordered]@{ Bytes=(Get-Item -LiteralPath $rc1).Length; Sha256=(Get-FileHash -LiteralPath $rc1 -Algorithm SHA256).Hash }
if ($rc1Before.Bytes -ne $rc1After.Bytes -or $rc1Before.Sha256 -ne $rc1After.Sha256) { throw 'RC1 installer changed while producing RC2.' }

$signature = Get-AuthenticodeSignature -LiteralPath $installer
$files = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -File) + @(Get-Item -LiteralPath $installer)
$manifest = @($files | ForEach-Object {
    [ordered]@{ Path=(Get-ScopedRelativePath $releaseRoot $_.FullName); Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
} | Sort-Object Path)
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $releaseRoot 'file-manifest-rc2.json') -Encoding UTF8
$manifest | Export-Csv -LiteralPath (Join-Path $releaseRoot 'file-manifest-rc2.csv') -NoTypeInformation -Encoding UTF8
$manifest | ForEach-Object { "$($_.Sha256) *$($_.Path)" } | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS-RC2.txt') -Encoding UTF8
$uiIndex = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc2\evidence-index.json'
if (-not (Test-Path -LiteralPath $uiIndex)) { throw 'RC2 UI evidence index is missing.' }
Copy-Item -LiteralPath $uiIndex -Destination (Join-Path $releaseRoot 'ui-evidence-index-rc2.json') -Force

$releaseManifest = [ordered]@{
    Product=$productName; Version='2.3.0'; FileVersion='2.3.0.0'; SchemaVersion=3; Candidate='RC2'; Provider='None'
    PhysicalSecondMonitorTested=$false; GitCommit=(& git -C $repoRoot rev-parse HEAD).Trim(); OutputType='WinExe'
    SelfContained=$true; Runtime='win-x64'; Installer=(Get-ScopedRelativePath $releaseRoot $installer)
    InstallerSha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash; InstallerBytes=(Get-Item -LiteralPath $installer).Length
    SignatureStatus=$signature.Status.ToString(); Rc1Preserved=$true; Rc1Sha256=$rc1After.Sha256
    ReleaseScan=[ordered]@{ Passed=$true; NoTests=$true; NoVendorSdk=$true; NoLocalhost=$true; NoLogs=$true; NoDatabases=$true; NoSyntheticAssets=$true }
    GeneratedAt=[DateTimeOffset]::Now.ToString('O')
}
$releaseManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest-rc2.json') -Encoding UTF8
$releaseManifest | ConvertTo-Json -Depth 8
