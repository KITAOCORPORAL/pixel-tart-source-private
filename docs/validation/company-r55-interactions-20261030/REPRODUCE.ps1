param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop';$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;Push-Location $repo
try {
 & $Dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false
 if($LASTEXITCODE){throw 'Build failed'}
 $id=[guid]::NewGuid().ToString('N');$env:KITAO_R55_FIX_ROOT=Join-Path $Output ('fix-'+$id);$env:KITAO_R55_ROOT=Join-Path $Output ('r55-'+$id);$env:KITAO_LIGHTTABLE_ROOT=Join-Path $Output ('film-'+$id);$env:KITAO_STAGE_IDENTITY=Join-Path $Output ('stage-'+$id)
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~R55InteractionFixTests|FullyQualifiedName~ViewingLifecycleTests|FullyQualifiedName~R55ViewingTests&FullyQualifiedName!~IndependentR55|FullyQualifiedName~LightTableWorkflowTests&FullyQualifiedName!~IndependentViewing&FullyQualifiedName!~AuthorizedTen|FullyQualifiedName~R54DevelopedStageTests&FullyQualifiedName!~IndependentStage' --logger 'trx;LogFileName=ReproduceWorkflow.trx' --results-directory $Output
 if($LASTEXITCODE){throw 'Workflow failed'}
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~IndependentR55LayoutReopen|FullyQualifiedName~IndependentViewingTableAndFilmReopen|FullyQualifiedName~IndependentStageReopen' --logger 'trx;LogFileName=ReproduceIndependent.trx' --results-directory $Output
 if($LASTEXITCODE){throw 'Independent reopen failed'}
} finally {Pop-Location}
# Process-only variables. This is a new diagnostic build, not the frozen ZIP replay.
# No directory scan for user photos or external engine/source execution.
