[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot,

    [ValidateRange(64, 4096)]
    [int]$PerformanceWidth = 192,

    [ValidateRange(64, 4096)]
    [int]$PerformanceHeight = 128
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NormalizedDirectoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

function Assert-NewSafeOutputRoot {
    param([Parameter(Mandatory = $true)][string]$Path)
    $resolved = Get-NormalizedDirectoryPath -Path $Path
    $temporary = Get-NormalizedDirectoryPath -Path ([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temporary + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputRoot must be an explicit child directory of the system temporary directory.'
    }
    if (Test-Path -LiteralPath $resolved) {
        if ($null -ne (Get-ChildItem -LiteralPath $resolved -Force | Select-Object -First 1)) {
            throw 'OutputRoot must be new or empty. Existing files are never overwritten or deleted.'
        }
    }
    return $resolved
}

function Invoke-Runner {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][int]$Count,
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][string]$ResultPath
    )
    & $dotnet run --project $runnerProject -c Release --no-restore -- $Mode `
        --fixture-root $fixtureRoot `
        --database $DatabasePath `
        --result $ResultPath `
        --count $Count
    if ($LASTEXITCODE -ne 0) { throw "$Mode acceptance runner failed with exit code $LASTEXITCODE." }
}

$resolvedOutput = Assert-NewSafeOutputRoot -Path $OutputRoot
$toolRoot = $PSScriptRoot
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $toolRoot '..\..'))
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '..\..'))
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { $dotnet = 'dotnet' }
$runnerProject = Join-Path $toolRoot 'PixelTart.AssetLibrary.V16.AcceptanceRunner.csproj'
$fixtureRoot = Join-Path $resolvedOutput 'fixtures'
$resultRoot = Join-Path $resolvedOutput 'results'
$databaseRoot = Join-Path $resolvedOutput 'databases'
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
New-Item -ItemType Directory -Path $databaseRoot -Force | Out-Null

& (Join-Path $toolRoot 'New-AssetLibraryV16Fixtures.ps1') `
    -OutputRoot $fixtureRoot `
    -PerformanceCount 1000 `
    -PerformanceWidth $PerformanceWidth `
    -PerformanceHeight $PerformanceHeight | Out-Host

& $dotnet restore $runnerProject --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Acceptance runner restore failed with exit code $LASTEXITCODE." }

Invoke-Runner -Mode pipeline -Count 100 -DatabasePath (Join-Path $databaseRoot 'pipeline-100.db') -ResultPath (Join-Path $resultRoot 'pipeline-100.json')
Invoke-Runner -Mode pipeline -Count 1000 -DatabasePath (Join-Path $databaseRoot 'pipeline-1000.db') -ResultPath (Join-Path $resultRoot 'pipeline-1000.json')
Invoke-Runner -Mode cancellation -Count 3 -DatabasePath (Join-Path $databaseRoot 'cancellation.db') -ResultPath (Join-Path $resultRoot 'cancellation.json')

$summary = [ordered]@{
    schema = 'pixel-tart-asset-library-v16-acceptance-summary/v1'
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    synthetic_fixture_count = 1003
    color_management_reference_verified = $false
    raw_visual_proxy_verified = $false
    results = @(
        'results/pipeline-100.json'
        'results/pipeline-1000.json'
        'results/cancellation.json'
    )
}
[IO.File]::WriteAllText(
    (Join-Path $resolvedOutput 'acceptance-summary.json'),
    ($summary | ConvertTo-Json -Depth 6) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    OutputRoot = $resolvedOutput
    SummaryPath = Join-Path $resolvedOutput 'acceptance-summary.json'
    Pipeline100 = Join-Path $resultRoot 'pipeline-100.json'
    Pipeline1000 = Join-Path $resultRoot 'pipeline-1000.json'
    Cancellation = Join-Path $resultRoot 'cancellation.json'
}
