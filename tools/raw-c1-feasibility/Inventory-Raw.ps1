param([string]$Root='N:/pixart/RAW/Real',[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$here=$PSScriptRoot
$repo=Split-Path (Split-Path $here -Parent) -Parent
if(Test-Path -LiteralPath $Output){throw 'Use fresh inventory output path'}
& dotnet run --project (Join-Path $here 'MetadataProbe') -- $Root $Output
if($LASTEXITCODE -ne 0){throw 'Metadata inventory failed'}
