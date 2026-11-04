param([string]$Sdk,[string]$FrozenPublishedRoot,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve prior diagnostic builds; choose a new output directory'}
if(!$env:APPDATA){$env:APPDATA=Join-Path $env:USERPROFILE 'AppData/Roaming'}
$env:DOTNET_ROOT=Split-Path $Sdk
& $Sdk build (Join-Path $PSScriptRoot 'RawReopenStageProbe.csproj') -c Release -o $OutputDirectory "-p:FrozenPublishedRoot=$FrozenPublishedRoot"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
foreach($name in @('raw_r.dll','lcms2.dll','jpeg8.dll','zlib1.dll')){
 $source=Join-Path $FrozenPublishedRoot $name;$target=Join-Path $OutputDirectory $name
 if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $source).Hash){throw 'Existing native binary differs; choose new output directory'}}else{Copy-Item -LiteralPath $source -Destination $target}
}
exit 0
