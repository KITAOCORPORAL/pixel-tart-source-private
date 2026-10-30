param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Use a new output directory; never overwrite prior evidence'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;$release=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
New-Item -ItemType Directory -Path $Output | Out-Null
Push-Location $repo
try {
 dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false
 if($LASTEXITCODE){throw 'Harness build failed'}
 $hostPath=Join-Path $Output 'bound-testhost';Copy-Item -LiteralPath 'tests/RAWSelectionAssistant.WpfTests/bin/Release/net10.0-windows10.0.19041.0' -Destination $hostPath -Recurse
 $binding=@();Get-ChildItem -LiteralPath $release -Recurse -File -Filter '*.dll' | ForEach-Object {$rel=[IO.Path]::GetRelativePath($release,$_.FullName);$target=Join-Path $hostPath $rel;if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $_.FullName -Destination $target;$a=(Get-FileHash -LiteralPath $_.FullName).Hash;$b=(Get-FileHash -LiteralPath $target).Hash;if($a-ne$b){throw "Binding mismatch: $rel"};$binding+=@{file=$rel;sha256=$a}}};$binding | ConvertTo-Json | Set-Content (Join-Path $Output 'BINDING.json')
 $env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'runtime';$env:KITAO_PLANNING_CANVAS_ROOT=Join-Path $Output 'planning-canvas';$env:KITAO_CANVAS_PRODUCT=Join-Path $Output 'canvas';$env:KITAO_CANVAS_PRODUCT_REOPEN=$env:KITAO_CANVAS_PRODUCT
 $filter='FullyQualifiedName~PlanningCanvasConsistencyTests&FullyQualifiedName!~IndependentReopenPreservesPngWithoutReadingOfflineCanvasSources|FullyQualifiedName~PlanningOwnedCanvasTests&FullyQualifiedName!~CreateDistinctNativeSyntheticPackages|FullyQualifiedName~CanvasProductClosureWpfTests&FullyQualifiedName!~IndependentProcessReopen|FullyQualifiedName~BatchTwoDocumentWpfTests|FullyQualifiedName~PlanningFoundationWpfTests|FullyQualifiedName~CanvasObjectTransformTests|FullyQualifiedName~CanvasSelectionOperationTests|FullyQualifiedName~CanvasTransformOperationTests|FullyQualifiedName~CanvasSurfaceInteractionTests|FullyQualifiedName~WholeAppDiagnosticPackageTests'
 dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') "/TestCaseFilter:$filter" '/Logger:trx;LogFileName=workflow.trx' "/ResultsDirectory:$Output"
 if($LASTEXITCODE){throw 'Affected regression failed'}
 dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~IndependentReopenPreservesPngWithoutReadingOfflineCanvasSources|FullyQualifiedName~IndependentProcessReopenAndExportPixelsMatch' '/Logger:trx;LogFileName=independent.trx' "/ResultsDirectory:$Output"
 if($LASTEXITCODE){throw 'Independent reopen failed'}
 # Host starts App in its own isolated process; not ComputerUse/native mouse acceptance.
 $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Output 'host-runtime';$env:KITAO_PLANNING_CANVAS_ROOT=Join-Path $Output 'host'
 dotnet vstest (Join-Path $hostPath 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~PlanningCanvasHostedTests' '/Logger:trx;LogFileName=host.trx' "/ResultsDirectory:$Output"
 if($LASTEXITCODE){throw 'Complete hosted product entry failed'}
} finally {Pop-Location}
# Run numerical references separately with Python/Pillow/numpy/ffmpeg already installed:
# python verify_numerical.py <planning-canvas directory> --output <new numeric output>
# No Browser/WebView/CDP, no source-directory scanning, no old worktree mutation.
