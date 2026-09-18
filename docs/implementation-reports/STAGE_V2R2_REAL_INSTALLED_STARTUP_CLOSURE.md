# Stage V.2R2 — Real installed startup closure

Status: IN PROGRESS — not a release approval.

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
- Full core suite: running, not yet a PASS.
- Fresh/upgrade/reinstall installed fixed binary: PENDING.
- Runtime manifest/dependency probes: PENDING fixed package.
- UI screenshots: Windows capture backend reports `SetIsBorderRequired ... 0x80004002`; accessibility observation works. Do not substitute fabricated screenshots.

FIXED_PRODUCT_SOURCE_SHA, installer identity and installed gate results will be recorded after source freeze and actual execution. Original rejected artifact is preserved locally under artifacts/stage-v2-installed-startup-failure/history/67362b1. Raw user logs and registration backup remain local and are not uploaded.
