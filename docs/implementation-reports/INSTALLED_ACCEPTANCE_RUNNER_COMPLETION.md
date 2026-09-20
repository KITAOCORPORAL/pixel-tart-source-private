# Installed Acceptance Runner Completion Pass

Date: 2026-09-20. Final state: **AWAITING LOCAL ACCEPTANCE RUN**.

## Identity and scope

- START_HEAD: `6628d624dbee3d6a9170009a9d1b9e5e4353ec14`.
- PRODUCT_SOURCE_SHA: `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc` (unchanged).
- Product source modified: NO; installer source/runtime resources modified: NO.
- Current Setup: `PixelTart-DeveloperPreview-2.3.0-dev.8cb95e6-x64-Setup.exe`.
- SHA256: `C2E5F26A1D00BFE2A466450C0BB9D015CDD35D6733E909FE8ADCB5BAEFA6247D`.
- Old upgrade build: `8729d17fd0b19c1f2a984f8e12d1c4a8f8189d51`; Setup SHA256 `BCCD62FAEADC8F711074AB913760DDD564C4D19DEC0A5E384795E7F814140A78`.
- No new product installer. No Stage VI, new product features, video, layout redesign or PDF architecture change.
- FINAL_HEAD: the Git commit containing this completion report (reported as a concrete SHA in final handoff; not embedded self-referentially).

## Environment and executed checks

Windows PowerShell; SDK `D:\AI AGENT\.dotnet\dotnet.exe`; external runner net10.0-windows, self-contained win-x64. Live execution attempted: NO. Live execution allowed in this session: NO; computer-use skill restricts live Windows interaction to its supported JS API. Custom UIA/SendKeys/CopyFromScreen code was authored and compiled, not invoked. No BAT, --run, or --kit was executed. Previous supported-interface failures were not retried.

| Check actually executed | Result |
|---|---|
| Standalone Release build | PASS, 0 warnings / 0 errors |
| Self-contained publish | PASS |
| Fresh complete plan schema/path/hash/coverage validation | PASS |
| Upgrade complete plan schema/path/hash/coverage validation | PASS |
| Offline positive/negative self-tests | 23 PASS |
| Packaged pdfinfo / pdftoppm version probes | PASS, 26.07.0; no product PDF rendered |
| src/installer diff | Empty |
| Product project/assembly references | None |

These checks do not establish UI acceptance. Full Core/WPF/Real App/DPI gates were not rerun for tool/docs-only changes; earlier same-product-source results remain historical evidence only.

## Workflow implementation

| Area | Implemented checks | Live result |
|---|---|---|
| Fresh install/startup | Hash-pinned Setup; isolated new directory; full version SHA, path, PID/title, 10-second survival | NOT_RUN |
| Onboarding/planning | Optional tutorial exit with SKIPPED_EXPECTED; required tutorial absence and planning list | NOT_RUN |
| Create/date | Modal title/location; keyboard date change from Sep 19 to Sep 20; parsed UI date assertion; unique list selection | NOT_RUN |
| Text/persistence | ValuePattern edit, switch out/back, natural close, restart, list/body verification | NOT_RUN |
| Seven modules | Page content/empty-state landmarks; shot progress content, not just nav button | NOT_RUN |
| Preview | List/navigation/edit hidden, body retained, screenshot, Esc restoration | NOT_RUN |
| Quick Preview/import | Generated fixture only; real File Dialog; image Enter opens preview | NOT_RUN |
| Context menu | Shift+F10; all seven requested operations; full bounds captured | NOT_RUN |
| Booking | Real picker list/empty state and screenshot | NOT_RUN |
| PDF | Real export controls and SaveFileDialog; 300 DPI selection; size/pages/dimensions; Poppler full-page renders | NOT_RUN |
| Tether | Global workspace plus `Shot 01 / 01 · 未命名拍摄`; no camera mock | NOT_RUN |
| Return planning/online | Global navigation; original persisted body; online content | NOT_RUN |
| Normal close | WindowPattern.Close, optional real save confirmation, wait exit zero, fatal log scan | NOT_RUN |
| Upgrade | Old install/UI data creation/natural close → same-dir new Setup → data/seven modules/preview/normal close | NOT_RUN |

