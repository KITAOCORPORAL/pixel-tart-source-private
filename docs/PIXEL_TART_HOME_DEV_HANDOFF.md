# Pixel Tart home development handoff

This is the source-of-truth handoff for `KITAOCORPORAL/pixel-tart-source-private`. It records the current repository state; it does not turn incomplete product evidence into a PASS.

## A. Project identity

- Repository: `KITAOCORPORAL/pixel-tart-source-private`
- Branch: `integration/pixel-tart-developer-preview`
- Product SHA at handoff: `8e8f16cab28930cc2ba5456ceac08445c32835f5`
- Last audited product commit: `8e8f16c`. Same-source native node drag evidence is complete; native visual screenshot confirmation remains unavailable on this host.
- Project path: repository-relative; the production project is `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`.
- Starting reference SHA from the original brief (`139ff52`) is historical only; the remote had advanced and this handoff uses the newer remote HEAD.

## B. Current development phase

- Photography Product Development Gate: **PASS**. See `docs/PIXEL_TART_PHOTOGRAPHY_INTERACTION_CLOSURE.md`; physical DPI remains a release hardware gate.
- Color Studio Phase 1: **BLOCKED / substantial implementation complete**. See `docs/COLOR_STUDIO_PHASE1_CLOSURE.md` and `docs/COLOR_STUDIO_PHASE1_NATIVE_PIXEL_QA.md`. Do not relabel this as READY.
- Completed: shared zoom/pan model and sampling mapping, professional node stack, scheme persistence/migration, selected-node batch sync, switching/cancellation/retry paths, shared preview/export pipeline, targeted regression and portable evidence records.
- Remaining Phase 1 gate: a clean synchronized native visual drag run. `scripts/verify-color-studio-native-capture.ps1` now validates real Production WPF captures (100/100 stable frames), but its first 18-case visual drag run has intermittent observer timeouts/order mismatches and is diagnostic only. Physical DPI remains a release-hardware gate.
- Phase 2: 3D Color Space core is **PARTIAL**; WPF visualization and production Canvas ↔ 3D integration remain deferred.
- Phase 2 is **PARTIAL CORE IMPLEMENTATION / BLOCKED BY PHASE 1 REGRESSION**. P2.1 proxy/cache core, camera state, and framework-neutral sample/cluster linking are under `docs/color-studio/`; WPF visualization is not integrated.

## C. Next product task

Continue **Color Studio Phase 1 Native Interaction / Final UX Review Gate** only. The actual mouse-down/threshold/drag-over/insertion-line/drop and final node order are recorded on the current source SHA; obtain native visual confirmation on a host where PrintWindow capture works. Do not relabel Phase 1 READY from state evidence alone. Do not redo the implemented stack, scheme, batch-sync, undo, recovery, or fixture work. After closure, integrate the existing Phase 2 core into a bounded WPF visualization using the Phase 2 rendering options and test plan.

## D. Deferred / later

- 3D Color Space: `DEFERRED_TO_PHASE_2`
- Physical DPI: `RELEASE HARDWARE GATE`
- Installer: `NOT CURRENT PHASE`
- Browser Extension: `LATER`
- Online Selection Mini Program: `LATER`

## E. Architecture constraints

Keep Simple and Professional on one canonical state model. Preserve per-target stack isolation, `ColorAdjustmentStack`, `ReferenceMatchNode`, `ColorRangeNode`, `TransitionBlendNode`, `FilmNode`, scheme persistence and legacy migration, selected-node sync, the shared Preview/Export pipeline, headless export, and cancellation/revision/last-wins behavior. Use `docs/COLOR_STUDIO_CURRENT_IMPLEMENTATION_MAP.md` as the API map. Do not create a second Color Studio architecture or renderer.

## F. UI rules

Use the Pixel Tart palette: Graphite, Mineral, Warm Silver, Oxidized Copper, and Photography Gold. No purple/violet theme, Windows-white menus, system-blue selection, or fluorescent AI green. Preserve the information hierarchy “图片 > 空间 > 信息 > 控件”. Parameter values use the neutral value brush rather than Accent; ratings use warm gold; system-visible UI is primarily Chinese.

## G. Known test state

- Core and targeted WPF Color Studio/Photography suites are recorded in the current evidence documents and latest commits.
- Release x64 product build was verified from this source with .NET SDK 10.0.401 (compatible with the 10.0.302 minimum feature band): 0 warnings, 0 errors.
- Logical DPI coverage is development evidence; refreshed scheme/error/node-overflow states are recorded, but physical-display validation is not claimed.
- Performance follow-up is P2 and non-blocking; historical baselines are not silently treated as current.

## H. Known issues

- P0: none observed in targeted runs (not a whole-app absence guarantee).
- P1: native pointer interaction and final UX review remain open; physical DPI is a separate release-hardware gate.
- P2: controlled performance comparison/optimization follow-up; Phase 2 visual polish and memory telemetry.

## K. Reference Match V4 foundation (2026-09-25)

- V4 is **PARTIAL**: bounded OKLab representative samples, regularized CPU Sinkhorn transport, soft luminance-band local blending, protection hooks, gamut mapping, bounded residual correction, cancellation and cache-key revisioning are implemented in Core.
- The existing Match v3 and shared Preview/Export pipeline remain the canonical path; no second renderer or WPF V4 integration was added while Phase 1 native drag evidence is still blocked.
- GPU hardware is detected, but the backend is **AUDIT / DEFERRED**. The engine has an explicit unavailable GPU seam and records CPU fallback; no GPU acceleration claim is made.

## I. Environment and handoff rules

Read `docs/DEV_ENVIRONMENT_WINDOWS.md`. Run the bootstrap and bounded verification scripts after cloning. There are no submodules and no current Git LFS payloads. Do not rely on stash, old `bin/obj`, local DLLs, copied packages, company paths, real photos, customer data, credentials, or hidden machine state. Runtime user data belongs under `%LocalAppData%` at runtime; repository fixtures are synthetic and safe.

## J. Home first run

1. Open Codex on the home Windows PC and authorize access to the private GitHub repository; Git Credential Manager or browser OAuth is sufficient. `gh auth login` is optional if GitHub CLI is already installed. Never put a PAT in a URL, script, manifest, or repository file.
2. Clone `https://github.com/KITAOCORPORAL/pixel-tart-source-private.git` and check out `integration/pixel-tart-developer-preview` (or pull with `--ff-only` if already cloned).
3. Run `powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-home.ps1` from the repository.
4. Run `powershell -ExecutionPolicy Bypass -File .\scripts\verify-home-dev.ps1`; it checks remote parity before building.
5. Ask Codex to read `docs/CODEX_RESUME_PROMPT.md` and continue only the next product task above after verification passes.

Minimum SDK: 10.0.302 with `latestFeature` roll-forward in `global.json`; 10.0.401 was verified here. A compatible newer .NET 10 feature band is allowed.
