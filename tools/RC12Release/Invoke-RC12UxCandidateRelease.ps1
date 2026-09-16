[CmdletBinding()]
param(
    [string]$WpfEvidenceRoot = '',
    [string]$VisualEvidenceRoot = '',
    [string]$ScaleEvidenceRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$dotnet = [IO.Path]::GetFullPath((Join-Path $repoRoot '..\..\.dotnet\dotnet.exe'))
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$trackedStatus = (& git -C $repoRoot status --porcelain --untracked-files=no)
if ($trackedStatus) { throw 'RC12 UX candidate requires a clean tracked worktree.' }

if ([string]::IsNullOrWhiteSpace($WpfEvidenceRoot)) { $WpfEvidenceRoot = Join-Path $repoRoot 'artifacts\rc12-wpf-process-isolation\asset-library-ux-closure-current-head' }
if ([string]::IsNullOrWhiteSpace($VisualEvidenceRoot)) { $VisualEvidenceRoot = Join-Path $repoRoot 'artifacts\rc12-product-visual' }
if ([string]::IsNullOrWhiteSpace($ScaleEvidenceRoot)) { $ScaleEvidenceRoot = Join-Path $repoRoot 'artifacts\rc12-asset-library-ux-scale-current-head' }

$wpfManifest = Join-Path $WpfEvidenceRoot 'rc12-wpf-process-isolation.json'
$visualManifest = Join-Path $VisualEvidenceRoot 'rc12-product-visual-evidence.json'
$scaleManifest = Join-Path $ScaleEvidenceRoot 'rc12-visual-performance-scale-evidence.json'
foreach ($manifestPath in @($wpfManifest,$visualManifest,$scaleManifest)) {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Required current-HEAD gate manifest is missing: $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.source_commit -ne $head) { throw "Gate manifest does not match current HEAD: $manifestPath" }
}

& (Join-Path $repoRoot 'tools\RC12ProductVisualHarness\Test-RC12ProductVisualEvidence.ps1') -EvidenceRoot $VisualEvidenceRoot
if ($LASTEXITCODE -ne 0) { throw 'Current-HEAD product visual evidence validation failed.' }

$publishRoot = Join-Path $repoRoot 'artifacts\releases\2.3.0\publish\rc12-win-x64'
$installerRoot = Join-Path $repoRoot 'artifacts\releases\2.3.0\installer'
$installer = Join-Path $installerRoot '像素蛋挞_Setup_2.3.0_RC12_x64.exe'
[IO.Directory]::CreateDirectory($publishRoot) | Out-Null
[IO.Directory]::CreateDirectory($installerRoot) | Out-Null

& $dotnet publish (Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj') -c Release -r win-x64 --self-contained true -p:Platform=x64 --no-restore -o $publishRoot "-p:SourceRevisionId=$head"
if ($LASTEXITCODE -ne 0) { throw 'RC12 current-HEAD publish failed.' }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup was not found.' }

if (Test-Path -LiteralPath $installer) {
    $historyRoot = Join-Path $installerRoot 'history'
    [IO.Directory]::CreateDirectory($historyRoot) | Out-Null
    $oldHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.Substring(0,12)
    Copy-Item -LiteralPath $installer -Destination (Join-Path $historyRoot "像素蛋挞_Setup_2.3.0_RC12_pre_UX_$oldHash.exe") -Force
}
& $iscc '/DCandidateBuild' '/DCandidateRc12' (Join-Path $repoRoot 'installer\RAWSelectionAssistant.iss')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'RC12 UX candidate installer build failed.' }

$manifestPath = Join-Path $installerRoot 'rc12-ux-candidate-current-head.json'
$releaseManifest = [ordered]@{
    schema = 'pixel-tart-rc12-ux-candidate/v1'
    product_version = '2.3.0-RC12'
    source_commit = $head
    installer = $installer
    installer_bytes = (Get-Item -LiteralPath $installer).Length
    installer_sha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    application_sha256 = (Get-FileHash -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe') -Algorithm SHA256).Hash
    wpf_manifest_sha256 = (Get-FileHash -LiteralPath $wpfManifest -Algorithm SHA256).Hash
    visual_manifest_sha256 = (Get-FileHash -LiteralPath $visualManifest -Algorithm SHA256).Hash
    scale_manifest_sha256 = (Get-FileHash -LiteralPath $scaleManifest -Algorithm SHA256).Hash
    generated_at = [DateTimeOffset]::Now.ToString('O')
}
$releaseManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
[pscustomobject]$releaseManifest | ConvertTo-Json -Depth 6
