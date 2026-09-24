[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo
$failed = $false
function Gate([string]$name, [scriptblock]$action) {
  Write-Host "== $name =="
  try { & $action; if ($LASTEXITCODE -ne 0) { throw "exit code $LASTEXITCODE" }; Write-Host "PASS: $name" -ForegroundColor Green }
  catch { Write-Host "FAIL: $name - $($_.Exception.Message)" -ForegroundColor Red; $script:failed = $true }
}

Gate 'Windows and .NET 10 SDK' {
  if ($env:OS -ne 'Windows_NT') { throw 'Windows required.' }
  $v = (dotnet --version).Trim()
  if ($v -notmatch '^10\.0\.') { throw "Detected $v; .NET 10 required." }
  Write-Host "SDK $v"
}
Gate 'Git identity and clean state' {
  $branch = (git branch --show-current).Trim()
  if ($branch -ne 'integration/pixel-tart-developer-preview') { throw "Unexpected branch: $branch" }
  $origin = (git remote get-url origin).Trim()
  if ($LASTEXITCODE -ne 0 -or $origin -notmatch '^(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)KITAOCORPORAL/pixel-tart-source-private(?:\.git)?$') { throw 'Origin must be the Pixel Tart private GitHub repository without embedded credentials.' }
  git fetch origin --prune
  if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin; verify private repository access.' }
  $localHead = (git rev-parse HEAD).Trim()
  $remoteHead = (git rev-parse origin/integration/pixel-tart-developer-preview).Trim()
  if ($LASTEXITCODE -ne 0 -or $localHead -ne $remoteHead) { throw "Local HEAD $localHead differs from origin/integration/pixel-tart-developer-preview $remoteHead" }
  Write-Host "Branch $branch"
  Write-Host "HEAD $localHead (matches origin)"
  $status = git status --short
  if ($status) { Write-Host $status; throw 'Working tree is not clean.' }
}
Gate 'Restore' { dotnet restore RAWSelectionAssistant.sln --nologo }
Gate 'Release x64 product build' { dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -p:Platform=x64 --no-restore --nologo }
Gate 'Color Studio critical tests' { dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ColorStudio' --nologo }
Gate 'Color Studio WPF critical tests' { dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~BatchExportProcessedPixelsTests|FullyQualifiedName~ColorStudioStateClosureTests|FullyQualifiedName~ReferenceWorkspaceWideRatioTests' --blame-hang-timeout 120s --nologo }
Gate 'Photography critical regression' { dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PhotographyInteractionClosureTests' --nologo }

if ($failed) { throw 'Home development verification failed.' }
Write-Host 'HOME DEVELOPMENT ENVIRONMENT: READY' -ForegroundColor Green
