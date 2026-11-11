param([string]$RuntimeRoot='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $RuntimeRoot){$RuntimeRoot=Join-Path $taskRoot 'artifacts/mobile-schedule/final-runtime'}
$RuntimeRoot=[IO.Path]::GetFullPath($RuntimeRoot)
$package=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Guangjing/Deliveries/MobileScheduleV1-20261111'
$app=Join-Path $package 'App/KitaoPhotoSelector.exe'
if(-not(Test-Path -LiteralPath $app)){throw "Complete diagnostic App missing: $app"}
$identity=Get-Content -LiteralPath (Join-Path $RuntimeRoot 'identity.json') -Raw | ConvertFrom-Json
$id=[Guid]::Parse($identity.bookingId)
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=$RuntimeRoot
& $app --mobile-schedule "--booking-id=$id"
