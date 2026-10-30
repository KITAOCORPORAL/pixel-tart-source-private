param(
 [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$NaturalManifest,
 [string]$PreviousSeven
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$release=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
if(Test-Path -LiteralPath $Output){throw 'Use a new output directory; never overwrite an existing run'}
New-Item -ItemType Directory -Path $Output | Out-Null
Push-Location $repo
try {
 # Only test harness is built here. The frozen release directory is read-only.
 dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false
 if($LASTEXITCODE){throw 'Harness build failed'}
 $hostPath=Join-Path $Output 'bound-testhost'
 Copy-Item -LiteralPath 'tests/RAWSelectionAssistant.WpfTests/bin/Release/net10.0-windows10.0.19041.0' -Destination $hostPath -Recurse
 $bound=@()
 Get-ChildItem -LiteralPath $release -Recurse -File -Filter '*.dll' | ForEach-Object {
   $relative=[IO.Path]::GetRelativePath($release,$_.FullName);$target=Join-Path $hostPath $relative
   if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $_.FullName -Destination $target;$a=(Get-FileHash -LiteralPath $_.FullName).Hash;$b=(Get-FileHash -LiteralPath $target).Hash;if($a-ne$b){throw "Binding mismatch: $relative"};$bound+=@{file=$relative;sha256=$a}}
 }
 $bound | ConvertTo-Json | Set-Content (Join-Path $Output 'BINDING.json')
 $env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'runtime'
 $env:KITAO_HOME_R55_ROOT=Join-Path $Output 'home';$env:KITAO_R55_ROOT=Join-Path $Output 'r55';$env:KITAO_R55_FIX_ROOT=Join-Path $Output 'fix';$env:KITAO_LIGHTTABLE_ROOT=Join-Path $Output 'workflow';$env:KITAO_STAGE_IDENTITY=Join-Path $Output 'stage'
 $filter='FullyQualifiedName~HomeR55RefreshTests|FullyQualifiedName~R55ViewingTests&FullyQualifiedName!~IndependentR55|FullyQualifiedName~R55InteractionFixTests|FullyQualifiedName~ViewingLifecycleTests|FullyQualifiedName~LightTableWorkflowTests&FullyQualifiedName!~IndependentViewing&FullyQualifiedName!~AuthorizedTen|FullyQualifiedName~R54DevelopedStageTests&FullyQualifiedName!~IndependentStage|FullyQualifiedName~WholeAppDiagnosticPackageTests'
 dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') "/TestCaseFilter:$filter" '/Logger:trx;LogFileName=workflow.trx' "/ResultsDirectory:$Output"
 if($LASTEXITCODE){throw 'Affected workflow failed'}
 dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~IndependentR55LayoutReopen|FullyQualifiedName~IndependentViewingTableAndFilmReopen|FullyQualifiedName~IndependentStageReopen' '/Logger:trx;LogFileName=independent.trx' "/ResultsDirectory:$Output"
 if($LASTEXITCODE){throw 'Independent reopen failed'}
 if($NaturalManifest -or $PreviousSeven){
   if(!$NaturalManifest -or !$PreviousSeven -or !(Test-Path -LiteralPath $NaturalManifest) -or !(Test-Path -LiteralPath $PreviousSeven)){throw 'Both explicit authorized manifests must exist; never scan to replace missing inputs'}
   $env:KITAO_HOME_R55_MANIFEST=$NaturalManifest;$env:KITAO_HOME_R55_SEVEN=$PreviousSeven
   dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~HomeR55MeasurementsTests&FullyQualifiedName!~IndependentNaturalProjectReopen' '/Logger:trx;LogFileName=natural-local-only.trx' "/ResultsDirectory:$Output"
   if($LASTEXITCODE){throw 'Natural workflow failed'}
   dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~IndependentNaturalProjectReopen' '/Logger:trx;LogFileName=natural-independent.trx' "/ResultsDirectory:$Output"
   if($LASTEXITCODE){throw 'Natural reopen failed'}
 }
} finally {Pop-Location}
# No Browser/ComputerUse/WebView/CDP, no process killing, no input directory search.
# Natural output contains private paths/photos: keep it LOCAL, do not add it to Git or ZIP.
