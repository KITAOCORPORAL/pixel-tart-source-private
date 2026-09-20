# Independent installed UI acceptance runner

## AcceptanceKit v3 navigation regression

Current delivery is PixelTart-Installed-Acceptance-Kit-8cb95e6-v3.zip. ScopePreset=PrimaryNavigation resolves centrally to SidebarNavigationScroll, never SidebarRoot (a sibling landmark). MainWindow preset means the actual PID-bound window. Both plans pre-audit all 13 navigation entries. --lint-plan <file> emits a per-step classified selector-lint.json with error/warning explanations. --navigation-tests adds 9 offline regressions; total offline cases 40. Any required scope resolution failure is identified separately from a missing target. Date PART_TextBox/Edit fix is retained.

Preflight checks 7/7, with existing PixelTart instances prompting WAIT/close/retry. No forced termination. Runner records ownership and checks PID/path/window before normal close. Live audit/UI remains restricted; --preflight-only and lint/tests do not invoke it. Older v2 notes below are historical; v3 supersedes their navigation scope and 6-check preflight.

## AcceptanceKit v2 selector correction

The first real user run installed and launched successfully, then stopped at date-value: the DatePicker and its inner editor both expose ValuePattern and the same Name. v2 requires workspace → uniquely named create-modal heading container → Custom date control → PART_TextBox / Edit / 拍摄日期. Both date write/read selectors are fixed in both plans. Exact ID/type/name finds one target in the recorded tree; the original tree is flat, so it cannot prove the complete ancestor traversal live.

Both full plans were audited. Module navigation now scopes to the observed Custom planning workspace: the named UniformGrid navigation container does not appear in the actual UIA tree. Content editors/landmarks use DocumentScroll; native file-dialog IDs remain window-scoped; menu entries use the unique open Menu; PDF quality uses its ComboBox; confirmation uses the actual themed dialog and YesButton. Where source-derived peers have not yet been observed (reference image item, calendar, popup), live uniqueness still must be proven; no speculative product IDs were added.

`--audit-selectors <kit-root>` is a LIVE, isolated install/start diagnostic. It opens the create modal, checks all name/date/location/confirm selectors without editing or creating project data, and stops with SELECTOR_AUDIT_ONLY; later UI states are explicitly not visited. Do not run it in this restricted session. Normal fresh/upgrade plans additionally run non-mutating selector checkpoints on entering each necessary state, including navigation, editing, calendar, image preview/menu, booking and export dialogs. Formal PASS still requires all 124 fresh steps and upgrade, never resumed/stiched evidence.

`--selector-tests` runs eight offline cases covering the five requested named test groups, both plan date contracts and full selector scopes. It does not simulate live UIA success. On ambiguous target/scope (and missing required target), selector-diagnostics.json stores requested selector, count, all candidate IDs/names/types/classes/rectangles, parents and ancestor chains. Exactly one is required with Single(); duplicate runtime roots are deduplicated by actual UIA identity only, never by Name/index.

Status: **AWAITING LOCAL ACCEPTANCE RUN**. Full workflow code and plans are implemented and offline-validated; live selectors and installed UI behavior remain NOT VERIFIED.

## Portable ZIP delivery correction — 2026-09-20

Deliver `artifacts/PixelTart-Installed-Acceptance-Kit-8cb95e6.zip`, never the BAT alone. Its root contains the BAT, Launch-Acceptance.ps1, self-contained runner/runtime, both plans, Chinese README and KIT_MANIFEST.json; installer/ contains both unmodified Setup files. There are no development paths in either plan. The packaging script now creates a fresh staging directory and a root-flat ZIP; KIT_MANIFEST.json replaces integrity.json.

The apphost now uses asInvoker. BAT immediately prints a banner and invokes six preflight checks before any UAC. Hashing uses framework SHA256 (no external PowerShell module dependency). Only after explicit start confirmation does the launcher elevate for the existing admin-only Setup; its child PixelTart process inherits the same token. Current Setup has no PrivilegesRequiredOverridesAllowed, so /CURRENTUSER is not an available override without rebuilding the frozen installer.

