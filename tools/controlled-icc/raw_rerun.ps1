$ErrorActionPreference='Stop'
$dotnet='D:/AI AGENT/.dotnet/dotnet.exe'
$root='D:/AI AGENT/kitao-controlled-icc-20261015'
$fixtures=Get-Content "$root/docs/evidence/controlled-icc-20261015/01_FROZEN_CONTRACTS.json" -Raw | ConvertFrom-Json
$out="$root/artifacts/controlled-icc/raw"
New-Item -ItemType Directory -Path $out -Force | Out-Null
& $dotnet build "$root/tools/company-validation/RawInputProbe/RawInputProbe.csproj" -c Release --nologo *> "$out/build.log"
if($LASTEXITCODE -ne 0){throw 'Old decoder probe build failed'}
foreach($f in $fixtures.inputs){
 $hash=(Get-FileHash -LiteralPath $f.path -Algorithm SHA256).Hash
 if($hash -ne $f.sha256){throw 'Authorized input hash mismatch'}
 & $dotnet "$root/tools/company-validation/RawInputProbe/bin/Release/net10.0/RawInputProbe.dll" $f.path $f.sha256 "$out/$($f.file).legacy.json" *> "$out/$($f.file).legacy.log"
 & 'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/raw_stages_v3.exe' $f.path "$out/$($f.file).candidate.tif" 1 *> "$out/$($f.file).candidate.log"
 @{candidateExit=$LASTEXITCODE;sha256=$hash;after=(Get-FileHash -LiteralPath $f.path -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content "$out/$($f.file).identity.json"
}
