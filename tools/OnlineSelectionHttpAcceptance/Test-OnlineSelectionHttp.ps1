param([string]$BaseUrl = 'http://127.0.0.1:17649', [string]$RunRoot = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $RunRoot) { $RunRoot = Join-Path $taskRoot ('artifacts/online-selection-http/acceptance-' + [Guid]::NewGuid().ToString('N')) }
& dotnet build (Join-Path $PSScriptRoot 'OnlineSelectionHttpAcceptance.csproj') -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -v:q
if ($LASTEXITCODE -ne 0) { throw 'Acceptance build failed.' }
$runner = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows10.0.19041.0/win-x64/OnlineSelectionHttpAcceptance.exe'
& $runner roundtrip $RunRoot $BaseUrl
if ($LASTEXITCODE -ne 0) { throw 'Real HTTP round trip failed. See local failure receipt.' }
& $runner reopen $RunRoot $BaseUrl
if ($LASTEXITCODE -ne 0) { throw 'New process recovery failed. See local failure receipt.' }
Write-Host "Verified receipts: $RunRoot"
