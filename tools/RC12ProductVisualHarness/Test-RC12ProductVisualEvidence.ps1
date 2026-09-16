[CmdletBinding()]
param([string]$EvidenceRoot = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) { $EvidenceRoot = Join-Path $repoRoot 'artifacts\rc12-product-visual' }
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$manifestPath = Join-Path $EvidenceRoot 'rc12-product-visual-evidence.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "RC12 visual manifest is missing: $manifestPath" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$errors = [Collections.Generic.List[string]]::new()
function Assert-Evidence([bool]$Condition, [string]$Message) { if (-not $Condition) { $errors.Add($Message) } }

$currentCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
Assert-Evidence ($manifest.schema -eq 'pixel-tart-rc12-product-visual/v1') 'Unexpected manifest schema.'
Assert-Evidence ($manifest.product_version -eq '2.3.0-RC12') 'Unexpected product version.'
Assert-Evidence ($manifest.source_commit -eq $currentCommit) "Evidence source commit does not match current HEAD ($currentCommit)."
Assert-Evidence ([bool]$manifest.real_app_xaml -and [bool]$manifest.real_main_window -and [bool]$manifest.pixel_tart_dark_theme) 'Evidence did not use the real themed application MainWindow.'
Assert-Evidence ([bool]$manifest.process_per_fixture -and -not [bool]$manifest.application_singleton_shared -and [bool]$manifest.lifecycle_isolated_per_capture) 'Per-capture process lifecycle isolation is not proven.'
Assert-Evidence ([bool]$manifest.source_files_unchanged) 'Synthetic source assets changed during capture.'

$captures = @($manifest.captures)
Assert-Evidence ($captures.Count -eq 71) "Expected 71 captures, found $($captures.Count)."
Assert-Evidence (@($captures | Where-Object group -eq 'product-screenshot').Count -eq 12) 'The 12 product screenshots are incomplete.'
Assert-Evidence (@($captures | Where-Object group -eq 'ux-simplification').Count -eq 10) 'The 10 UX simplification screenshots are incomplete.'
Assert-Evidence (@($captures | Where-Object group -eq 'dpi-current').Count -eq 32) 'The 32 current-DPI captures are incomplete.'
Assert-Evidence (@($captures | Where-Object group -eq 'asset-library-resolution').Count -eq 6) 'The six Asset Library resolution captures are incomplete.'
Assert-Evidence (@($captures | Where-Object group -eq 'asset-library-ux-closure').Count -eq 10) 'The ten Asset Library UX closure captures are incomplete.'
Assert-Evidence (@($captures | Where-Object group -eq 'asset-library-aspect-ratio').Count -eq 1) 'The Asset Library aspect-ratio capture is incomplete.'
Assert-Evidence ([bool]$manifest.aspect_ratio_baseline.exists -and (Test-Path -LiteralPath $manifest.aspect_ratio_baseline.path -PathType Leaf)) 'The pre-closure aspect-ratio baseline screenshot is missing.'
Assert-Evidence (@($captures | Where-Object { -not $_.passed -or -not $_.process_exited_before_next }).Count -eq 0) 'A capture failed or its process remained alive.'

$requiredScreenshots = @(
    '01_asset_library_grid.png','02_asset_library_masonry.png','03_asset_filter.png','04_asset_context_menu.png',
    '05_asset_inspector_project.png','06_viewer.png','07_calendar_booking_assets.png','08_inspiration_tray.png',
    '09_inspiration_collection.png','10_recent_library_switcher.png','11_offline_cached_preview.png','12_project_booking_picker.png'
)
$actualScreenshots = @($captures | Where-Object group -eq 'product-screenshot' | ForEach-Object file_name | Sort-Object)
Assert-Evidence ((@($requiredScreenshots | Sort-Object) -join '|') -ceq ($actualScreenshots -join '|')) 'Product screenshot names differ from the RC12 contract.'
$requiredClosureScreenshots = @(
    '01_clean.png','02_context_menu.png','03_submenu.png','04_folder_tree.png','05_filter_color.png',
    '06_inspector_rating.png','07_inspiration_board.png','08_loupe_idle.png','09_loupe_active.png','10_full_preview.png'
)
$actualClosureScreenshots = @($captures | Where-Object group -eq 'asset-library-ux-closure' | ForEach-Object file_name | Sort-Object)
Assert-Evidence ((@($requiredClosureScreenshots | Sort-Object) -join '|') -ceq ($actualClosureScreenshots -join '|')) 'Asset Library UX closure screenshot names differ from the contract.'
$dpi = @($captures | Where-Object group -eq 'dpi-current')
foreach ($percent in @(100,125,150,200)) { Assert-Evidence (@($dpi | Where-Object dpi_percent -eq $percent).Count -eq 8) "DPI $percent% does not contain eight product states." }

foreach ($capture in $captures) {
    Assert-Evidence (Test-Path -LiteralPath $capture.path -PathType Leaf) "Screenshot missing: $($capture.path)"
    Assert-Evidence (Test-Path -LiteralPath $capture.metadata_path -PathType Leaf) "Metadata missing: $($capture.metadata_path)"
    if (Test-Path -LiteralPath $capture.path -PathType Leaf) {
        Assert-Evidence ((Get-FileHash -LiteralPath $capture.path -Algorithm SHA256).Hash -eq $capture.sha256) "Screenshot hash mismatch: $($capture.path)"
    }
    if (Test-Path -LiteralPath $capture.metadata_path -PathType Leaf) {
        $metadata = Get-Content -LiteralPath $capture.metadata_path -Raw | ConvertFrom-Json
        Assert-Evidence ([bool]$metadata.passed) "Layout/theme metadata failed: $($capture.state)"
        Assert-Evidence ($metadata.sourceCommit -eq $manifest.source_commit) "Metadata source commit mismatch: $($capture.state)"
        Assert-Evidence ($metadata.layout.Overflow.Count -eq 0 -and $metadata.layout.ZeroSizedInteractive.Count -eq 0 -and $metadata.layout.TextClipping.Count -eq 0 -and $metadata.layout.UnexpectedWhiteSurfaces.Count -eq 0) "Blocking layout/theme issue: $($capture.state)"
    }
}

$before = @($manifest.source_before | ForEach-Object { "$($_.name)|$($_.bytes)|$($_.sha256)" })
$after = @($manifest.source_after | ForEach-Object { "$($_.name)|$($_.bytes)|$($_.sha256)" })
Assert-Evidence (($before -join "`n") -ceq ($after -join "`n")) 'Synthetic source integrity records differ.'
if ($errors.Count -ne 0) { throw ("RC12 product visual evidence FAILED:`n - " + ($errors -join "`n - ")) }
[pscustomobject]@{ product_version=$manifest.product_version; source_commit=$manifest.source_commit; captures=$captures.Count; dpi_states=$dpi.Count; passed=$true } | ConvertTo-Json
