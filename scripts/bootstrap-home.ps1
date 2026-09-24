[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

Write-Host "Pixel Tart home bootstrap: $repo"
if ($env:OS -ne 'Windows_NT') { throw 'Windows is required for this WPF solution.' }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is required. Install from https://git-scm.com/download/win.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw 'The .NET 10 SDK is required. Install the official SDK from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again.'
}
$sdkText = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdkText -notmatch '^10\.0\.') {
  throw "A .NET 10 SDK is required (global.json pins 10.0.302 with latestPatch); detected '$sdkText'."
}
Write-Host "Git: $((git --version).Trim())"
Write-Host "SDK: $sdkText"
if ((git branch --show-current).Trim() -ne 'integration/pixel-tart-developer-preview') { throw 'Checkout integration/pixel-tart-developer-preview before bootstrapping.' }
dotnet restore RAWSelectionAssistant.sln --nologo

$required = @('RAWSelectionAssistant.sln','src/RAWSelectionAssistant/RAWSelectionAssistant.csproj','tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj','tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj','docs/PIXEL_TART_HOME_DEV_HANDOFF.md','scripts/verify-home-dev.ps1')
$missing = $required | Where-Object { -not (Test-Path $_) }
if ($missing) { throw "Required repository paths are missing: $($missing -join ', ')" }

Write-Host "Branch: $((git branch --show-current).Trim())"
Write-Host "HEAD: $((git rev-parse HEAD).Trim())"
Write-Host 'Bootstrap complete. Run scripts/verify-home-dev.ps1 for the bounded development gate.'
