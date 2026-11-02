param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Delivery,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh output directory; existing evidence is never reset'}
New-Item -ItemType Directory -Path $Output | Out-Null
$env:DOTNET_ROOT=Split-Path $Dotnet
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'UserData'
$env:PIXEL_TART_ACCEPTANCE='1'
$env:KITAO_COMPANY_WORKFLOW_ROOT=Join-Path $Output 'workflow'
$assembly=Join-Path $Delivery 'release-testhost/RAWSelectionAssistant.WpfTests.dll'
if(-not(Test-Path -LiteralPath $assembly)){throw 'Frozen release-bound testhost is required; do not substitute a new build'}
$bindings=Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING.json') -Raw | ConvertFrom-Json
foreach($binding in $bindings){
    $path=Join-Path $Delivery ($binding.host+'/'+$binding.file)
    if((Get-FileHash -LiteralPath $path).Hash -ne $binding.releaseSha256){throw "Binding differs: $path"}
}
foreach($method in @('CompleteAppCreateMixedCanvasSaveAndInsertFixedSnapshot','IndependentCompleteAppReopenEditAndManuallyUpdateSnapshot','ThirdCompleteAppReopenKeepsBothSnapshotIdentitiesAndLayerStates','BusyLibraryReportsInlineAndPreservesCurrentCanvas')){
    & $Dotnet vstest $assembly "/TestCaseFilter:FullyQualifiedName~CompanyCanvasWorkflowTests.$method" "/Logger:trx;LogFileName=$method.trx" "/ResultsDirectory:$Output"
    if($LASTEXITCODE){throw "Workflow failed: $method"}
}
