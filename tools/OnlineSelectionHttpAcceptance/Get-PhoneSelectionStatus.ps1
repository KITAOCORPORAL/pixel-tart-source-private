param([string]$BaseUrl='http://127.0.0.1:17649', [string]$RunRoot='artifacts/online-selection-http/iphone13-safari')
$ErrorActionPreference='Stop'
$taskRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [System.IO.Path]::IsPathFullyQualified($RunRoot)) { $RunRoot=Join-Path $taskRoot $RunRoot }
$runner=Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows10.0.19041.0/win-x64/OnlineSelectionHttpAcceptance.exe'
& $runner phone-status $RunRoot $BaseUrl
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect phone selection status.' }
