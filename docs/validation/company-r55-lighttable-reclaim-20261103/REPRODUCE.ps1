param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Delivery,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Fresh output required; preserve old evidence'}
New-Item -ItemType Directory -Path $Output|Out-Null
$env:DOTNET_ROOT=Split-Path $Dotnet
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'UserData'
$env:KITAO_LIGHTTABLE_RECLAIM_ROOT=Join-Path $Output 'interactions'
foreach($binding in (Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING.json') -Raw|ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash -ne $binding.releaseSha256){throw "Binding differs: $($binding.file)"}
}
& $Dotnet vstest (Join-Path $Delivery 'release-testhost/RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~LighttableReclaimTests' '/Logger:trx;LogFileName=interactions.trx' "/ResultsDirectory:$Output"
if($LASTEXITCODE){throw 'Interaction repro failed'}
