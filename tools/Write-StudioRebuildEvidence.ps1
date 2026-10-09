param([Parameter(Mandatory)][string]$SourceHead,
      [Parameter(Mandatory)][string]$ReleaseDirectory,
      [int]$RuntimeProcessId=0)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence=Join-Path $repo 'docs/evidence/color-studio-rebuild-2026-10-05/rebuild-confirmed-2026-10-08'
$tests=Join-Path $repo 'artifacts/rebuild-confirmed-2026-10-08/tests'
$release=[IO.Path]::GetFullPath((Join-Path $repo $ReleaseDirectory))
if(-not $release.StartsWith((Join-Path $repo 'artifacts/releases'),[StringComparison]::OrdinalIgnoreCase)){throw 'Release outside task artifacts.'}
$reports=@(Get-ChildItem -LiteralPath $tests -Filter '*.trx' | Sort-Object Name | ForEach-Object {
    [xml]$xml=Get-Content -LiteralPath $_.FullName
    $c=$xml.TestRun.ResultSummary.Counters
    [ordered]@{File=$_.Name; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash;
        Passed=[int]$c.passed; Failed=[int]$c.failed; Skipped=([int]$c.total-[int]$c.executed);
        StartedUtc=$xml.TestRun.Times.start; FinishedUtc=$xml.TestRun.Times.finish;
        Failures=@($xml.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'Failed' | ForEach-Object {$_.testName});
        Skips=@($xml.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted' | ForEach-Object {$_.testName})}
})
$reports | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'TEST_RESULTS.json') -Encoding utf8
$rows=Get-Content -LiteralPath (Join-Path $repo 'docs/evidence/color-studio-rebuild-2026-10-05/computer-use-review/RUNTIME_CHECKLIST.md') | Where-Object {$_ -match '^\| RT\d+\.\d+ \|'}
if($rows.Count -ne 95){throw 'Expected unchanged 95 historical checks.'}
$runtime=@('# Company final Release — 95-item runtime acceptance','','Status: NOT_READY_FOR_USER_RETEST; VisualApproved=false; UserVerified=false.','',
    'Historical r1 results remain in ../computer-use-review/RUNTIME_CHECKLIST.md. None transfers to this new binary. Historical 15 PASS / 7 FAIL / 61 PARTIAL / 12 NOT_RUN are provenance only.',
    '', 'Native input gate: Windows.Graphics.Capture FrameArrived timeout; text-only read succeeds but click reports coordinate input geometry is unavailable. Prior GetCursorPos access-denied and failed fallback captures remain recorded. PrintWindow only proves capture/startup, not clicks. No permission workaround or scripted test-host result is called native acceptance.',
    '', '| ID | Required step (historical wording retained) | Historical r1 | Company final runtime | Missing evidence |','|---|---|---|---|---|')
foreach($row in $rows){
    $cells=$row.Split('|');$id=$cells[1].Trim();$requirement=$cells[2].Trim();$historical=$cells[4].Trim()
    $status=if($id -eq 'RT01.1'){'PARTIAL'}else{'BLOCKED'}
    $missing=if($id -eq 'RT01.1'){'Isolated production EXE launched; hashes/version/startup checked. Complete repeated sessions and workflow still absent.'}else{'Production pointer/keyboard steps cannot be completed with current desktop input/capture failure. Automatic tests are separate.'}
    $runtime+="| $id | $requirement | $historical | $status | $missing |"
}
$runtime+=@('', 'Company totals: PASS=0; FAIL=0; PARTIAL=1; NOT_RUN=0; BLOCKED=94. This is a blocked new-binary walkthrough, not proof the software has no defects.',
    '', 'CU language/product scope remains PARTIAL where composed legacy messages or full audit are not complete; this code limitation is not relabeled an environment block. See ISSUE_MATRIX.md and REQUIREMENTS_TRACE.md.',
    '', 'RAW, private home working files and historical output files not available here are not software failures. No RAW native success or new UI export comparison is claimed.')
$runtime | Set-Content -LiteralPath (Join-Path $evidence 'RUNTIME_ACCEPTANCE.md') -Encoding utf8
$files=@(Get-ChildItem -LiteralPath $release -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{Path=[IO.Path]::GetRelativePath($release,$_.FullName).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$version=(Get-Item -LiteralPath (Join-Path $release 'KitaoPhotoSelector.dll')).VersionInfo
if($version.ProductVersion -notlike "*$SourceHead*"){throw 'ProductVersion does not match source.'}
$manifest=[ordered]@{SourceHead=$SourceHead;StartHead='f0f9cdd0c8faefc7030d76bb7ddde8459f810685';
    Branch='integration/pixel-tart-developer-preview';Configuration='Release';RuntimeIdentifier='win-x64';SelfContained=$true;
    StartupProject='src/RAWSelectionAssistant/RAWSelectionAssistant.csproj';Executable="$ReleaseDirectory/KitaoPhotoSelector.exe";
    ProductVersion=$version.ProductVersion;FileVersion=$version.FileVersion;SDK='10.0.302';OS='Windows 10.0.19045';
    IncludedRuntime='Microsoft.NETCore.App / Microsoft.WindowsDesktop.App 10.0.10';GeneratedUtc=[DateTime]::UtcNow.ToString('o');
    AcceptanceBuild=$false;UiReviewBuild=$false;InstallerBuilt=$false;VisualApproved=$false;UserVerified=$false;
    RuntimeProcessId=$RuntimeProcessId;Files=$files}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'RELEASE_MANIFEST.json') -Encoding utf8
$changed= & git -C $repo diff --name-only f0f9cdd0c8faefc7030d76bb7ddde8459f810685 $SourceHead
$list=@('# Files and types','','Source changes relative to protected START_HEAD. No binaries, private originals, runtime databases or raw TRX paths are published.','','| File | Type | Purpose |','|---|---|---|')
foreach($file in $changed){
    $type=if($file.StartsWith('tests/')){'test'}elseif($file.StartsWith('tools/')){'reproducible evidence tool'}elseif($file.EndsWith('.json')){'product localization'}elseif($file.EndsWith('.xaml')){'production UI layout'}else{'production logic/control'}
    $purpose=switch -Regex ($file){
        'ReferencePixelStatistics|ReferenceLook|RenderPipeline|BitmapRenderer' {'Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md';break}
        'Session|ZoomPan|Navigator' {'Compatible persisted layout/metadata and read-only image navigation';break}
        'Filmstrip|WorkspaceView' {'Existing workspace docking, grouping and photo strip presentation';break}
        'Localization|Resources/Studio|Language' {'Three-language resources and live/persisted presentation';break}
        'Levels|BalanceWheel|Numeric|ToolPanel' {'Graphical controls editing existing nodes/transactions';break}
        'Canvas' {'Real context commands, clipboard vs duplicate, capability visibility';break}
        'MainWindow' {'Global language entry and concise workflow copy';break}
        'Histogram' {'Visible true histogram peaks without artificial baseline';break}
        default {'Regression or scoped support; inspect named file in source commit'}
    }
    $list+="| $file | $type | $purpose |"
}
$list+=@('', 'Evidence files: MD=review/report, JSON=sanitized machine results and manifest, PNG=explicitly labeled capture. Raw test reports and release payload remain ignored local artifacts.','', 'See SCREENSHOT_INDEX.md for actual included images and capture limitations.')
$list | Set-Content -LiteralPath (Join-Path $evidence 'FILES_AND_TYPES.md') -Encoding utf8
