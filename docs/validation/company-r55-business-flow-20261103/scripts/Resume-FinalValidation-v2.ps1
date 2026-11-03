param([Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][string]$Sdk)
$ErrorActionPreference='Stop'
$Root=[IO.Path]::GetFullPath($Root)
$pub=Join-Path $Root 'Guangjing-BusinessFlow-Diagnostic-win-x64'
$hostDir=Join-Path $Root 'release-testhost'
$bindings=Get-Content (Join-Path $Root 'PUBLISHED_BINDING.json') -Raw|ConvertFrom-Json
foreach($binding in $bindings){$a=(Get-FileHash -LiteralPath (Join-Path $pub $binding.file)).Hash;$b=(Get-FileHash -LiteralPath (Join-Path $hostDir $binding.file)).Hash;if($a -ne $b -or $a -ne $binding.releaseSha256){throw "Published binding changed: $($binding.file)"}}
$env:DOTNET_ROOT=Split-Path $Sdk
$env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ACCEPTANCE='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=[IO.Path]::GetFullPath((Join-Path $Root 'UserData-release-final2'))
$env:KITAO_BUSINESS_FLOW_ROOT=[IO.Path]::GetFullPath((Join-Path $Root 'release-final2-host'))
if(Test-Path -LiteralPath $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT){throw 'Refusing to reuse an existing producer fixture; preserve it and choose a fresh run suffix.'}
$env:KITAO_PLANNING_CANVAS_ROOT=[IO.Path]::GetFullPath((Join-Path $Root 'release-final2-canvas'))
$env:KITAO_PLANNING3_OUTPUT=[IO.Path]::GetFullPath((Join-Path $Root 'release-final2-existing'))
function Run-Test([string]$Name,[string]$Filter){
  & $Sdk vstest (Join-Path $hostDir 'RAWSelectionAssistant.WpfTests.dll') "/TestCaseFilter:$Filter" "/Logger:trx;LogFileName=$Name.trx" "/ResultsDirectory:$Root/raw" *> (Join-Path $Root "raw/$Name.log")
  Get-Content -LiteralPath (Join-Path $Root "raw/$Name.log") -Tail 6
  if($LASTEXITCODE -ne 0){throw "Test group failed: $Name"}
}
Run-Test 'release-final2-business-host' 'FullyQualifiedName~ProjectBusinessFlowTests.CompleteAppProjectPlanningCalendarExpensesAndSummaryChain'
Run-Test 'release-final2-independent-business' 'FullyQualifiedName~ProjectBusinessFlowTests.IndependentAppReopenPreservesProjectBookingAndLedgerIdentities'
Run-Test 'release-final2-regressions' 'FullyQualifiedName~PlanningExpenseScopeTests|FullyQualifiedName~AllFifteenSectionsExportOneDocxAndSurviveSaveReopenWithoutMutatingReferences|FullyQualifiedName~LedgerReadFailureIsExplicitAndUiSummaryActionDoesNotReplaceOldExport|FullyQualifiedName~ExportDeclinesAProjectChangeDuringLedgerReadAndKeepsExistingFile|FullyQualifiedName~CancelLateGenerationAndProjectSwitchCannotPublish|FullyQualifiedName~FullShellKeepsPrimaryNavigationAndContentTemplates|FullyQualifiedName~AllFifteenSectionsAndModesConstructAtNarrowWidthsWithoutDesktop|FullyQualifiedName~RealShellAndHelpHaveNoRetiredTutorialTreeOrLegacyVisibleBrand'
Run-Test 'release-final2-existing-business' 'FullyQualifiedName~FifteenSectionsCreateEditLinkRecoverMoveAndReopenOneSyntheticPlan'
@{recordedAtShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'China Standard Time').ToString('o');verifiedBindings=$bindings.Count;testHostSha256=(Get-FileHash -LiteralPath (Join-Path $hostDir 'RAWSelectionAssistant.WpfTests.dll')).Hash;productVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $pub 'KitaoPhotoSelector.dll')).ProductVersion}|ConvertTo-Json|Set-Content (Join-Path $Root 'FINAL_RELEASE_TEST_IDENTITY.json')
