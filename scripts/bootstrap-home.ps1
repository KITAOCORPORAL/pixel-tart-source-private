[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

Write-Host "Pixel Tart home bootstrap: $repo"
if ($env:OS -ne 'Windows_NT') { throw 'Windows is required for this WPF solution.' }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is required. Install from https://git-scm.com/download/win.' }
if ((git rev-parse --show-toplevel).Trim() -ne ($repo -replace '\\','/')) {
  throw 'Run this script from a Pixel Tart repository checkout.'
}
$origin = (git remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or $origin -notmatch '^(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)KITAOCORPORAL/pixel-tart-source-private(?:\.git)?$') {
  throw 'The origin remote must point to KITAOCORPORAL/pixel-tart-source-private on GitHub (HTTPS or SSH, without embedded credentials).'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw 'The .NET 10 SDK is required. Install the official SDK from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again.'
}
$sdkText = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdkText -notmatch '^10\.0\.') {
  throw "A compatible .NET 10 SDK is required (global.json baseline 10.0.302, rollForward latestFeature); detected '$sdkText'."
}
Write-Host "Git: $((git --version).Trim())"
Write-Host "SDK: $sdkText"
Write-Host 'Origin: KITAOCORPORAL/pixel-tart-source-private'
if ((git branch --show-current).Trim() -ne 'integration/pixel-tart-developer-preview') { throw 'Checkout integration/pixel-tart-developer-preview before bootstrapping.' }

$required = @('global.json','RAWSelectionAssistant.sln','src/RAWSelectionAssistant/RAWSelectionAssistant.csproj','tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj','tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj','docs/PIXEL_TART_HOME_DEV_HANDOFF.md','docs/CODEX_RESUME_PROMPT.md','scripts/verify-home-dev.ps1')
$missing = $required | Where-Object { -not (Test-Path $_) }
if ($missing) { throw "Required repository paths are missing: $($missing -join ', ')" }
dotnet restore RAWSelectionAssistant.sln --nologo
if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }

Write-Host "Branch: $((git branch --show-current).Trim())"
Write-Host "HEAD: $((git rev-parse HEAD).Trim())"
Write-Host 'Bootstrap complete. Run scripts/verify-home-dev.ps1 for the bounded development gate.'
