param([Parameter(Mandatory)][string]$PreparedRoot,[Parameter(Mandatory)][string]$BindingRoot,[Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Preserve all old outputs; require NEW output directory'}
$PreparedRoot=(Resolve-Path -LiteralPath $PreparedRoot).Path
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$r=[ordered]@{status='PREFLIGHT';utc=[DateTimeOffset]::UtcNow;fullRawDevelopments=0;tools=@{};failures=@();productionEquivalent=$false;optionalComponents='Upstream Makefile.msvc defaults: no extra RawSpeed/DNGSDK/LCMS/JPEG opt-in; NOT production-equivalent';flags='Upstream COPT plus COPT_OPT=/openmp; no thread env changes'}
foreach($n in @('cl.exe','nmake.exe','dumpbin.exe','dotnet.exe')){$c=Get-Command $n -ErrorAction SilentlyContinue;$r.tools[$n]=if($c){$c.Source}else{$null};if(!$c){$r.failures+="Missing tool: $n"}}
function Save{$r|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputRoot 'BUILD_STATUS.json') -Encoding utf8}
if($r.failures.Count){$r.status='NOT_BUILT_TOOLCHAIN_UNAVAILABLE';Save;Write-Output ($r|ConvertTo-Json -Depth 8);exit 3}
try{
 $receipt=Get-Content -LiteralPath (Join-Path $PreparedRoot 'PREPARATION.json') -Raw|ConvertFrom-Json
 if($receipt.commit -ne 'cccb97647fcee56801fa68231fa8a38aa8b52ef7'){throw 'Source commit mismatch'}
 foreach($item in (Get-Content -LiteralPath (Join-Path $PreparedRoot 'FILES.json') -Raw|ConvertFrom-Json)){
  $candidate=[IO.Path]::GetFullPath((Join-Path $PreparedRoot $item.path))
  if(!$candidate.StartsWith($PreparedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside prepared source'}
  if((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $item.sha256){throw "Prepared input drift: $($item.path)"}
 }
 if((Get-ChildItem -LiteralPath (Join-Path $PreparedRoot 'object') -Filter '*.obj' -File).Count -or (Test-Path -LiteralPath (Join-Path $PreparedRoot 'bin/libraw.dll'))){throw 'Use a fresh prepared tree; preserve all previous native outputs'}
 & cl.exe /Bv 2>&1 | Out-File (Join-Path $OutputRoot 'compiler-version.log')
 # The caller must enter a genuine x64 Developer shell. No vcvars/global env/OMP mutation here.
 if($env:VSCMD_ARG_TGT_ARCH -ne 'x64'){throw 'Require existing x64 MSVC Developer shell'}
 Push-Location $PreparedRoot
 try{& nmake.exe /nologo /f Makefile.msvc 'bin\libraw.dll' 'COPT_OPT=/openmp' 2>&1 | Tee-Object (Join-Path $OutputRoot 'native-build.log');if($LASTEXITCODE -ne 0){throw "Native build exit $LASTEXITCODE"}}finally{Pop-Location}
 $hostRoot=Join-Path $OutputRoot 'host'
 & dotnet.exe build (Join-Path $PSScriptRoot 'PreparationHost.csproj') -c Release -o $hostRoot "-p:BindingRoot=$BindingRoot" 2>&1 | Tee-Object (Join-Path $OutputRoot 'managed-build.log')
 if($LASTEXITCODE -ne 0){throw "Host build exit $LASTEXITCODE"}
 Copy-Item -LiteralPath (Join-Path $PreparedRoot 'bin/libraw.dll') -Destination (Join-Path $hostRoot 'raw_r.dll')
 & dumpbin.exe /headers /imports /exports (Join-Path $hostRoot 'raw_r.dll') 2>&1 | Out-File (Join-Path $OutputRoot 'native-pe.log')
 if($LASTEXITCODE -ne 0){throw 'PE inspection failed'}
 $r.nativeSha256=(Get-FileHash (Join-Path $hostRoot 'raw_r.dll') -Algorithm SHA256).Hash
 & dotnet.exe (Join-Path $hostRoot 'PreparationHost.dll') --self-test-no-raw (Join-Path $hostRoot 'raw_r.dll') (Join-Path $OutputRoot 'NO_RAW_SELF_TEST.json') 2>&1 | Tee-Object (Join-Path $OutputRoot 'no-raw-test.log')
 if($LASTEXITCODE -ne 0){throw "Native/ABI self-test exit $LASTEXITCODE"}
 $r.status='BUILT_AND_NO_RAW_SELF_TEST_COMPLETED_NOT_A_FIX';Save
}catch{$r.status='FAILED_PRESERVED_NO_AUTOMATIC_RETRY';$r.failures+=$_.ToString();Save;throw}
# Intentionally never starts a RAW decode or an application.