`运行安装版验收.bat --preflight-only` executes only file checks and `PixelTart.InstalledAcceptance.exe --kit <root> --preflight-only`; it never installs, launches the product, calls UIA or elevates. `Test-PortableKit.ps1` copies and extracts the ZIP into an independent directory, tests this route and five fail-closed cases, then restores all fixtures. Live --kit without the final flag remains forbidden in this session. Success/failure produces acceptance-launch.log and acceptance-result.zip; the BAT always reaches pause. OS policy can still prevent any BAT/PowerShell launch, and no claim is made to bypass that.

No project references or production assembly loading. The runner installs into a new SHA-named directory under `%LOCALAPPDATA%/PixelTart-TestAcceptance`, launches its real `PixelTart.exe` with isolated data, validates product version, and uses PID-scoped Microsoft UI Automation patterns. No ViewModel, ICommand, reflection, JSON/SQLite state editing or RenderTargetBitmap.

`--validate plan.json` only checks schema, isolation and installer hash; no UI, install or process launch. `--run plan.json` is a live desktop operation and must be used only in an environment permitting that API. The current Codex computer-use skill restricts live Windows automation to its supported JS API, so this session must not invoke `--run` or a substitute wrapper.

Ambiguous selectors, missing patterns, window clipping, black images, escaped focus, installer/hash/version mismatch and abnormal close fail closed. A partial plan can never emit READY FOR USER ACCEPTANCE. Screenshot capture alone does not prove no occlusion or full popup inclusion; manual review is required.

Build with `dotnet build tools/PixelTart.InstalledAcceptance -c Release`. Package with `Package-Kit.ps1` (SDK/cache parameters can be overridden). Packaging publishes self-contained win-x64, copies existing installers and Poppler tools, runs both offline validations and 23 self-tests, then generates integrity.json. It never executes live acceptance. Offline commands can use the DLL with dotnet to avoid apphost elevation. The historical smoke plan is not shipped and cannot represent full acceptance.

`planning-full.plan.json` implements fresh install, conditional onboarding, create/date, edit/switch/restart persistence, seven content landmarks, preview, synthetic reference import through a real file dialog, Quick Preview, context menu, booking, 300 DPI raster PDF export/inspection, tether shot context, global navigation return, online selection and natural exit; 16 screenshot requests. `upgrade-full.plan.json` installs known old 8729d17, creates data through UI, closes, upgrades the same directory to 8cb95e6, verifies body/list/seven modules/preview and exits naturally.

Selector priority: AutomationId, otherwise Name + ControlType with ancestor ID/Name/ControlType. A unique text heading identifies the nearest containing modal scope; list items can be constrained by a unique descendant title. Ambiguity fails without index/First/coordinates. Optional applies only to uncounted invokes; absent optional controls produce SKIPPED_EXPECTED. Missing live providers produce evidence-backed gap reports, not automatic product changes.

Local user entry: download and extract the complete ZIP, double-click `运行安装版验收.bat`, pass preflight, start and confirm UAC. See `README_验收说明.md`. The runner and Setup invocations share an elevated token, with uiAccess disabled. Never launch live flags/BAT without --preflight-only from this restricted session.

Each run uses a unique test namespace, never deletes old evidence, and blocks if the shared installer registration belongs outside that namespace or any PixelTart process is running. Setup receives NOCLOSEAPPLICATIONS and no shortcuts/tasks. Application data and legacy migration are isolated with the existing acceptance-root environment override. No product state is written by the runner. Path traversal, alternate streams and reparse targets are rejected.

Evidence lives in `acceptance-result/<timestamp-id>/{fresh,upgrade}`: acceptance.json, uia.jsonl, environment, install logs, actual screenshots/PDF and failure UIA tree. No missing artifact is synthesized. Two completed plans yield TECHNICAL_ACCEPTANCE_PASS; visual stays NOT_REVIEWED and user acceptance PENDING_USER. Source-derived selectors may require follow-up after actual UIA results. Product src/installer remain frozen and original Setup is reused.
