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
  Write-Host "Branch $((git branch --show-current).Trim())"
  Write-Host "HEAD $((git rev-parse HEAD).Trim())"
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
