param([Parameter(Mandatory=$true)][string]$Sdk)
$ErrorActionPreference='Stop'
$env:DOTNET_ROOT=Split-Path $Sdk
Set-Location 'E:/Kitao-Worktrees/root-cause-fixes-20261117'
$sha='cb8c1adcde14c7092d0bd22ca692d6073d9efa09'
git diff --quiet $sha HEAD -- src tests
if($LASTEXITCODE -ne 0){throw 'Frozen source differs'}
git diff --quiet $sha -- src tests
if($LASTEXITCODE -ne 0){throw 'Dirty product inputs'}
$package='E:/Kitao-Deliveries/root-cause-fixes-20261117/Guangjing-RootCause-cb8c1adc-Full-win-x64'
if(Test-Path -LiteralPath (Join-Path $package 'PACKAGE_FILES.json')){throw 'The frozen package already exists; do not overwrite a sealed delivery'}
$props=@('-p:Platform=x64','-p:WholeAppDiagnosticBuild=true','-p:PhotographerTrialBuild=false',('-p:InformationalVersion=2.3.0+'+$sha),'-p:IncludeSourceRevisionInInformationalVersion=false',('-p:SourceRevisionId='+$sha),'-p:RestoreConfigFile=E:/Kitao-Deliveries/company-functional-20261116/NuGet.Config')
$components=@(@{name='App';project='src/RAWSelectionAssistant/RAWSelectionAssistant.csproj'},@{name='SelectionService';project='src/PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj'},@{name='ScheduleService';project='src/Guangjing.Schedule.Server/Guangjing.Schedule.Server.csproj'},@{name='native-host';project='src/Guangjing.BrowserCapture.NativeHost/Guangjing.BrowserCapture.NativeHost.csproj'})
foreach($item in $components){& $Sdk publish $item.project -c Release -r win-x64 --self-contained true @props -o ($package+'/'+$item.name) *> ('E:/Kitao-Deliveries/root-cause-fixes-20261117/BUILD_'+$item.name+'.log');Write-Output ($item.name+'_exit='+$LASTEXITCODE);if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}}
