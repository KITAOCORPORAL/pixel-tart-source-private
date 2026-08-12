[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('raw', 'batch', 'local-split', 'collage')]
    [string]$Mode,

    [Parameter(Mandatory)]
    [string]$Output,

    [Parameter(Mandatory)]
    [string]$Report,

    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]]$Sources
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dotnet = Join-Path (Split-Path -Parent $repoRoot) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }

& $dotnet run --project (Join-Path $PSScriptRoot 'PixelTart.RealFileValidation.csproj') `
    -c Release -- $Mode $Output $Report @Sources
if ($LASTEXITCODE -ne 0) { throw "Real file validation failed with exit code $LASTEXITCODE." }
