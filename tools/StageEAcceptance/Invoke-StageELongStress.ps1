param(
    [ValidateRange(1, 240)][int]$Minutes = 60,
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$stressRoot = Join-Path $repoRoot 'artifacts\diagnostics\2.3.0\stage-e-stress'
$resolvedStressRoot = [IO.Path]::GetFullPath($stressRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $stressRoot 'final-result.json' }
$resolvedOutput = [IO.Path]::GetFullPath($Output)
if (-not $resolvedOutput.StartsWith($resolvedStressRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Stress evidence must remain under $resolvedStressRoot."
}
if (Test-Path -LiteralPath $resolvedOutput) { throw 'Stress evidence already exists; refusing to overwrite it.' }

$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnet build (Join-Path $PSScriptRoot 'StageEAcceptance.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $resolvedStressRoot | Out-Null
$executable = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows10.0.19041.0\win-x64\StageEAcceptance.exe'
& $executable --minutes $Minutes --output $resolvedOutput
exit $LASTEXITCODE
