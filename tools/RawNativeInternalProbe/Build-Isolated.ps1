param([Parameter(Mandatory)][string]$PreparedRoot,[Parameter(Mandatory)][string]$BindingRoot,[Parameter(Mandatory)][string]$Sdk,[Parameter(Mandatory)][string]$Contract,[Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Preserve old build output'}
$PreparedRoot=(Resolve-Path -LiteralPath $PreparedRoot).Path;$OutputRoot=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputRoot)
New-Item -ItemType Directory $OutputRoot|Out-Null
& (Join-Path $PSScriptRoot 'Discover-Toolchain.ps1') -Output (Join-Path $OutputRoot 'TOOLCHAIN.json')
$t=Get-Content (Join-Path $OutputRoot 'TOOLCHAIN.json') -Raw|ConvertFrom-Json
$r=[ordered]@{status='PREFLIGHT';nativeCompilerInvocations=0;rawCalls=0;source='cccb97647fcee56801fa68231fa8a38aa8b52ef7';flags='Unchanged upstream Makefile.msvc DLL target; COPT_OPT=/openmp';productionEquivalent=$false;optionalComponents='No RawSpeed/DNGSDK/LCMS/JPEG opt-in; must not claim production-equivalent';missing=$t.missing;compiler=$t.commands.'cl.exe';sdk=$env:WindowsSdkDir;developerTarget=$env:VSCMD_ARG_TGT_ARCH}
function Save{$r|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputRoot 'BUILD_STATUS.json')}
if($t.missing.Count){$r.status='NOT_BUILT_REQUIRED_MSVC_TOOLCHAIN_UNAVAILABLE';Save;exit 3}
try{
 $prep=Get-Content (Join-Path $PreparedRoot 'PREPARATION.json') -Raw|ConvertFrom-Json
 if($prep.source -ne $r.source -or !$prep.originalByteRestoration){throw 'Unverified preparation'}
 $coverage=Get-Content -LiteralPath $Contract -Raw|ConvertFrom-Json
 if(!$coverage.staticContractComplete -or $coverage.preparationSha256 -ine (Get-FileHash (Join-Path $PreparedRoot 'PREPARATION.json')).Hash){throw 'Contract does not bind this prepared source'}
 foreach($f in (Get-Content (Join-Path $PreparedRoot 'FILES.json') -Raw|ConvertFrom-Json)){
  $p=[IO.Path]::GetFullPath((Join-Path $PreparedRoot $f.path));if(!$p.StartsWith($PreparedRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Inventory escaped source root'}
  if((Get-FileHash -LiteralPath $p).Hash.ToLowerInvariant() -cne $f.sha256){throw ('Prepared drift: '+$f.path)}
 }
 if((Get-ChildItem (Join-Path $PreparedRoot 'object') -Filter '*.obj' -ErrorAction SilentlyContinue).Count -or (Test-Path (Join-Path $PreparedRoot 'bin/libraw.dll'))){throw 'Fresh prepared root required; never retry in old build root'}
 & cl.exe /Bv *> (Join-Path $OutputRoot 'compiler-version.log')
 Push-Location $PreparedRoot
 try{$r.nativeCompilerInvocations=1;& nmake.exe /nologo /f Makefile.msvc 'bin\libraw.dll' 'COPT_OPT=/openmp' *> (Join-Path $OutputRoot 'native-build.log');if($LASTEXITCODE -ne 0){throw "Native build exit $LASTEXITCODE"}}finally{Pop-Location}
 $hostRoot=Join-Path $OutputRoot 'host';$env:DOTNET_ROOT=Split-Path $Sdk
 & $Sdk build (Join-Path $PSScriptRoot 'RawNativeInternalHost.csproj') -c Release -o $hostRoot "-p:BindingRoot=$BindingRoot" *> (Join-Path $OutputRoot 'host-build.log');if($LASTEXITCODE -ne 0){throw 'Managed build failed'}
 Copy-Item -LiteralPath (Join-Path $PreparedRoot 'bin/libraw.dll') -Destination (Join-Path $hostRoot 'raw_r.dll')
 & dumpbin.exe /headers /imports /exports (Join-Path $hostRoot 'raw_r.dll') *> (Join-Path $OutputRoot 'native-pe.log');if($LASTEXITCODE -ne 0){throw 'PE verification failed'}
 $r.nativeSha256=(Get-FileHash (Join-Path $hostRoot 'raw_r.dll')).Hash;$r.preparationSha256=(Get-FileHash (Join-Path $PreparedRoot 'PREPARATION.json')).Hash
 $r.status='NATIVE_BUILT_PENDING_MANIFEST_AND_NO_RAW_SELF_TEST';Save
 # Freeze with Freeze.mjs, then explicitly run --self-test-no-raw. Never run RAW here.
}catch{$r.status='FAILED_PRESERVED_NO_RETRY';$r.error=$_.ToString();Save;throw}
