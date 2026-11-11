param([int]$Port=17662,[string]$PublicBaseUrl='https://desktop-beiweiqu.taile9dfde.ts.net:8443',[string]$RuntimeRoot='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $RuntimeRoot){$RuntimeRoot=Join-Path $taskRoot 'artifacts/mobile-schedule/final-runtime'}
$package=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Guangjing/Deliveries/MobileScheduleV1-20261111'
$service=Join-Path $package 'ScheduleService/Guangjing.Schedule.Server.dll'
if(-not(Test-Path -LiteralPath $service)){throw "Published service missing: $service"}
& dotnet $service "--Schedule:Root=$RuntimeRoot" "--Schedule:OwnerKeyFile=$RuntimeRoot/schedule-owner.key" "--Schedule:Port=$Port" "--Schedule:PublicBaseUrl=$PublicBaseUrl" "--contentRoot=$package/ScheduleService"
exit $LASTEXITCODE
