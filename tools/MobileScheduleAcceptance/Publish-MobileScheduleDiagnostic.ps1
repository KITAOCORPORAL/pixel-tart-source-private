param([string]$RuntimeRoot='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $RuntimeRoot){$RuntimeRoot=Join-Path $taskRoot 'artifacts/mobile-schedule/final-runtime'}
$RuntimeRoot=[IO.Path]::GetFullPath($RuntimeRoot)
$package=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Guangjing/Deliveries/MobileScheduleV1-20261111'
New-Item -ItemType Directory -Force -Path $package | Out-Null
$source=(& git -C $taskRoot rev-parse HEAD).Trim()
& dotnet publish (Join-Path $taskRoot 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj') -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -p:IncludeSourceRevisionInInformationalVersion=true "-p:SourceRevisionId=$source" -o (Join-Path $package 'App') -v:q
if($LASTEXITCODE -ne 0){throw 'Complete diagnostic App publish failed'}
& dotnet publish (Join-Path $taskRoot 'src/Guangjing.Schedule.Server/Guangjing.Schedule.Server.csproj') -c Release "-p:SourceRevisionId=$source" -o (Join-Path $package 'ScheduleService') -v:q
if($LASTEXITCODE -ne 0){throw 'Schedule service publish failed'}
& dotnet publish (Join-Path $taskRoot 'src/Guangjing.BrowserCapture.NativeHost/Guangjing.BrowserCapture.NativeHost.csproj') -c Release -r win-x64 --self-contained true "-p:SourceRevisionId=$source" -o (Join-Path $package 'native-host') -v:q
if($LASTEXITCODE -ne 0){throw 'Preserved browser native host publish failed'}
& python (Join-Path $PSScriptRoot 'Write-DiagnosticPackage.py') $package $RuntimeRoot $source $taskRoot
if($LASTEXITCODE -ne 0){throw 'Package binding generation failed'}
