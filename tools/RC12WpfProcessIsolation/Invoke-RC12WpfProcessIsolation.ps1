[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [string]$ClassPattern = '*',
    [string]$IsolationRoot = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$dotnet = [IO.Path]::GetFullPath((Join-Path $repoRoot '..\..\.dotnet\dotnet.exe'))
$project = Join-Path $repoRoot 'tests\RAWSelectionAssistant.WpfTests\RAWSelectionAssistant.WpfTests.csproj'
$sourceRoot = Join-Path $repoRoot 'tests\RAWSelectionAssistant.WpfTests'
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$productDirty = @(& git -C $repoRoot status --porcelain -- src tests tools).Count -gt 0
$evidenceSource = if ($productDirty) { 'UNFROZEN_WORKTREE' } else { $sourceCommit }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot ('artifacts\rc12-wpf-process-isolation\' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'))
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:Platform=x64 --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'RC12 WPF isolation build failed.' }
}

$classes = Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.cs' | ForEach-Object {
    $source = Get-Content -LiteralPath $_.FullName -Raw
    if ($source -match '\[TestCategory\("P3Diagnostic"\)\]') { return }
    [regex]::Matches($source, '(?s)\[TestClass(?:Attribute)?\]\s*(?:public|internal)\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(?<name>[A-Za-z0-9_]+)') | ForEach-Object {
        $_.Groups['name'].Value
    }
} | Where-Object { $_ -like $ClassPattern } | Sort-Object -Unique
if (@($classes).Count -eq 0) { throw "No WPF fixtures matched '$ClassPattern'." }

$results = [Collections.Generic.List[object]]::new()
$fixtureIndex = 0
foreach ($className in $classes) {
    $fixtureIndex++
    $safeName = $className -replace '[^A-Za-z0-9_.-]', '_'
    if (-not [string]::IsNullOrWhiteSpace($IsolationRoot)) {
        $fixtureRoot = Join-Path ([IO.Path]::GetFullPath($IsolationRoot)) $safeName
        $env:PIXEL_TART_HUMAN_ACCEPTANCE = '1'
        $env:PIXEL_TART_ACCEPTANCE_ROOT = Join-Path $fixtureRoot 'data'
        $env:PIXEL_TART_CONTRACT_EVIDENCE = Join-Path $fixtureRoot 'contract'
        $env:PIXEL_TART_NAVIGATION_EVIDENCE = Join-Path $fixtureRoot 'navigation'
        $env:PIXEL_TART_WHOLE_APP_EVIDENCE = Join-Path $fixtureRoot 'whole-app'
        $env:PIXEL_TART_CONTRACT_SOURCE = $evidenceSource
        $env:PIXEL_TART_PRODUCT_SOURCE_SHA = $evidenceSource
    }
    $trxName = ('{0:000}-{1}.trx' -f $fixtureIndex, $safeName)
    $stdoutPath = Join-Path $OutputRoot ('{0:000}-{1}.stdout.txt' -f $fixtureIndex, $safeName)
    $stderrPath = Join-Path $OutputRoot ('{0:000}-{1}.stderr.txt' -f $fixtureIndex, $safeName)
    $arguments = @(
        'test', ('"' + $project + '"'), '-c', 'Release', '-p:Platform=x64', '--no-build', '--no-restore',
        '--filter', ('FullyQualifiedName~RAWSelectionAssistant.WpfTests.' + $className),
        '--results-directory', ('"' + $OutputRoot + '"'),
        '--logger', ('trx;LogFileName=' + $trxName),
        '--logger', 'console;verbosity=minimal'
    )
    $startedAt = [DateTimeOffset]::Now
    $process = Start-Process -FilePath $dotnet -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $endedAt = [DateTimeOffset]::Now
    $trxPath = Join-Path $OutputRoot $trxName
    $total = 0; $passed = 0; $failed = 0; $skipped = 0
    if (Test-Path -LiteralPath $trxPath) {
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
        $manager = New-Object Xml.XmlNamespaceManager($trx.NameTable)
        $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $counters = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $manager)
        if ($null -ne $counters) {
            $total = [int]$counters.total
            $passed = [int]$counters.passed
            $failed = [int]$counters.failed
            $executed = if ($null -ne $counters.executed) { [int]$counters.executed } else { $passed + $failed }
            $skipped = [Math]::Max([int]$counters.notExecuted, $total - $executed)
        }
    }
    $results.Add([ordered]@{
        fixture=$className; process_id=$process.Id; started_at=$startedAt.ToString('O'); ended_at=$endedAt.ToString('O')
        exit_code=$process.ExitCode; total=$total; passed=$passed; failed=$failed; skipped=$skipped
        trx=$trxPath; stdout=$stdoutPath; stderr=$stderrPath
    })
    $process.Dispose()
    Write-Progress -Activity 'RC12 WPF fixture isolation' -Status $className -PercentComplete ([int](100*$fixtureIndex/@($classes).Count))
}
Write-Progress -Activity 'RC12 WPF fixture isolation' -Completed

$testCount = 0; $passedCount = 0; $failedCount = 0; $skippedCount = 0
foreach ($fixture in $results) {
    $testCount += [int]$fixture['total']
    $passedCount += [int]$fixture['passed']
    $failedCount += [int]$fixture['failed']
    $skippedCount += [int]$fixture['skipped']
}
$manifest = [ordered]@{
    schema='pixel-tart-rc12-wpf-process-isolation/v1'; product_version='2.3.0-RC12'; source_commit=$evidenceSource; checkout_head=$sourceCommit; product_dirty=$productDirty
    process_per_fixture=$true; application_singleton_shared=$false; fixture_count=@($results).Count
    test_count=$testCount; passed_count=$passedCount; failed_count=$failedCount; skipped_count=$skippedCount
    failed_fixture_count=@($results | Where-Object { $_.exit_code -ne 0 -or $_.failed -ne 0 -or $_.skipped -ne 0 }).Count
    generated_at=[DateTimeOffset]::Now.ToString('O'); fixtures=$results
}
$manifestPath = Join-Path $OutputRoot 'rc12-wpf-process-isolation.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
[pscustomobject]$manifest | Select-Object product_version,source_commit,fixture_count,test_count,passed_count,failed_count,skipped_count,failed_fixture_count | ConvertTo-Json
if ($manifest.failed_fixture_count -ne 0) { throw "RC12 WPF process-isolated gate failed in $($manifest.failed_fixture_count) fixture(s). See $manifestPath" }
