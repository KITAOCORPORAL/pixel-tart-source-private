# Build and test execution

SDK10.0.302, WindowsDesktop/Core10.0.10; global.json/dependencies not upgraded. Production startup WinExe `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`, `KitaoPhotoSelector.exe`; no installer/test-build substitutions.

| Command | Evidence / result |
|---|---|
| dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -o artifacts/releases/rebuild-confirmed-final-2026-10-09/publish/win-x64 -p:IncludeSourceRevisionInInformationalVersion=true -p:SourceRevisionId=44ec69d573901c8045e0aa11a13eb4d75de6ad44 | Restore and final Release publish succeeded; exact product version/287 payload hashes in manifest |
| dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Debug --no-restore | Final source succeeded,0 warnings/errors |
| dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release --no-restore | Final source succeeded,0 warnings/errors |
| dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Debug | core-post-review-final.trx:1578pass,0fail,6skip; no later Core changes |
| dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Debug --no-restore | wpf-final-source-full.trx:1509pass,0fail,11skip; before final tiny locale fix and evidence test; subsequent focused regression separately recorded |
| dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Debug --filter FullyQualifiedName~StudioLocalizationTests\|FullyQualifiedName~StudioRebuildVisualEvidenceTests --no-restore | locale-visual-final-settled.trx:7pass,0fail,0skip with explicit AUTO output env; resource key/summary sequence zhCN→en→zhTW→zhCN |
| dotnet test tests/PixelTart.ModularHarness.Tests/PixelTart.ModularHarness.Tests.csproj -c Debug | modular-post-review.trx:14pass,0fail,0skip |
| dotnet test tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj -c Debug | dpi-with-generated-evidence.trx:91pass,0fail,0skip; existing automated fixture contract, not new Release actual OS-DPI |
| dotnet test tests/PixelTart.NativeAcceptance/PixelTart.NativeAcceptance.csproj -c Debug | native-gate-final.trx:19pass,0fail,1skip. RealReleaseAppStartsAndOpensLibrary opt-in NOT executed; remaining tests are guard logic, not native interaction |

Final focused source44ec69d regression uses `FullyQualifiedName~Studio|FullyQualifiedName~Reference|FullyQualifiedName~ColorRange|FullyQualifiedName~CanvasContext|FullyQualifiedName~SingleClose`, Debug, no-build/no-restore. Result:168passed,0failed,6skipped; source-44ec69d-final-regression.trx. Exact hash/times in TEST_RESULTS.json. Tests are not summed across repeated overlapping runs.

Opt-in WPF snapshots: `PIXEL_TART_REBUILD_VISUAL_OUTPUT` points to isolated ignored automatic-studio output. Test uses real WorkspaceView, synthetic chart,3 DIP sizes ×5 categories ×4 render scales; this is not actual system scaling. Native input tools remain unavailable; do not run PowerShell UIA fallback in place of Computer Use.

Core skips require missing actual RAW corpus/performance opt-in/GPU conditions. WPF skips include authorized real JPEG/RAW, review sheets/whole route inventory and visual opt-in scenarios; tests have prerequisites, none deleted. Existing MSTest analyzer warnings remain in test builds. Failed initial zoom/curve/decode/histogram/layout tests plus missing-DPI-artifact failures remain in TEST_RESULTS.json. New evidence fixture failed before Measure/Arrange due absent visual descendants; fixed fixture initialization then passes without weakening assertions. Generated ui-guardian historical reports modified by tests were restored byte-content to HEAD, not committed.
