param(
 [Parameter(Mandatory)][string]$PreparedRoot,
 [Parameter(Mandatory)][string]$OutputRoot,
 [Parameter(Mandatory)][ValidateSet('XTransStableInputSeparateRgbV1')][string]$EnableCandidate
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Preserve existing candidate build; choose a new output root'}
$PreparedRoot=(Resolve-Path -LiteralPath $PreparedRoot).Path
$receipt=Get-Content -LiteralPath (Join-Path $PreparedRoot 'CANDIDATE_PREPARATION.json') -Raw | ConvertFrom-Json
if($receipt.version -ne $EnableCandidate -or !$receipt.candidateProof.workerInverseNormalizedExact -or $receipt.productIntegration){throw 'Not the reviewed isolated V1 source'}
$rootPrefix=$PreparedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
foreach($item in $receipt.files){
 $target=[IO.Path]::GetFullPath((Join-Path $PreparedRoot $item.path))
 if(!$target.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Source inventory escaped root'}
 if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $item.sha256){throw ('Candidate source drift: '+$item.path)}
}
if((Get-ChildItem -LiteralPath (Join-Path $PreparedRoot 'object') -Filter '*.obj' -ErrorAction SilentlyContinue).Count -or (Test-Path -LiteralPath (Join-Path $PreparedRoot 'bin/libraw.dll'))){throw 'Fresh uncompiled source root required'}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$OutputRoot=(Resolve-Path -LiteralPath $OutputRoot).Path
$tools=[ordered]@{};foreach($name in @('cl.exe','nmake.exe','dumpbin.exe')){$c=Get-Command $name -ErrorAction SilentlyContinue;$tools[$name]=if($c){$c.Source}else{$null}}
$report=[ordered]@{version=$EnableCandidate;status='PREFLIGHT';rawCalls=0;debuggerRuns=0;nativeBuildAttempts=0;tools=$tools;source=$PreparedRoot;preparationSha256=(Get-FileHash -LiteralPath (Join-Path $PreparedRoot 'CANDIDATE_PREPARATION.json')).Hash;flags='Original Makefile.msvc DLL target and COPT_OPT=/openmp, no optional component change';productionEquivalent=$false;reason='Candidate diagnostic source only; not product package'}
function Save-Receipt {$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'BUILD_STATUS.json') -Encoding utf8}
if(@($tools.Values | Where-Object { !$PSItem }).Count){$report.status='NOT_BUILT_REQUIRED_MSVC_TOOLCHAIN_MISSING';Save-Receipt;exit 3}
& cl.exe /Bv *> (Join-Path $OutputRoot 'compiler-version.log')
Push-Location -LiteralPath $PreparedRoot
try{
 $report.nativeBuildAttempts=1
 & nmake.exe /nologo /f Makefile.msvc 'bin\libraw.dll' 'COPT_OPT=/openmp' *> (Join-Path $OutputRoot 'native-build.log')
 if($LASTEXITCODE -ne 0){throw ('Candidate native build exit '+$LASTEXITCODE)}
} catch {$report.status='FAILED_PRESERVED_NO_AUTORETRY';$report.error=$_.ToString();Save-Receipt;throw} finally {Pop-Location}
$dll=Join-Path $PreparedRoot 'bin/libraw.dll'
& dumpbin.exe /headers /imports /exports $dll *> (Join-Path $OutputRoot 'native-pe.log')
if($LASTEXITCODE -ne 0){$report.status='FAILED_NATIVE_IDENTITY';Save-Receipt;throw 'Candidate PE inspection failed'}
$report.status='NATIVE_BUILT_PENDING_NO_RAW_HOST_SELFTEST_AND_NEW_TWO_PROCESS_BATCH'
$report.nativePath=$dll;$report.nativeSha256=(Get-FileHash -LiteralPath $dll).Hash
Save-Receipt
Write-Output 'Candidate diagnostic DLL built. No RAW was run. Freeze a new host/runtime manifest; preserve old consumed ledgers; do not launch an old debugger/window runner.'