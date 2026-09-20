# Independent installed UI acceptance runner

Status: **AWAITING LOCAL ACCEPTANCE RUN**. Full workflow code and plans are implemented and offline-validated; live selectors and installed UI behavior remain NOT VERIFIED.

No project references or production assembly loading. The runner installs into a new SHA-named directory under `%LOCALAPPDATA%/PixelTart-TestAcceptance`, launches its real `PixelTart.exe` with isolated data, validates product version, and uses PID-scoped Microsoft UI Automation patterns. No ViewModel, ICommand, reflection, JSON/SQLite state editing or RenderTargetBitmap.

`--validate plan.json` only checks schema, isolation and installer hash; no UI, install or process launch. `--run plan.json` is a live desktop operation and must be used only in an environment permitting that API. The current Codex computer-use skill restricts live Windows automation to its supported JS API, so this session must not invoke `--run` or a substitute wrapper.

Ambiguous selectors, missing patterns, window clipping, black images, escaped focus, installer/hash/version mismatch and abnormal close fail closed. A partial plan can never emit READY FOR USER ACCEPTANCE. Screenshot capture alone does not prove no occlusion or full popup inclusion; manual review is required.

Build with `dotnet build tools/PixelTart.InstalledAcceptance -c Release`. Package with `Package-Kit.ps1` (SDK/cache parameters can be overridden). Packaging publishes self-contained win-x64, copies existing installers and Poppler tools, runs both offline validations and 23 self-tests, then generates integrity.json. It never executes live acceptance. Offline commands can use the DLL with dotnet to avoid apphost elevation. The historical smoke plan is not shipped and cannot represent full acceptance.

`planning-full.plan.json` implements fresh install, conditional onboarding, create/date, edit/switch/restart persistence, seven content landmarks, preview, synthetic reference import through a real file dialog, Quick Preview, context menu, booking, 300 DPI raster PDF export/inspection, tether shot context, global navigation return, online selection and natural exit; 16 screenshot requests. `upgrade-full.plan.json` installs known old 8729d17, creates data through UI, closes, upgrades the same directory to 8cb95e6, verifies body/list/seven modules/preview and exits naturally.

Selector priority: AutomationId, otherwise Name + ControlType with ancestor ID/Name/ControlType. A unique text heading identifies the nearest containing modal scope; list items can be constrained by a unique descendant title. Ambiguity fails without index/First/coordinates. Optional applies only to uncounted invokes; absent optional controls produce SKIPPED_EXPECTED. Missing live providers produce evidence-backed gap reports, not automatic product changes.

Local user entry: double-click `运行安装版验收.bat`, confirm UAC. See `README_验收说明.md`. The runner and Setup invocations share an elevated token, with uiAccess disabled. Never launch live flags/BAT from this restricted session.

Each run uses a unique test namespace, never deletes old evidence, and blocks if the shared installer registration belongs outside that namespace or any PixelTart process is running. Setup receives NOCLOSEAPPLICATIONS and no shortcuts/tasks. Application data and legacy migration are isolated with the existing acceptance-root environment override. No product state is written by the runner. Path traversal, alternate streams and reparse targets are rejected.

Evidence lives in `acceptance-result/<timestamp-id>/{fresh,upgrade}`: acceptance.json, uia.jsonl, environment, install logs, actual screenshots/PDF and failure UIA tree. No missing artifact is synthesized. Two completed plans yield TECHNICAL_ACCEPTANCE_PASS; visual stays NOT_REVIEWED and user acceptance PENDING_USER. Source-derived selectors may require follow-up after actual UIA results. Product src/installer remain frozen and original Setup is reused.
