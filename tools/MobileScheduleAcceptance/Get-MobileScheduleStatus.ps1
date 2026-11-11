$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime=Join-Path $taskRoot 'artifacts/mobile-schedule/final-runtime'
$runner=Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows10.0.19041.0/win-x64/MobileScheduleAcceptance.exe'
& $runner pc-readback $runtime http://127.0.0.1:17662
if($LASTEXITCODE -ne 0){throw 'PC calendar readback failed'}
Get-Content -LiteralPath (Join-Path $runtime 'pc-readback.json')
