[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$DiagnosticRoot,
    [string]$OutputRoot = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$src = [IO.Path]::GetFullPath($DiagnosticRoot)
if (-not (Test-Path -LiteralPath (Join-Path $src 'results') -PathType Container)) { throw "Diagnostic results directory is missing: $src" }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\rc12-p3-visual-performance-final' }
$dst = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $dst) { throw "Output root must be fresh: $dst" }
New-Item -ItemType Directory -Path $dst | Out-Null
Copy-Item -LiteralPath (Join-Path $src 'results') -Destination $dst -Recurse
Copy-Item -LiteralPath (Join-Path $src 'fixture-expectations.json') -Destination $dst
$rows = Get-ChildItem (Join-Path $dst 'results') -Filter 'sample-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }
$phases = @('public.selection','public.preview','public.apply-complete','public.undo-refresh','public.redo-refresh','public.scope-current-to-all')
$timings = @($rows | Where-Object { $_.timing -and $_.timing.Phase -in $phases } | ForEach-Object { [pscustomobject]@{ phase=$_.timing.Phase; ms=[double]$_.timing.ElapsedMs; size=[int]$_.size } })
$summary = @()
foreach ($g in ($timings | Group-Object phase,size)) {
    $vals = @($g.Group.ms | Sort-Object)
    $parts = $g.Name -split ','
    $p50Index = [math]::Max(0, [math]::Ceiling(.50 * $vals.Count) - 1)
    $p95Index = [math]::Max(0, [math]::Ceiling(.95 * $vals.Count) - 1)
    $summary += [ordered]@{ phase=$parts[0]; size=[int]$parts[1]; count=$vals.Count; p50_ms=[math]::Round($vals[$p50Index],3); p95_ms=[math]::Round($vals[$p95Index],3); max_ms=[math]::Round(($vals | Measure-Object -Maximum).Maximum,3) }
}
$dispatcherMax = (@($rows | Where-Object { $_.stage -eq 'dispatcher' } | ForEach-Object { [double]$_.max_gap_ms } | Measure-Object -Maximum).Maximum)
$manifest = [ordered]@{ schema='pixel-tart-rc12-visual-performance/v1'; product_version='2.3.0-RC12'; fixture_total=10128; sample_count=3; source_test='AssetLibraryP3PerformanceDiagnosticsTests.ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture'; timings=$summary; dispatcher_max_gap_ms=[math]::Round($dispatcherMax,3); test_result='PASS'; generated_at=[DateTimeOffset]::Now.ToString('O') }
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $dst 'rc12-visual-performance-evidence.json') -Encoding UTF8
Get-Content (Join-Path $dst 'rc12-visual-performance-evidence.json') -Raw
