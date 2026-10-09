# Confirmed rebuild — 2026-10-08

Status: NOT_READY_FOR_USER_RETEST — code repaired and Release launched; native walkthrough incomplete

VisualApproved=false; UserVerified=false.

## Protected starting point

- Company START_HEAD and fetched REMOTE_HEAD: `f0f9cdd0c8faefc7030d76bb7ddde8459f810685`.
- Branch: `integration/pixel-tart-developer-preview`. Starting tree clean.
- Actual checkout verified with Git; no reset, clean, force push, or main merge.
- Existing company Release SourceHead: `1a9a53913665490298e6de1b5656a33d0efc934b`; the starting HEAD is documentation, **not** this binary's source.
- SDK: 10.0.302; existing global.json unchanged. Windows 10 build 19045, desktop runtime 10.0.10.
- Repository public: no private photographs, customer content, original annotated private screenshots or personal paths may be committed.

## Inputs and scope

The user explicitly confirmed execution of Pixel_Tart_CODEX_20261008.md: A01–A15, B01–B16, C01–C10. Historical six CU repairs are links, not a reduced scope.

Eleven locally available annotated images were visually opened. Five are candidates without `(2)` in the filename: 3.png, 4.png, 5.png, 8.png, 9.png. Exact equivalence awaits clarification. Exact historical image(20261005-022726).png and C1 auxiliary-area reference were not found. Do not claim these read or invent their annotations.

## Evidence policy correction

Keep the previous company repair evidence unchanged. Its classification of unexecuted checks as FAIL is superseded by the confirmed instruction: PASS=executed and correct; FAIL=executed defect; PARTIAL=incomplete coverage; NOT_RUN=not executed; BLOCKED=external obstacle. Automatic results and native interaction results remain separate.

## First reproduced defect

`ColorStudioZoomPanTests.LargeSourceWheelZoomDoesNotJumpToTwentyFivePercentOrStickThere` fails against the starting production code: expected .0896 after one wheel step from .08; actual .25. Baseline TRX is kept in ignored artifacts/rebuild-confirmed-2026-10-08/tests/zoom-baseline.trx. Other six zoom tests pass. This establishes a regression test before the A13 repair, not native acceptance.

The current source already notifies CurrentReferencePath after populating references. A11 investigation must continue into decoding, actual selection and presentation; do not reimplement that existing notification.

## Native baseline

The existing company Release main window is running and its accessibility tree is readable. Windows.Graphics.Capture previously timed out with ScreenshotFrameArrived. Screenshot capture is not a mouse/keyboard acceptance result. Public synthetic fixtures exist in isolated ignored inputs; private photos visible in the existing workspace must not enter public evidence.

## Current delivery / review entry

Final production SourceHead / Release SourceHead: `44ec69d573901c8045e0aa11a13eb4d75de6ad44`; main repair source commit `76057fd462bfadad5416829a383540eed9c9f820`. Evidence commit is the later docs-only commit containing this directory (resolve with `git log -1 --format=%H -- 00_START_HERE.md` from this directory), not a circular manifest field.

Final process PID26384 startedUTC2026-10-09T01:37:01Z; STARTUP_OK logged fullSourceSha44ec69d. Main window responsive at actualDPI144. Final native screenshot captured2400×1380 physical/1600×920DIP, no startup exception observed. This proves launch only. Existing desktop company shortcut calls Start-CompanyStudioReview defaults and therefore still targets the previous company publish; not changed or clicked as final-version proof.

- [Requirements trace](REQUIREMENTS_TRACE.md): all41 confirmed A/B/C requirements retained, old CU links.
- [Repair/root causes](ISSUE_MATRIX.md): failed runs preserved and source-vs-native boundaries.
- [95 runtime steps](RUNTIME_ACCEPTANCE.md): new company binary ledger; historical r1 passes are not inherited.
- [Release identity](RELEASE_MANIFEST.json): authoritative SourceHead, embedded version, all payload hashes and isolated launch location. The evidence commit is never this SourceHead.
- [Tests](TEST_RESULTS.json): sanitized counts/hash/times including original failures; raw TRX remains local ignored artifacts.
- [Layout](UI_LAYOUT_SPEC.md), [algorithm](ALGORITHM_REVIEW.md), [performance](PERFORMANCE.md), [remaining](ROADMAP.md).
- [Screenshot index](SCREENSHOT_INDEX.md), [file types](FILES_AND_TYPES.md).

Reproduce with SDK10.0.302 and unchanged global.json:

```powershell
dotnet restore RAWSelectionAssistant.sln
dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Debug --no-restore
dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release --no-restore
dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Debug
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Debug
dotnet test tests/PixelTart.ModularHarness.Tests/PixelTart.ModularHarness.Tests.csproj -c Debug
```

DPI tests additionally require existing tools/RC12ProductVisualHarness output; generating simulated layout fixtures is not OS scaling acceptance. NativeAcceptance contains hardware/fixture prerequisites; report skips, never fake hardware.

Publish real production project (no UiReviewBuild or AcceptanceBuild), using SourceHead from manifest:

```powershell
dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -o artifacts/releases/rebuild-confirmed-final-2026-10-09/publish/win-x64 -p:IncludeSourceRevisionInInformationalVersion=true -p:SourceRevisionId=SOURCE_HEAD
./tools/Start-CompanyStudioReview.ps1 -ReleaseDirectory artifacts/releases/rebuild-confirmed-final-2026-10-09/publish/win-x64 -RuntimeDirectory artifacts/rebuild-confirmed-2026-10-08/final-native-runtime
```

Launcher path validated for actual production start; isolated child environment does not alter user AppData/default install. Existing desktop shortcut is not silently replaced; its click target remains unverified. Final launch path is repository-relative for public evidence, actual local absolute location returned to user separately. No installer or binaries/private photos are pushed.

Native failure remains exact: `FrameArrived timed out: timed out waiting on channel`; text-only tree can read but click reports `coordinate input geometry is unavailable`; no supported Invoke fallback. PrintWindow succeeds as screenshot-only. User navigation between observations is not counted as controlled acceptance. No security settings or rights were bypassed.
