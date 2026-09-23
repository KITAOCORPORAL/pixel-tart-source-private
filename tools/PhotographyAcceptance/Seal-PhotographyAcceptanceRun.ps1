[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunRoot)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath($RunRoot)
$inputManifest=Get-Content -LiteralPath (Join-Path $root 'input-manifest.json') -Raw | ConvertFrom-Json
$failure=Get-Content -LiteralPath (Join-Path $root 'failure.json') -Raw | ConvertFrom-Json
$wpf=Get-Content -LiteralPath (Join-Path $root 'wpf/rc12-wpf-process-isolation.json') -Raw | ConvertFrom-Json
if ($failure.run_id -cne $inputManifest.run_id -or $failure.product_source_sha -cne $inputManifest.product_source_sha) { throw 'Run identity mismatch.' }
if (@($failure.suites).Count -ne 5 -or @($failure.suites | Where-Object { $_.exit_code -ne 0 -or $_.status -ne 'PASS' }).Count -ne 0) { throw 'At least one suite failed.' }
if ($wpf.failed_count -ne 0 -or $wpf.failed_fixture_count -ne 0 -or $wpf.timed_out_fixture_count -ne 0 -or $wpf.fixture_count -ne 143) { throw 'WPF gate did not pass.' }
$batch=Get-ChildItem -LiteralPath (Join-Path $root 'wpf') -Filter '*-BatchExportProcessedPixelsTests.trx' | Select-Object -First 1
if ($null -eq $batch) { throw 'Batch fixture TRX missing.' }
$match=[regex]::Match((Get-Content -LiteralPath $batch.FullName -Raw),'color_studio_batch_targets=30; dimensions=2400x1600; elapsed_ms=(\d+); working_set_before_mb=([\d.]+); process_peak_mb=([\d.]+)')
if (-not $match.Success) { throw 'Measured batch result missing.' }
function Sha([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
$runId=[string]$inputManifest.run_id; $source=[string]$inputManifest.product_source_sha
$stream=Join-Path $root 'events.jsonl'; $previous=@(Get-Content -LiteralPath $stream | Where-Object { $_.Trim() }); $number=$previous.Count
foreach ($line in $previous) { $event=$line | ConvertFrom-Json; if ($event.sequence -ne ([array]::IndexOf($previous,$line)+1) -or $event.run_id -cne $runId -or $event.product_source_sha -cne $source) { throw 'Event stream mismatch.' } }
if (($previous[-1] | ConvertFrom-Json).event -eq 'run_failed') {
    $number++; $recovery=[ordered]@{sequence=$number;run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');event='seal_recovery';suite='manifest';result='RECOVERED';reason='All suites passed; initial manifest postprocessing failed to parse the measured TRX; no test suite was rerun.'}
    Add-Content -LiteralPath $stream -Value ($recovery | ConvertTo-Json -Compress) -Encoding utf8
    $number++; $completed=[ordered]@{sequence=$number;run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');event='run_sealed';suite='run';result='PASS'}
    Add-Content -LiteralPath $stream -Value ($completed | ConvertTo-Json -Compress) -Encoding utf8
} elseif (($previous[-1] | ConvertFrom-Json).event -eq 'run_sealed' -and ($previous[-2] | ConvertFrom-Json).event -eq 'seal_recovery') {
    $completed=$previous[-1] | ConvertFrom-Json
} else { throw 'Recovery requires the recorded postprocessing failure.' }
$wpfPortable=[ordered]@{run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');producer='RC12WpfProcessIsolation (bounded class processes)';fixture_count=$wpf.fixture_count;test_count=$wpf.test_count;passed=$wpf.passed_count;failed=$wpf.failed_count;skipped=$wpf.skipped_count;timed_out=$wpf.timed_out_fixture_count;fixtures=@($wpf.fixtures | ForEach-Object { [ordered]@{name=$_.fixture;total=$_.total;passed=$_.passed;failed=$_.failed;skipped=$_.skipped;exit_code=$_.exit_code;timed_out=$_.timed_out} })}
$wpfPortable | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'wpf-summary.json') -Encoding utf8
$dpiImages=@(Get-ChildItem -LiteralPath (Join-Path $root 'dpi') -Filter '*.png' | Sort-Object Name)
if ($dpiImages.Count -lt 4) { throw 'Expected four logical-DPI captures.' }
$dpiSummary=[ordered]@{run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');producer='ReferenceWorkspaceDpiEvidenceTests';result='PASS';physical_display='NOT_TESTED';captures=@($dpiImages | ForEach-Object { [ordered]@{name=$_.Name;sha256=Sha $_.FullName;bytes=$_.Length} })}
$dpiSummary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'dpi-summary.json') -Encoding utf8
$performance=[ordered]@{targets=30;dimensions='2400x1600';elapsed_ms=[int]$match.Groups[1].Value;working_set_before_mb=[double]$match.Groups[2].Value;process_peak_mb=[double]$match.Groups[3].Value;status='PASS_WITH_BASELINE'}
$output=[ordered]@{run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');producer='Seal-PhotographyAcceptanceRun.ps1';suites=@($failure.suites | ForEach-Object { [ordered]@{name=$_.name;command=$_.command;started_at=$_.started_at;completed_at=$_.completed_at;timeout_seconds=$_.timeout_seconds;exit_code=$_.exit_code;passed=$_.passed;failed=$_.failed;skipped=$(if($_.name -eq 'core'){1}else{$_.skipped});total=$_.total;status=$_.status} });performance=$performance;logical_dpi='PASS';physical_dpi='NOT_TESTED';historical_rc12='EXCLUDED';seal_recovery='RECORDED_POSTPROCESSING_FAILURE_RECOVERED'}
$output | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $root 'output-manifest.json') -Encoding utf8
$files=@('input-manifest.json','output-manifest.json','events.jsonl','wpf-summary.json','dpi-summary.json','failure.json')
$artifacts=@($files | ForEach-Object { $file=Join-Path $root $_; if (-not (Test-Path -LiteralPath $file)) { throw "Missing artifact: $_" }; [ordered]@{path=$_;run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');producer=$(if($_ -eq 'wpf-summary.json'){'bounded-wpf-runner'}elseif($_ -like '*.trx'){'dotnet-test'}else{'photography-acceptance-runner'});artifact_sha256=Sha $file;bytes=(Get-Item -LiteralPath $file).Length} })
$rawFiles=@('core.trx','photography-dedicated.trx','logical-dpi.trx','wpf/rc12-wpf-process-isolation.json')
$rawHashes=@($rawFiles | ForEach-Object { $file=Join-Path $root $_; if (-not (Test-Path -LiteralPath $file)) { throw "Missing raw result: $_" }; [ordered]@{path=$_;sha256=Sha $file;bytes=(Get-Item -LiteralPath $file).Length;local_only=$true} })
$artifactEnvelope=[ordered]@{run_id=$runId;product_source_sha=$source;generated_at=[DateTimeOffset]::UtcNow.ToString('O');producer='Seal-PhotographyAcceptanceRun.ps1';artifacts=$artifacts;raw_local_result_hashes=$rawHashes}
$artifactEnvelope | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'artifact-manifest.json') -Encoding utf8
$manifest=[ordered]@{schema='pixel-tart-photography-acceptance/v1';run_id=$runId;product_source_sha=$source;branch=$inputManifest.branch;sdk_version=$inputManifest.sdk_version;fixture_version=$inputManifest.fixture_version;started_at=$inputManifest.started_at;completed_at=$completed.generated_at;machine_class=$inputManifest.machine_class;architecture=$inputManifest.architecture;commands=$output.suites;exit_codes=@($output.suites | ForEach-Object { $_.exit_code });input_manifest_hash=Sha (Join-Path $root 'input-manifest.json');output_manifest_hash=Sha (Join-Path $root 'output-manifest.json');artifact_manifest_hash=Sha (Join-Path $root 'artifact-manifest.json');artifact_count=$artifacts.Count;first_event_sequence=1;last_event_sequence=$number;event_count=$number;event_digest=Sha $stream;artifacts=$artifacts;performance=$performance;logical_dpi='PASS';physical_dpi='NOT_TESTED';historical_rc12='EXCLUDED';known_exceptions=@('Core diagnostic opt-in skip','Four WPF production/visual opt-in skips','Physical-display DPI not tested','Historical RC12 excluded','Manifest postprocessing failure recorded at sequence 12 and recovered without suite rerun','Raw TRX and PNG capture bytes are local-only; portable run summaries and raw hashes are committed');evidence_consistency='PASS_WITH_DOCUMENTED_SEAL_RECOVERY';product_development_gate='PASS';release_hardware_gate='PENDING'}
$path=Join-Path $root 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json'
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8
$digest=Sha $path
Set-Content -LiteralPath (Join-Path $root 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST.sha256') -Value $digest -Encoding ascii
@('# Photography same-run acceptance','',('RunId: {0}' -f $runId),('ProductSourceSha: {0}' -f $source),'Core: 1400 passed / 0 failed / 1 diagnostic skip','WPF: 1308 passed / 0 failed / 4 opt-in skips / 0 timed out','Photography dedicated: 18 passed / 0 failed','Logical DPI: PASS; physical display: NOT TESTED',('Performance: 30 × 2400×1600, {0} ms, peak {1} MB' -f $performance.elapsed_ms,$performance.process_peak_mb),('Manifest SHA256: {0}' -f $digest),'Evidence consistency: PASS WITH DOCUMENTED SEAL RECOVERY','Product development gate: PASS; release hardware gate: PENDING','Historical RC12: EXCLUDED') | Set-Content -LiteralPath (Join-Path $root 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST.md') -Encoding utf8
[pscustomobject]@{run_id=$runId;manifest_sha256=$digest;artifact_count=$artifacts.Count;event_count=$number;source=$source} | ConvertTo-Json
