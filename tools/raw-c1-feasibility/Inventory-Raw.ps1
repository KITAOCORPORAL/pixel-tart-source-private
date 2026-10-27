param([string]$Root='N:/pixart/RAW/Real',[Parameter(Mandatory=$true)][string]$Output,[switch]$Recursive)
$ErrorActionPreference='Stop'
$here=$PSScriptRoot
$repo=Split-Path (Split-Path $here -Parent) -Parent
if(Test-Path -LiteralPath $Output){throw 'Use fresh inventory output path'}
$probeArgs=@($Root,$Output); if($Recursive){$probeArgs+='--recursive'}
& dotnet run --project (Join-Path $here 'MetadataProbe') -- @probeArgs
if($LASTEXITCODE -ne 0){throw 'Metadata inventory failed'}
