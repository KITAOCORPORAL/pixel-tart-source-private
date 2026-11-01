param(
 [Parameter(Mandatory=$true)][string]$Checkout,
 [Parameter(Mandatory=$true)][string]$Library,
 [Parameter(Mandatory=$true)][string]$TraceLibrary,
 [Parameter(Mandatory=$true)][string]$Resources,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$Python='python'
)
$ErrorActionPreference='Stop'
# Caller explicitly selects an actual company execution. Machine/GPU/driver are probed,
# never copied from home metadata. No GPU render starts until current build identity exists.
if(Test-Path -LiteralPath $Output){throw 'Output must be a NEW company handoff directory'}
$parent=Split-Path ([IO.Path]::GetFullPath($Output)) -Parent
if(-not(Test-Path -LiteralPath $parent -PathType Container)){throw 'Output parent must exist'}
New-Item -ItemType Directory -Path $Output | Out-Null
$identity=Join-Path $Output 'build-before-run.json'
& $Python (Join-Path $PSScriptRoot 'collect_build_identity.py') --checkout $Checkout --library $Library --trace-library $TraceLibrary --resources $Resources --output $identity
if($LASTEXITCODE -ne 0){throw 'Company build identity failed; no render executed'}
$run=Join-Path $Output 'run'
& $Python (Join-Path $PSScriptRoot 'run_diagnostic.py') --checkout $Checkout --library $Library --trace-library $TraceLibrary --resources $Resources --expected-resources (Join-Path $PSScriptRoot 'expected-resources.json') --fixtures (Join-Path $PSScriptRoot 'fixtures') --resolved (Join-Path $PSScriptRoot 'resolved-portraA.json') --device-role company --build-provenance $identity --output $run
if($LASTEXITCODE -ne 0){throw 'Company diagnostic failed; retain output/identity, no retry'}
& $Python (Join-Path $PSScriptRoot 'verify_run.py') $run --role company
if($LASTEXITCODE -ne 0){throw 'Independent company package validation failed'}
Write-Output "Company execution COMPLETE. Compare with sealed HOME package separately: $run"
