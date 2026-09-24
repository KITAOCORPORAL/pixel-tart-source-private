param(
 [string]$EvidenceDirectory='docs/evidence/color-studio-phase1',
 [string]$ResultsDirectory='artifacts/color-studio-final/test-results'
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
 $source=(git rev-parse HEAD).Trim()
 if($source -ne 'c175ebe2c2c94fc78da4bce0bd94f90a977a6310') {
  throw 'This closure annotation is bound to the current recovery source c175ebe. Re-audit build, test provenance and visual reviews before recording a different source.'
 }
 $runId='color-studio-phase1-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
 $manifestPath=Join-Path $EvidenceDirectory 'final-ux/COLOR_STUDIO_PHASE1_SCREENSHOT_MANIFEST.json'
 $manifest=@(Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json)
 foreach($n in 1..18) {
  $scenario=$n.ToString('00')
  if(-not($manifest | Where-Object {$_.Scenario -eq $scenario -and $_.file -notmatch '-popup-'})) {throw "Missing scenario $scenario"}
 }
 foreach($item in $manifest) {
  $imagePath=Join-Path (Split-Path $manifestPath) $item.file
  if((Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash -ne $item.SHA256){throw "Changed image: $($item.file)"}
  if(-not $item.HasTarget -or $item.ProductSourceSha -notmatch '^[a-f0-9]{40}$'){throw "Invalid production provenance: $($item.file)"}
  # Human review result from this closure; do not infer interaction PASS from a still image.
  $item.FullSizeReview='INDIVIDUALLY_REVIEWED; viewer downscaled large frames; native-pixel QA remains PARTIAL'
  $item | Add-Member -Force NoteProperty ClosureRunId $runId
  $note=switch($item.Scenario) {
   '09' {'Post-reorder only; native drag insertion feedback remains unverified.'}
   '10' {'Undo result/control state; not a recording of keyboard or pointer interaction.'}
   '11' {'Captured UI shows processing although earlier state snapshot was idle; settled rendering is not proven.'}
   '14' {'Post-cancellation valid frame; not an in-progress stop-button capture.'}
   '18' {'Loaded-target logical 200% simulation; not physical-display DPI certification.'}
   '23' {'Loaded-target logical 200% simulation, including separate popup HWND.'}
   default {'Visual state reviewed; command/model regression is recorded separately.'}
  }
  $item | Add-Member -Force NoteProperty ReviewNote $note
 }
 $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8
 $runs=@()
 foreach($entry in @(
  @{File='core-closure.trx';Source='1324ce012058151b19fc2501746adb872c0083e5'},
  @{File='wpf-closure.trx';Source='1324ce012058151b19fc2501746adb872c0083e5'},
  @{File='phase1-recovery-c175ebe.trx';Source=$source},
  @{File='performance-final.trx';Source='7bddb78db242345fc2b47c3cbbb14fa5bd135ee5'}
 )) {
  $path=Join-Path $ResultsDirectory $entry.File
  [xml]$trx=Get-Content -Raw -LiteralPath $path
  $c=$trx.TestRun.ResultSummary.Counters
  $runs+= [ordered]@{
   File=$entry.File;ProductSourceSha=$entry.Source;OriginalRunId=$trx.TestRun.id
   SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
   Start=$trx.TestRun.Times.start;Finish=$trx.TestRun.Times.finish
   Total=[int]$c.total;Passed=[int]$c.passed;Failed=[int]$c.failed;Skipped=[int]$c.notExecuted
   Tests=@($trx.TestRun.Results.UnitTestResult | ForEach-Object {
    [ordered]@{Name=$_.testName;Outcome=$_.outcome;Duration=$_.duration;Output=[string]$_.Output.StdOut}
   })
  }
 }
 $summary=[ordered]@{
  RunId=$runId;GeneratedAt=[DateTime]::UtcNow.ToString('o');ProductSourceSha=$source
  SDKVersion='10.0.401';FixtureVersion='color-studio-still-life-v1'
  FinalStatus='BLOCKED';EvidenceConsistency='TRACEABLE_MULTI_REVISION; NOT SINGLE_BINARY_SIGNOFF'
  ScreenshotSources=@($manifest.ProductSourceSha | Sort-Object -Unique)
  ScreenshotCount=$manifest.Count;ScreenshotHashes='PASS';FullSizeReview='PARTIAL: individual review completed, large images downscaled by viewer'
  ScreenshotManifestSHA256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
  Build=[ordered]@{Configuration='Release x64';Result='PASS';Warnings=0;Errors=0;ProductVersion='2.3.0+'+$source;Command='dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -p:Platform=x64 -p:IncludeSourceRevisionInInformationalVersion=true --no-restore -v:minimal';RecordedFrom='Observed build output in this closure; not a rebuilt evidence commit'}
  Runs=$runs
  Performance=[ordered]@{Targets=30;Width=2400;Height=1600;ElapsedMs=64986;PeakWorkingSetMB=857.0;BaselineSeconds=59.7;BaselinePeakMB=771.5;TimeDeltaPercent=8.85;PeakDeltaPercent=11.08;Assessment='PARTIAL comparison: capture workload was concurrent; no controlled same-host historical peak comparison claimed'}
  PhysicalDpi='PENDING';ColorSpace3D='DEFERRED_TO_PHASE_2';HistoricalRC12='NOT_USED'
 }
 $summary | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'CLOSURE_RUN.json') -Encoding utf8
 Write-Output "RunId=$runId; screenshots=$($manifest.Count); hashes=PASS; source=$source"
} finally {Pop-Location}
