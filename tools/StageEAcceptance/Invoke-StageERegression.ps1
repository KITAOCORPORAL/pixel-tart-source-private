param(
    [string]$Configuration = 'Debug',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\test-results\2.3.0-stage-e-matrix'
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$coreProject = Join-Path $repoRoot 'tests\RAWSelectionAssistant.Tests\RAWSelectionAssistant.Tests.csproj'
$wpfProject = Join-Path $repoRoot 'tests\RAWSelectionAssistant.WpfTests\RAWSelectionAssistant.WpfTests.csproj'

function Get-ScopedRelativePath([string]$BasePath, [string]$Path) {
    $resolvedBase = [IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped expected root: $resolvedPath"
    }
    return $resolvedPath.Substring($resolvedBase.Length + 1).Replace('\', '/')
}

function Invoke-Series {
    param(
        [string]$Name,
        [string]$Project,
        [int]$Rounds,
        [string]$Filter = '',
        [string[]]$ExtraArguments = @()
    )

    $runs = @()
    for ($round = 1; $round -le $Rounds; $round++) {
        $resultDirectory = Join-Path $OutputRoot $Name
        New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
        $trxName = "${Name}_${round}.trx"
        $arguments = @('test', $Project, '-c', $Configuration, '--no-restore', '--nologo', '--logger', "trx;LogFileName=$trxName", '--results-directory', $resultDirectory)
        if (-not [string]::IsNullOrWhiteSpace($Filter)) { $arguments += @('--filter', $Filter) }
        $arguments += $ExtraArguments
        $started = [DateTimeOffset]::Now
        & $dotnet @arguments
        $exitCode = $LASTEXITCODE
        $trxPath = Join-Path $resultDirectory $trxName
        if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $trxPath)) {
            throw "$Name round $round failed with exit code $exitCode."
        }
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        $run = [ordered]@{
            Round = $round
            Total = [int]$counters.total
            Passed = [int]$counters.passed
            Failed = [int]$counters.failed
            Skipped = [int]$counters.notExecuted
            DurationSeconds = [Math]::Round(([DateTimeOffset]::Now - $started).TotalSeconds, 3)
            Trx = Get-ScopedRelativePath $repoRoot $trxPath
        }
        if ($run.Failed -ne 0 -or $run.Skipped -ne 0 -or $run.Passed -ne $run.Total) {
            throw "$Name round $round did not pass cleanly."
        }
        $runs += $run
    }
    return $runs
}

$summary = [ordered]@{
    Configuration = $Configuration
    StartedAt = [DateTimeOffset]::Now.ToString('O')
    GitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
    Series = [ordered]@{}
}

$summary.Series.LutCore = @(Invoke-Series -Name 'lut-core' -Project $coreProject -Rounds 20 -Filter 'FullyQualifiedName~Version230StageDColorCoreTests')
$summary.Series.ClientWindow = @(Invoke-Series -Name 'client-window' -Project $wpfProject -Rounds 20 -Filter 'FullyQualifiedName~Version230StageDColorWpfTests')
$summary.Series.WatchFolder = @(Invoke-Series -Name 'watch-folder' -Project $coreProject -Rounds 10 -Filter 'FullyQualifiedName~Version230WatchFolder')
$summary.Series.Recovery = @(Invoke-Series -Name 'recovery' -Project $coreProject -Rounds 20 -Filter 'Name~RecoveredAssociation|Name~WaitForCompletion|Name~AwaitableProgress')
$summary.Series.CoreParallel = @(Invoke-Series -Name 'core-parallel' -Project $coreProject -Rounds 3)
$summary.Series.CoreNonParallel = @(Invoke-Series -Name 'core-nonparallel' -Project $coreProject -Rounds 3 -ExtraArguments @('--', 'MSTest.Parallelize.Workers=1'))
$summary.CompletedAt = [DateTimeOffset]::Now.ToString('O')
$summary.Passed = $true
$summaryPath = Join-Path $OutputRoot "specialized-$($Configuration.ToLowerInvariant()).json"
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 10
