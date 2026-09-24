[CmdletBinding()]
param([string]$OutputRoot = '', [string]$Dotnet = '', [int]$CoreTimeoutSeconds = 180, [int]$WpfTimeoutSeconds = 1800)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Dotnet) { $Dotnet = if ($env:PIXEL_TART_DOTNET) { $env:PIXEL_TART_DOTNET } else { (Get-Command dotnet -ErrorAction Stop).Source } }
if (-not (Test-Path -LiteralPath $Dotnet)) { throw "SDK executable missing: $Dotnet" }
$source = (& git -C $repo rev-parse HEAD).Trim()
$branch = (& git -C $repo branch --show-current).Trim()
if ($branch -ne 'integration/pixel-tart-developer-preview') { throw "Unexpected branch: $branch" }
if (@(& git -C $repo status --porcelain -- src tests).Count -gt 0) { throw 'Product source/tests must be committed before a sealed acceptance run.' }
$sdk = (& $Dotnet --version).Trim()
$runId = 'photo-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + $source.Substring(0,8)
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo "docs/evidence/photography/$runId" }
$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw "Run directory already exists: $root" }
[IO.Directory]::CreateDirectory($root) | Out-Null
$started = [DateTimeOffset]::UtcNow.ToString('O')
$events = [Collections.Generic.List[object]]::new()
$suites = [Collections.Generic.List[object]]::new()
$sequence = 0
function Write-Event([string]$kind, [string]$suite, [string]$result) {
    $script:sequence++
    $row = [ordered]@{ sequence=$script:sequence; run_id=$runId; product_source_sha=$source; generated_at=[DateTimeOffset]::UtcNow.ToString('O'); event=$kind; suite=$suite; result=$result }
    $script:events.Add($row)
    Add-Content -LiteralPath (Join-Path $root 'events.jsonl') -Value ($row | ConvertTo-Json -Compress -Depth 8) -Encoding utf8
}
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Invoke-Bounded([string]$name, [string]$file, [string[]]$arguments, [int]$seconds, [string]$trxName = '') {
    $out = Join-Path $root "$name.stdout.txt"; $err = Join-Path $root "$name.stderr.txt"
    Write-Event 'suite_started' $name 'RUNNING'
    $start = [DateTimeOffset]::UtcNow
    $quoted = $arguments | ForEach-Object { if ($_ -match '\s') { '"' + ($_ -replace '"','\"') + '"' } else { $_ } }
    $process = Start-Process -FilePath $file -ArgumentList $quoted -WorkingDirectory $repo -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
    $finished = $process.WaitForExit([Math]::Max(1,$seconds)*1000)
    if (-not $finished) { try { $process.Kill($true) } catch {} ; [void]$process.WaitForExit(5000) }
    $exit = if ($finished) { $process.ExitCode } else { 124 }
    $process.Dispose()
    $passed=0; $failed=0; $skipped=0; $total=0
    if ($trxName -and (Test-Path -LiteralPath (Join-Path $root $trxName))) {
        [xml]$trx=Get-Content -LiteralPath (Join-Path $root $trxName) -Raw
        $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable); $ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $c=$trx.SelectSingleNode('//t:ResultSummary/t:Counters',$ns)
        if ($c) { $total=[int]$c.total; $passed=[int]$c.passed; $failed=[int]$c.failed; $skipped=[int]$c.notExecuted }
    }
    $status=if ($exit -eq 0 -and $failed -eq 0) { 'PASS' } elseif ($exit -eq 124) { 'TIMEOUT' } else { 'FAIL' }
    $script:suites.Add([ordered]@{ name=$name; command=($file + ' ' + ($arguments -join ' ')); started_at=$start.ToString('O'); completed_at=[DateTimeOffset]::UtcNow.ToString('O'); timeout_seconds=$seconds; exit_code=$exit; status=$status; passed=$passed; failed=$failed; skipped=$skipped; total=$total; stdout="$name.stdout.txt"; stderr="$name.stderr.txt"; trx=$trxName })
    Write-Event 'suite_completed' $name $status
    if ($status -ne 'PASS') { throw "$name failed ($status); see $out" }
}
try {
    $input = [ordered]@{ run_id=$runId; product_source_sha=$source; branch=$branch; sdk_version=$sdk; fixture_version='synthetic-reference-v2'; started_at=$started; machine_class='Windows-native-WPF-development'; architecture=[Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); historical_rc12='EXCLUDED' }
    $input | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'input-manifest.json') -Encoding utf8
    Write-Event 'run_started' 'run' 'RUNNING'
    Invoke-Bounded 'build' $Dotnet @('build','RAWSelectionAssistant.sln','-c','Release','-p:Platform=x64','--no-restore','-warnaserror','-v:q') 180
    Invoke-Bounded 'core' $Dotnet @('test','tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj','-c','Release','-p:Platform=x64','--no-build','--no-restore','--results-directory',$root,'--logger','trx;LogFileName=core.trx','--logger','console;verbosity=minimal') $CoreTimeoutSeconds 'core.trx'
    $env:PIXEL_TART_PHOTOGRAPHY_DPI_OUTPUT=Join-Path $root 'dpi'
    $dedicatedFilter='FullyQualifiedName~BatchExportProcessedPixelsTests|FullyQualifiedName~ReferenceWorkspaceDpiEvidenceTests|FullyQualifiedName~ReferenceWorkspaceMetadataTests|FullyQualifiedName~InspectorStarRatingTests'
    Invoke-Bounded 'photography-dedicated' $Dotnet @('test','tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj','-c','Release','-p:Platform=x64','--no-build','--no-restore','--filter',$dedicatedFilter,'--results-directory',$root,'--logger','trx;LogFileName=photography-dedicated.trx','--logger','console;verbosity=minimal') 180 'photography-dedicated.trx'
    $env:PIXEL_TART_DOTNET=$Dotnet
    Invoke-Bounded 'wpf' 'powershell.exe' @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $repo 'tools/RC12WpfProcessIsolation/Invoke-RC12WpfProcessIsolation.ps1'),'-SkipBuild','-AllowOptInSkips','-FixtureTimeoutSeconds','120','-EvidenceFixtureTimeoutSeconds','600','-OutputRoot',(Join-Path $root 'wpf')) $WpfTimeoutSeconds
    $wpf=Get-Content -LiteralPath (Join-Path $root 'wpf/rc12-wpf-process-isolation.json') -Raw | ConvertFrom-Json
    if ($wpf.failed_count -ne 0 -or $wpf.timed_out_fixture_count -ne 0 -or $wpf.failed_fixture_count -ne 0) { throw 'WPF fixture manifest did not pass.' }
    $suites[$suites.Count-1]['passed']=[int]$wpf.passed_count; $suites[$suites.Count-1]['failed']=[int]$wpf.failed_count; $suites[$suites.Count-1]['skipped']=[int]$wpf.skipped_count; $suites[$suites.Count-1]['total']=[int]$wpf.test_count
    Invoke-Bounded 'logical-dpi' $Dotnet @('test','tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj','-c','Release','-p:Platform=x64','--no-build','--no-restore','--filter','FullyQualifiedName~CurrentReferenceWorkspaceRendersAtFourScalesAndNarrowResolution','--results-directory',$root,'--logger','trx;LogFileName=logical-dpi.trx','--logger','console;verbosity=minimal') 90 'logical-dpi.trx'
    $batchTrx = Get-ChildItem -LiteralPath (Join-Path $root 'wpf') -Filter '*-BatchExportProcessedPixelsTests.trx' | Select-Object -First 1
    if (-not $batchTrx) { throw 'Same-run batch/performance fixture is missing.' }
    [xml]$batchXml = Get-Content -LiteralPath $batchTrx.FullName -Raw
    $batchText = [System.Net.WebUtility]::HtmlDecode($batchXml.InnerText)
    if ($batchText -notmatch 'color_studio_batch_targets=30; dimensions=2400x1600; elapsed_ms=(\d+); working_set_before_mb=([\d.]+); process_peak_mb=([\d.]+)') { throw 'Same-run 30-target performance baseline is missing.' }
    $performance=[ordered]@{ targets=30; dimensions='2400x1600'; elapsed_ms=[int]$Matches[1]; working_set_before_mb=[double]$Matches[2]; process_peak_mb=[double]$Matches[3]; status='PASS_WITH_BASELINE'; producer='BatchExportProcessedPixelsTests' }
    $output=[ordered]@{ run_id=$runId; product_source_sha=$source; generated_at=[DateTimeOffset]::UtcNow.ToString('O'); suites=$suites; logical_dpi='PASS'; physical_dpi='NOT_TESTED'; performance=$performance; historical_rc12='EXCLUDED' }
    $output | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $root 'output-manifest.json') -Encoding utf8
    Write-Event 'run_completed' 'run' 'PASS'
    $artifactList = [Collections.Generic.List[object]]::new()
    $files = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Name -notlike 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST*' -and $_.Name -ne 'artifact-manifest.json' } | Sort-Object FullName
    foreach ($f in $files) {
        $relative=[IO.Path]::GetRelativePath($root,$f.FullName).Replace('\','/')
        $artifactList.Add([ordered]@{ path=$relative; run_id=$runId; product_source_sha=$source; generated_at=[DateTimeOffset]::UtcNow.ToString('O'); producer=if ($relative -like 'wpf/*') { 'bounded-wpf-runner' } else { 'photography-acceptance-runner' }; artifact_sha256=Hash $f.FullName; bytes=$f.Length })
    }
    $artifactList | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'artifact-manifest.json') -Encoding utf8
    $manifest=[ordered]@{
        schema='pixel-tart-photography-acceptance/v1'; run_id=$runId; product_source_sha=$source; branch=$branch; sdk_version=$sdk; fixture_version='synthetic-reference-v2'; started_at=$started; completed_at=[DateTimeOffset]::UtcNow.ToString('O'); machine_class='Windows-native-WPF-development'; architecture=[Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        commands=$suites; exit_codes=@($suites | ForEach-Object { $_['exit_code'] }); input_manifest_hash=Hash (Join-Path $root 'input-manifest.json'); output_manifest_hash=Hash (Join-Path $root 'output-manifest.json'); artifact_count=$artifactList.Count; first_event_sequence=1; last_event_sequence=$sequence; event_count=$sequence; event_digest=Hash (Join-Path $root 'events.jsonl'); artifacts=$artifactList; performance=$performance; known_exceptions=@('Core CPU performance diagnostic opt-in skip','Four WPF visual/production opt-in skips','Physical display DPI not tested','Historical RC12 excluded'); product_development_gate='PASS'; release_hardware_gate='PENDING'
    }
    $manifestPath=Join-Path $root 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json'
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $sha=Hash $manifestPath
    @("# Photography acceptance", "", "RunId: ``$runId``", "ProductSourceSha: ``$source``", "SDK: ``$sdk``", "Core: $($suites[1]['passed']) passed / $($suites[1]['failed']) failed / $($suites[1]['skipped']) skipped", "WPF: $($suites[3]['passed']) passed / $($suites[3]['failed']) failed / $($suites[3]['skipped']) skipped", "Logical DPI: PASS; Physical DPI: NOT TESTED", "Performance: PASS WITH BASELINE; $($performance.elapsed_ms) ms; peak $($performance.process_peak_mb) MB", "InputManifestHash: ``$($manifest.input_manifest_hash)``", "OutputManifestHash: ``$($manifest.output_manifest_hash)``", "Events: 1..$sequence; digest ``$($manifest.event_digest)``", "Artifacts: $($artifactList.Count)", "Manifest SHA256: ``$sha``", "Historical RC12 evidence: EXCLUDED", "Product Development Gate: PASS; Release Hardware Gate: PENDING") | Set-Content -LiteralPath (Join-Path $root 'PHOTOGRAPHY_ACCEPTANCE_MANIFEST.md') -Encoding utf8
    Write-Output ([ordered]@{ run_id=$runId; source=$source; manifest_sha256=$sha; core=$suites[1]; wpf=$suites[3]; artifact_count=$artifactList.Count } | ConvertTo-Json -Depth 8)
} catch {
    Write-Event 'run_failed' 'run' 'FAIL'
    $failure=[ordered]@{ run_id=$runId; product_source_sha=$source; generated_at=[DateTimeOffset]::UtcNow.ToString('O'); error=$_.Exception.Message; suites=$suites }
    $failure | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'failure.json') -Encoding utf8
    throw
}
