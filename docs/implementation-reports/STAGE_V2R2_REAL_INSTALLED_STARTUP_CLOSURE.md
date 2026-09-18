# Stage V.2R2 — Real installed startup closure

Status: INSTALLED STARTUP FIX VERIFIED; FULL ACCEPTANCE INCOMPLETE — not a release approval.

## USER_REPORTED_FAILURE

Old installer `PixelTart-DeveloperPreview-2.3.0-dev.67362b1-x64-Setup.exe` is REJECTED.
Product SHA `67362b112e873ec165508c39ec543ddba73e3b7e`.
Installer SHA256 `16373B8F5374DA92A9159FFEA4971E47E876BEE460496A23335F2F79219F8BB2`.
Original installed binary hashes match the preserved manifest; desktop/start-menu targets are correct.
Actual 16:40–16:41 logs identify missing BooleanToVisibilityConverter during MainWindow construction.
Real isolated install exit 0; its installed executable reproduces the same exception.

## Root cause and secondary failures

1. ReferenceColorWorkspaceView initializes before its shell parent, so its StaticResource converter must be locally available.
2. Once construction proceeds, Window.Show throws on the read-only NextCaptureFolder property with an implicit TwoWay TextBox binding; changed to OneWay.
3. Tether's reference template refers to nonexistent IconButton; mapped locally to the existing Av2IconButton style.
4. Reference workspace visibility bound to its child model rather than shell; use ancestor Window context. SourceImage now notifies when loaded.

No pages were removed or disabled. Previous source-text gates never constructed the full production window or its deferred templates.

## Diagnostics and compatibility

Startup stage/error code and STARTUP_OK logs added, source SHA embedded in informational version and developer window title. Error dialog offers copy summary/open logs; stacks remain in logs. Theme resource URI now explicitly identifies its owning assembly. Invalid settings shapes/null legacy collections fall back safely. Unreadable color catalogs retain original files and display a recovery message. Planning session restore failure falls back to workbench. Demo seeding no longer overwrites existing edited project data.

## Current evidence

- Release product build: 0 warnings / 0 errors.
- Real App startup test: production App.xaml, composition, MainWindow Show/Loaded, dispatcher/navigation and clean close PASS.
- Test traverses every production tool plus asset library, planning and tether; constructs actual free canvas. Planning filter categories each repeated five times. Demo user canvas edit preservation PASS.
- Focused core startup/settings/reference/migration tests: 13 passed, 0 failed.
- Focused WPF Stage III/IV/V regression: 11 passed, 0 failed.
- Full core suite: 1372 passed, 0 failed.
- Full WPF suite: 1262 passed, 0 failed, 1 pre-existing fixture-dependent test skipped. First full attempt had 15 failures: hardcoded assembly name broke resource initialization and downstream tests; corrected to runtime assembly name. Two unnamed Tether buttons also corrected. No failing tests suppressed.
- Final source production startup integration: 1 passed, 0 failed, including composition, every production toolbox route, actual canvas construction, Planning filters 5× and preservation of an edited demo canvas.
- Intermediate eb57d17 installed binary: fresh install/launch, upgrade install/launch, uninstall, reinstall/launch and all three normal exit codes PASS (0). This is intermediate evidence, not substituted for final package.
- Intermediate upgrade: 7/7 project JSON files hash-identical; existing Projects, ProjectSources and ShootBookings rows retained exactly. No old DB migration error occurred. Historical Stage IV/V/V.1 fixture matrix is NOT separately established.
- Final 4ccf52e fresh installed startup: real installer exit 0; actual installed EXE starts from C:\Windows with isolated Chinese/space-containing data root; MainWindow visible, STARTUP_OK with exact product SHA. Final lifecycle results below.
- Runtime manifest/dependency probes: 285 MATCH, 259 dependency probes present. No missing runtime or forbidden test/source/PDB files. This proves shipped content presence, not invocation of every native image codec.
- UI screenshots: Windows capture backend reports `SetIsBorderRequired ... 0x80004002`; accessibility observation works. Do not substitute fabricated screenshots.

## Frozen final candidate

FIXED_PRODUCT_SOURCE_SHA: `4ccf52e6176baba06589ce21e51c0e8bb95eed3f`.
Installer: `PixelTart-DeveloperPreview-2.3.0-dev.4ccf52e-x64-Setup.exe`.
SHA256: `5C23F7F481E83377509E655C8A49E6156D208657B3D50B0CBD5A069CF9AEE457`.
Local directory: `artifacts/stage-v2-installed-startup-failure/builds/2.3.0-dev.4ccf52e/installer/`.
Size: 51,394,040 bytes.
Clean publish uses committed source and packaging, warnings-as-errors, self-contained win-x64. No source changed during packaging. Previous artifacts are preserved, not overwritten.

## Final package lifecycle evidence (local time)

| Check | Result | Evidence |
|---|---|---|
| Fresh install | PASS | installer exit 0; separate Final Installed App directory |
| Fresh installed EXE | PASS | PID 25676; STARTUP_OK 17:32:05; actual window observed, 63 seconds alive; exit 0 |
| Upgrade from failed 67362b1 | PASS | old then final installer actually run on Final Upgrade App; both exit 0 |
| Upgrade installed EXE | PASS | PID 50276; STARTUP_OK 17:33:49; actual window observed, >60 seconds alive; exit 0 |
| Uninstall | PASS | final test runtime removed; uninstaller exit 0; isolated AppData retained |
| Reinstall | PASS | same final installer exit 0 |
| Reinstalled EXE | PASS | PID 43028; STARTUP_OK 17:35:09; actual window observed, >30 seconds alive; exit 0 |
| Runtime three-way diff | PASS | 285 file hashes MATCH; 259 manifest dependencies present |
| Working directory independence | PASS for tested paths | C:\Windows and C:\Windows\System32; Chinese/space data paths |
| Planning startup | PASS | actual installed MainWindow exposes populated project planning workspace |
| Full installed navigation / image workflow | INCOMPLETE | see remaining requirements below |

No unexpected reboot was requested. Original installed EXE/DLL hashes rechecked after tests and unchanged. Registry points to original D:\1 installation. No user photos/data were deleted.

## Still required before full closure

- Installed Planning filter repetition, reference image/result comparison, scheme save/reopen, Watch Folder and Tether → full editor manual workflow are not fully verified on final candidate.
- Actual installed screenshots unavailable because desktop capture fails. Logical WPF rendered fixtures are not physical screenshots.
- InstalledApplicationSmokeGate script added: verifies frozen manifest, requires 30 seconds visible main window, STARTUP_OK, recorded routes, no errors and exit 0; incomplete routes cannot become PASS. Complete automated installed navigation gate is not yet demonstrated.
- Old settings migration tests pass; historical DB fixtures and all requested restore-page scenarios need additional evidence.
- Crash dialog code includes open-log/copy-summary controls; interactive error-dialog verification remains pending.

Original rejected artifact is preserved locally under artifacts/stage-v2-installed-startup-failure/history/67362b1. Raw user logs and registration backup remain local and are not uploaded. User's original installation, shortcuts and registration were preserved/restored; isolated uninstall removed only the test runtime and retained its AppData. No Stage VI or promotional work was started.
