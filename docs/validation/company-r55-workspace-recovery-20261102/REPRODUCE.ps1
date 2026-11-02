param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Delivery,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh output directory; prior failures must be preserved'}
New-Item -ItemType Directory -Path $Output|Out-Null
$env:DOTNET_ROOT=Split-Path $Dotnet
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'UserData'
$env:KITAO_WORKSPACE_RECOVERY_ROOT=Join-Path $Output 'recovery'
$assembly=Join-Path $Delivery 'release-testhost-final/RAWSelectionAssistant.WpfTests.dll'
foreach($binding in (Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING_FINAL.json') -Raw|ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash -ne $binding.releaseSha256){throw "Binding changed: $($binding.file)"}
}
& $Dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~WorkspaceSaveRecoveryTests&FullyQualifiedName!~IndependentProcessReopensRecoveredCanvasAndBothLibraryLeases' '/Logger:trx;LogFileName=recovery.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'Recovery failed'}
& $Dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~WorkspaceSaveRecoveryTests.IndependentProcessReopensRecoveredCanvasAndBothLibraryLeases' '/Logger:trx;LogFileName=independent.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'Independent recovery failed'}
