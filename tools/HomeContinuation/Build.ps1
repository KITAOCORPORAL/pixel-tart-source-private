param([string]$ArtifactRoot,[string]$PackageRoot)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=(& git -C $repo log -1 --format=%H -- src tests).Trim()
if($LASTEXITCODE -ne 0 -or $source -notmatch '^[0-9a-f]{40}$'){throw 'Cannot resolve frozen product source'}
$changes=& git -C $repo status --porcelain -- src tests
if($changes){throw 'Commit product and test changes before building'}
if(!$ArtifactRoot){$ArtifactRoot=Join-Path $repo 'artifacts/home-resume-20261122'}
if(!$PackageRoot){$PackageRoot=Join-Path $env:LOCALAPPDATA ('Guangjing/Deliveries/HomeResume-'+$source.Substring(0,8)+'-20261122')}
if(Test-Path -LiteralPath $PackageRoot){throw 'Preserve existing build; supply a new package directory'}
New-Item -ItemType Directory -Path $ArtifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path $PackageRoot | Out-Null
$context=[ordered]@{productSource=$source;worktree=$repo;artifacts=[IO.Path]::GetFullPath($ArtifactRoot);package=[IO.Path]::GetFullPath($PackageRoot);clientTimeZone='Europe/Berlin';fullModuleDiagnostic=$true;newRawDecodes=0;forcedGC=$false}
$context|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $ArtifactRoot 'BUILD_CONTEXT.json') -Encoding utf8
$props=@('-p:Platform=x64','-p:WholeAppDiagnosticBuild=true','-p:PhotographerTrialBuild=false',('-p:InformationalVersion=2.3.0+'+$source),'-p:IncludeSourceRevisionInInformationalVersion=false',('-p:SourceRevisionId='+$source))
$components=@(@{Name='App';Project='RAWSelectionAssistant/RAWSelectionAssistant.csproj'},@{Name='SelectionService';Project='PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj'},@{Name='ScheduleService';Project='Guangjing.Schedule.Server/Guangjing.Schedule.Server.csproj'},@{Name='native-host';Project='Guangjing.BrowserCapture.NativeHost/Guangjing.BrowserCapture.NativeHost.csproj'})
foreach($component in $components){
 $log=Join-Path $ArtifactRoot ('BUILD_'+$component.Name+'.log')
 & dotnet publish (Join-Path $repo ('src/'+$component.Project)) -c Release -r win-x64 --self-contained true @props -v:q --nologo -o (Join-Path $PackageRoot $component.Name) *> $log
 if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 20;throw ('Build failed: '+$component.Name)}
 Write-Output ($component.Name+' published: '+$source)
}
