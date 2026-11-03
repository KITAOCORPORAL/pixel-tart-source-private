param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Delivery,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Use fresh output; preserve old failures'}
New-Item -ItemType Directory -Path $Output|Out-Null
$env:DOTNET_ROOT=Split-Path $Dotnet
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'UserData'
$env:KITAO_SUMMARY_CANVAS_ROOT=Join-Path $Output 'feedback'
$assembly=Join-Path $Delivery 'final-testhost/RAWSelectionAssistant.WpfTests.dll'
foreach($binding in (Get-Content (Join-Path $Delivery 'FINAL_BINDING.json') -Raw|ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash -ne $binding.releaseSha256){throw "Binding differs: $($binding.file)"}
}
& $Dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~MissingRichAndLightRevisionLinksWarnInPlaceWithoutExportingStaleRtfImages|FullyQualifiedName~DeferredSnapshotWarningsMatchPlaceholdersReturnedWarningsAndDocxSummary|FullyQualifiedName~ExplicitRichBlockAndLightPlacementUseFrozenRevisionInsteadOfEmbeddedStaleImage' '/Logger:trx;LogFileName=feedback.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'Feedback regression failed'}
