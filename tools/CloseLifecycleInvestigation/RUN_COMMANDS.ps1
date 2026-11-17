param([Parameter(Mandatory=$true)][string]$WorkspaceSdk,[Parameter(Mandatory=$true)][string]$DeliveryRoot,[string]$Round='review-replay')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if(Test-Path -LiteralPath (Join-Path $DeliveryRoot ('runs/'+$Round))){throw 'Preserve prior round; choose a fresh round name'}
$env:DOTNET_ROOT=Split-Path (Resolve-Path $WorkspaceSdk).Path
Push-Location $repo
try {
 & $WorkspaceSdk publish tools/CloseLifecycleInvestigation/CloseLifecycleInvestigation.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -p:InformationalVersion=2.3.0+e8180221937fa4abb578a16faacebe99a5d4ac1b -p:IncludeSourceRevisionInInformationalVersion=false -o (Join-Path $DeliveryRoot 'bin')
 if($LASTEXITCODE -ne 0){throw 'Diagnostic build failed'}
 & python (Join-Path $PSScriptRoot 'run_guarded.py') --exe (Join-Path $DeliveryRoot 'bin/CloseLifecycleInvestigation.exe') --root (Join-Path $DeliveryRoot 'runs') --round $Round
 if($LASTEXITCODE -ne 0){throw 'One or more expected observations were not obtained; inspect all preserved receipts'}
} finally {Pop-Location}