Fresh plan requests exactly 16 named real-window captures, including home, seven modules, preview, Quick Preview, context menu, date, booking, PDF completion, tether and online. Upgrade requests its own evidence. No source rendering or historical captures substituted.

## Selector and failure behavior

Process-bound root → optional ancestor → descendant. AutomationId preferred; otherwise Name + ControlType with scoped modal heading/ancestor and unique document-title descendant. Zero times out, multiple matches fail; no arbitrary first/index/coordinates. Optional is limited to uncounted invoke controls. UI delays use condition polling and bounded settling, not long sleeps. Focus keys have PID foreground checks. Common dialogs are scoped to their real Window and stable native IDs; missing access reports EXTERNAL_DIALOG_BLOCKED.

Source-derived selectors have not yet been verified on a live UIA tree. Framework peer exposure, locale and timing can require corrections after first local evidence. This is a runnable implementation, not a claim of first-run success. Missing AutomationIds are not proven, so no speculative product changes.

Failure saves step/error, all own-window UIA names/IDs/types/patterns when available, and AUTOMATION_GAP_REPORT.md. A failure alone is not proof of a product accessibility gap. No process is force-killed to manufacture PASS.

## Safety isolation audit (static PASS)

- New unique roots under `%LOCALAPPDATA%\PixelTart-TestAcceptance` for installation, synthetic fixtures, data and exports.
- Application/legacy data isolation confirmed against existing AppDataPaths acceptance-root contract; runner never edits app state or invokes app services.
- Relative paths reject traversal, absolute paths, alternate data streams and reparse ancestors/targets.
- Existing running PixelTart blocks; shared AppId registration outside test namespace blocks before Setup. Test machine/VM required when an actual used version is registered. No silent uninstall or overwrite of real installation.
- Setup receives explicit isolated DIR, NOCLOSEAPPLICATIONS, no icons/tasks. Existing Setup's admin requirement is met by one elevated runner; normal Windows installation registration is an explicit side effect, not described as fully portable.
- Old/new installer hashes checked; exact installed product-source SHA checked before each startup.
- No automatic upload, no real photos, no forced close, no deletion of prior evidence.
- Screenshot clipping/blank checks and popup bounding checks; occlusion remains MANUAL_REVIEW_REQUIRED.
- Bundled Poppler is from the local runtime cache; kit is local-only, not redistribution-cleared. Third-party source/license archive is not provided by that cache; external distribution requires separate review.

## One-click kit and evidence

Kit: `artifacts/installed-acceptance-kit/`. Entry: `运行安装版验收.bat`. It contains the self-contained runner, both full plans, both original Setup files, Poppler DLLs/tools, `README_验收说明.md`, dependency notice and file integrity manifest. `Package-Kit.ps1` reproduces it without live automation.

User actions: double-click and confirm UAC; return resulting acceptance-result folder. No IDE, commands, JSON edits, seven-module clicking or manual screenshots required. The first failed required step stops the workflow and retains evidence, without pretending subsequent files exist.

Runtime output: `acceptance-result/<timestamp-id>/acceptance.json`, then fresh/upgrade subfolders with acceptance.json, uia.jsonl, environment.txt, install logs, screenshots, exported PDF and page renders where reached. Two successful plans report TECHNICAL_ACCEPTANCE_PASS; visual remains NOT_REVIEWED and PENDING_USER.

Current live evidence: no new acceptance run, no new uia.jsonl, no installed screenshots or exported PDF. PLANNING_INSTALLED_ACCEPTANCE.json is an honest handoff record, not a runtime pass. PLANNING_INSTALLED_VISUAL_REVIEW.md remains unchanged / NOT REVIEWED because required screenshots do not exist yet. DPI, actual camera and photographer acceptance remain untested.

## Next step

Run the BAT on an isolated test Windows desktop and return acceptance-result for review. STOP at this user-authorized local handoff boundary.
