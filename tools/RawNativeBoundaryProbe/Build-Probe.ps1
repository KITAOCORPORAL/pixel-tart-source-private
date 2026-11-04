param([Parameter(Mandatory)][string]$Sdk,[Parameter(Mandatory)][string]$FrozenPublishedRoot,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve old builds; choose a new output directory'}
if(!$env:APPDATA){$env:APPDATA=Join-Path $env:USERPROFILE 'AppData/Roaming'}
$env:DOTNET_ROOT=Split-Path $Sdk
& $Sdk build (Join-Path $PSScriptRoot 'RawNativeBoundaryProbe.csproj') -c Release -o $OutputDirectory "-p:FrozenPublishedRoot=$FrozenPublishedRoot"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
foreach($n in @('raw_r.dll','lcms2.dll','jpeg8.dll','zlib1.dll')){Copy-Item -LiteralPath (Join-Path $FrozenPublishedRoot $n) -Destination (Join-Path $OutputDirectory $n)}
