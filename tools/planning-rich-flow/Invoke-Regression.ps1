param([string]$Dotnet='D:/AI AGENT/.dotnet/dotnet.exe',[string]$OutputRoot='artifacts/rich-flow/reproduced')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
 $output=[IO.Path]::GetFullPath($OutputRoot)
 New-Item -ItemType Directory -Force $output,(Join-Path $output 'temp') | Out-Null
 $env:TEMP=Join-Path $output 'temp';$env:TMP=$env:TEMP
 $env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $output 'isolated-runtime'
 $env:KITAO_RICH_FLOW_EVIDENCE=Join-Path $output 'screenshots'
 $env:KITAO_PLANNING_COMPANY_SCREENSHOTS=Join-Path $output 'company-states'
 $filter='FullyQualifiedName~PlanningRichFlowTests|FullyQualifiedName~PlanningFoundationWpfTests|FullyQualifiedName~PlanningCompanyFollowupTests|FullyQualifiedName~PlanningR5UiTests|FullyQualifiedName~BatchFiveDocumentTests|FullyQualifiedName~BatchThreeDocumentTests|FullyQualifiedName~BatchTwoDocumentWpfTests|FullyQualifiedName~StageVPlanningWpfTests|FullyQualifiedName~BookingContinuePlanningAndResponsiveToolsTests'
 & $Dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --filter $filter --results-directory (Join-Path $output 'tests') --logger 'trx;LogFileName=wpf.trx'
 if($LASTEXITCODE -ne 0){throw 'WPF regression failed; preserve its TRX.'}
 & $Dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64 --filter 'FullyQualifiedName~Planning|FullyQualifiedName~StageIIIReferenceLookAndShotTests|FullyQualifiedName~BatchTwoDocumentTests|FullyQualifiedName~Version220Document' --results-directory (Join-Path $output 'tests') --logger 'trx;LogFileName=core.trx'
 if($LASTEXITCODE -ne 0){throw 'Core regression failed; preserve its TRX.'}
} finally { Pop-Location }
