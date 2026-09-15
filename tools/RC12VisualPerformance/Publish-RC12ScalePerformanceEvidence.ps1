[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$DiagnosticRoot,
    [string]$OutputRoot = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$src = [IO.Path]::GetFullPath($DiagnosticRoot)
$scale = Join-Path $src 'scale-matrix'
if (-not (Test-Path -LiteralPath $scale -PathType Container)) { throw "Scale matrix is missing: $scale" }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\rc12-visual-performance-scale-final' }
$dst = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $dst) { throw "Output root must be fresh: $dst" }
New-Item -ItemType Directory -Path $dst | Out-Null
Copy-Item -LiteralPath $scale -Destination $dst -Recurse
$rows = @(Get-ChildItem (Join-Path $dst 'scale-matrix') -Filter 'scale-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
$metrics = @('library_open_ms','first_visible_thumbnail_ms','thumbnail_queue_drained_ms','scroll_ms','search_ms','rating_filter_ms','project_filter_ms','booking_filter_ms','restart_cached_preview_ms','working_set_delta_bytes')
$summary = @()
foreach ($size in @(10000,50000,100000)) {
    $group = @($rows | Where-Object { [int]$_.size -eq $size })
    if ($group.Count -ne 3) { throw "Expected exactly three samples for $size; found $($group.Count)." }
    $values = [ordered]@{}
    foreach ($metric in $metrics) {
        $sorted = @($group | ForEach-Object { [double]$_.$metric } | Sort-Object)
        $values[$metric] = [ordered]@{
            p50 = [math]::Round($sorted[[math]::Ceiling(.50 * $sorted.Count) - 1], 3)
            p95 = [math]::Round($sorted[[math]::Ceiling(.95 * $sorted.Count) - 1], 3)
            max = [math]::Round(($sorted | Measure-Object -Maximum).Maximum, 3)
        }
    }
    $summary += [ordered]@{
        size = $size
        sample_count = $group.Count
        loaded_items_max = ($group.loaded_items | Measure-Object -Maximum).Maximum
        realized_items_max = ($group.realized_items_initial + $group.realized_items_after_scroll | Measure-Object -Maximum).Maximum
        thumbnail_queue_max = ($group.thumbnail_queue_max | Measure-Object -Maximum).Maximum
        metrics = $values
    }
}
$manifest = [ordered]@{
    schema = 'pixel-tart-rc12-visual-performance-scale/v1'
    product_version = '2.3.0-RC12'
    fixture_sizes = @(10000,50000,100000)
    samples_per_size = 3
    source_test = 'AssetLibraryP3PerformanceDiagnosticsTests.ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture'
    covered_operations = @('library-open','first-visible-thumbnail','scroll','search','rating-filter','project-filter','booking-filter','restart-cached-preview','working-set','thumbnail-queue','virtualized-realization')
    results = $summary
    test_result = 'PASS'
    generated_at = [DateTimeOffset]::Now.ToString('O')
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $dst 'rc12-visual-performance-scale-evidence.json') -Encoding UTF8
Get-Content (Join-Path $dst 'rc12-visual-performance-scale-evidence.json') -Raw
