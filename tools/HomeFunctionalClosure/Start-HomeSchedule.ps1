$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'home-context.json')){$c=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'home-context.json') -Raw | ConvertFrom-Json}
else{$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$c=Get-Content -LiteralPath (Join-Path $repo 'artifacts/home-20261115/CONTEXT.json') -Raw | ConvertFrom-Json}
& (Join-Path $c.package 'ScheduleService/Guangjing.Schedule.Server.exe') "--Schedule:Root=$($c.runtime)" "--Schedule:OwnerKeyFile=$($c.runtime)/schedule-owner.key" --Schedule:Port=17716 --Schedule:PublicBaseUrl=https://desktop-beiweiqu.taile9dfde.ts.net:8444 "--contentRoot=$($c.package)/ScheduleService"
