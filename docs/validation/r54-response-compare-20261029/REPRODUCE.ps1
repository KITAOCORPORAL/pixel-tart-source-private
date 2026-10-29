param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$EvidenceRoot,[string]$AuthorizedRawSource)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $root
try {
 & $Dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false
 if($LASTEXITCODE){throw 'Build failed'}
 $env:KITAO_STILL_WORKFLOW_ROOT=Join-Path $EvidenceRoot ('stills-'+[guid]::NewGuid().ToString('N'))
 $env:KITAO_R54_CLOSURE_VISUAL=Join-Path $EvidenceRoot ('visual-'+[guid]::NewGuid().ToString('N'))
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~NodeToolsAndStillsTests&FullyQualifiedName!~IndependentStill|FullyQualifiedName~R54ClosureUiTests' --results-directory $EvidenceRoot --logger 'trx;LogFileName=ReproduceWorkflow.trx'
 if($LASTEXITCODE){throw 'Workflow failed'}
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~IndependentStillMetadata' --results-directory $EvidenceRoot --logger 'trx;LogFileName=ReproduceIndependent.trx'
 if($LASTEXITCODE){throw 'Independent reopen failed'}
 if($AuthorizedRawSource){
  $env:KITAO_NODE_RESPONSE_SOURCE=$AuthorizedRawSource;$env:KITAO_NODE_RESPONSE_ROOT=Join-Path $EvidenceRoot ('command-'+[guid]::NewGuid().ToString('N'));$env:KITAO_NODE_RESPONSE_DEEP='1';$env:KITAO_NODE_RESPONSE_SAMPLES='10';$env:KITAO_NODE_RESPONSE_EXPORT='1';$env:KITAO_RAW_LOCAL_PROFILE='1'
  & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~ActualAuthorizedLargeSourceCommandLatency' --results-directory $EvidenceRoot --logger 'trx;LogFileName=ReproduceCommand.trx'
  if($LASTEXITCODE){throw 'Command benchmark failed'}
 }
} finally {Pop-Location}
# Process-only environment. This rebuild is a new diagnostic run, not the frozen package signoff.
