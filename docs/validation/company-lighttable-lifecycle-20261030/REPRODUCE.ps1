param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;Push-Location $root
try {
 & $Dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false
 if($LASTEXITCODE){throw 'Build failed'}
 $env:KITAO_LIGHTTABLE_ROOT=Join-Path $Output ('workflow-'+[guid]::NewGuid().ToString('N'))
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~ViewingLifecycleTests|FullyQualifiedName~LightTableWorkflowTests&FullyQualifiedName!~IndependentViewing&FullyQualifiedName!~AuthorizedTen' --results-directory $Output --logger 'trx;LogFileName=Workflow.trx'
 if($LASTEXITCODE){throw 'Workflow failed'}
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --filter 'FullyQualifiedName~IndependentViewingTableAndFilmReopen' --results-directory $Output --logger 'trx;LogFileName=Independent.trx'
 if($LASTEXITCODE){throw 'Independent reopen failed'}
} finally {Pop-Location}
# Does not scan private inputs or change persistent environment. A rebuilt run is diagnostic, not frozen-package signoff.
