param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Delivery,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Fresh output required; preserve existing evidence'}
New-Item -ItemType Directory -Path $Output|Out-Null
$env:DOTNET_ROOT=Split-Path $Dotnet
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'UserData'
$env:KITAO_SUMMARY_CANVAS_ROOT=Join-Path $Output 'samples'
$assembly=Join-Path $Delivery 'release-testhost/RAWSelectionAssistant.WpfTests.dll'
foreach($binding in (Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING.json') -Raw|ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash -ne $binding.releaseSha256){throw "Binding changed: $($binding.file)"}
}
& $Dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~PlanningSummaryCanvasSnapshotTests&FullyQualifiedName!~RenderExportedDocxPagesForVisualInspection' '/Logger:trx;LogFileName=summary.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'Snapshot summary tests failed'}
& $Dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~PlanningSummaryCanvasSnapshotTests.RenderExportedDocxPagesForVisualInspection' '/Logger:trx;LogFileName=render.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'DOCX product readback failed'}
