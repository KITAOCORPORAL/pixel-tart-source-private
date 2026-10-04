$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$runs = @()
$allResults = @()
foreach ($entry in @(
    @{Name='Core full';File='core-final.trx';Head='4315bc9007c6087b185d214917fd8ef36daea545';Note='Core unchanged since 4315bc9; final production source dc2ba3d'},
    @{Name='WPF full serial';File='wpf-r9.trx';Head='dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7';Note='Final full serial regression; 8m23s; normal testhost exit'},
    @{Name='DPI';File='dpi-final.trx';Head='4315bc9007c6087b185d214917fd8ef36daea545';Note='Automated contract only; old current-run-visual SourceHead de4c91a, not final runtime proof'}
)) {
    $path = Join-Path $root ('tests/' + $entry.File)
    [xml]$trx = Get-Content -LiteralPath $path -Raw
    $counts = $trx.TestRun.ResultSummary.Counters
    $runs += [ordered]@{Name=$entry.Name;SourceHead=$entry.Head;Passed=[int]$counts.passed;Failed=[int]$counts.failed;Skipped=([int]$counts.total-[int]$counts.executed);Total=[int]$counts.total;Outcome=$trx.TestRun.ResultSummary.outcome;Note=$entry.Note;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    $names = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { $names[[string]$test.id] = [string]$test.TestMethod.className }
    foreach ($result in $trx.TestRun.Results.UnitTestResult) { $allResults += [pscustomobject]@{Name=($names[[string]$result.testId] + '.' + $result.testName);Outcome=[string]$result.outcome;Run=$entry.Name} }
}
$categories = @()
foreach ($category in @(
    @{Name='Asset Library';Pattern='AssetLibrary|OrganizationRename|SidebarRename'},
    @{Name='Color Studio';Pattern='ColorStudio|ReferenceWorkspace|RuntimeCorrectionWpf'},
    @{Name='Reference Match';Pattern='ReferenceMatch|MatchV[34]|ReferenceColor|BatchExport'},
    @{Name='3D';Pattern='ColorSpace|Cloud|Oklab|Mapping'},
    @{Name='Free Canvas';Pattern='Canvas'},
    @{Name='Guardian';Pattern='Guardian'}
)) {
    $selected = @($allResults | Where-Object Name -Match $category.Pattern)
    $categories += [ordered]@{Name=$category.Name;Pattern=$category.Pattern;Passed=@($selected | Where-Object Outcome -EQ 'Passed').Count;Failed=@($selected | Where-Object Outcome -EQ 'Failed').Count;Skipped=@($selected | Where-Object Outcome -EQ 'NotExecuted').Count;Note='Overlapping subset of full runs, not additive'}
}
[ordered]@{SourceHead='dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7';Runs=$runs;Categories=$categories;VisualApproved=$false;UserVerified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'test-summary.json') -Encoding utf8
