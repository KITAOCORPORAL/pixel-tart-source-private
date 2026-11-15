$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$c=Get-Content -LiteralPath (Join-Path $repo 'artifacts/home-20261115/CONTEXT.json') -Raw | ConvertFrom-Json
$source=(& git -C $repo rev-parse HEAD).Trim()
$package=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Guangjing/Deliveries/FunctionalClosureHome-Integrated-'+$source.Substring(0,8))
New-Item -ItemType Directory -Force -Path $package | Out-Null
& dotnet publish (Join-Path $repo 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj') -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -p:IncludeSourceRevisionInInformationalVersion=true "-p:SourceRevisionId=$source" -o (Join-Path $package 'App') -p:WarningLevel=0 -v:q --nologo
if($LASTEXITCODE -ne 0){throw 'Full module publish failed'}
& dotnet publish (Join-Path $repo 'src/PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj') -c Release -r win-x64 --self-contained true "-p:SourceRevisionId=$source" -o (Join-Path $package 'SelectionService') -p:WarningLevel=0 -v:q --nologo
if($LASTEXITCODE -ne 0){throw 'Selection service publish failed'}
& dotnet publish (Join-Path $repo 'src/Guangjing.Schedule.Server/Guangjing.Schedule.Server.csproj') -c Release -r win-x64 --self-contained true "-p:SourceRevisionId=$source" -o (Join-Path $package 'ScheduleService') -p:WarningLevel=0 -v:q --nologo
if($LASTEXITCODE -ne 0){throw 'Schedule service publish failed'}
& dotnet publish (Join-Path $repo 'src/Guangjing.BrowserCapture.NativeHost/Guangjing.BrowserCapture.NativeHost.csproj') -c Release -r win-x64 --self-contained true "-p:SourceRevisionId=$source" -o (Join-Path $package 'native-host') -p:WarningLevel=0 -v:q --nologo
if($LASTEXITCODE -ne 0){throw 'Native host publish failed'}
& python (Join-Path $PSScriptRoot 'Seal-HomePackage.py') $package $source
if($LASTEXITCODE -ne 0){throw 'Home package binding failed'}
