# Pixel Tart home development handoff

This is the source-of-truth handoff for `KITAOCORPORAL/pixel-tart-source-private`. It records the current repository state; it does not turn incomplete product evidence into a PASS.

## A. Project identity

- Repository: `KITAOCORPORAL/pixel-tart-source-private`
- Branch: `integration/pixel-tart-developer-preview`
- Product SHA at handoff: `d7848d1e3b9b3f218f0434278305fddfec59f006`
- Last commit: `d7848d1 docs(dev): record final verification commit`
- Project path: repository-relative; the production project is `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`.
- Starting reference SHA from the original brief (`139ff52`) is historical only; the remote had advanced and this handoff uses the newer remote HEAD.

## B. Current development phase

- Photography Product Development Gate: **PASS**. See `docs/PIXEL_TART_PHOTOGRAPHY_INTERACTION_CLOSURE.md`; physical DPI remains a release hardware gate.
- Color Studio Phase 1: **BLOCKED / substantial implementation complete**. See `docs/COLOR_STUDIO_PHASE1_CLOSURE.md`. Do not relabel this as READY.
- Completed: shared zoom/pan model and sampling mapping, professional node stack, scheme persistence/migration, selected-node batch sync, switching/cancellation/retry paths, shared preview/export pipeline, targeted regression and portable evidence records.
- Remaining Phase 1 gates: native drag insertion and final zoom/pan walkthrough; production failure/retry visual walkthrough; strict native-pixel/full-size QA, settled roundtrip capture, and remaining logical-200% scheme/error coverage.
- Phase 2: 3D Color Space and cluster visualization are `DEFERRED_TO_PHASE_2`.

## C. Next task

Continue **Color Studio Phase 1 Final UX Closure**. Do not restart product analysis. Priority order:

1. Shared Zoom / Pan and eyedropper under Zoom/Pan.
2. Professional Node Row, Scheme product UX, and selected-node batch sync UI.
3. Undo / Redo final closure, rapid Reference/Scheme switching, and processing-error recovery.
4. Close the 18 production screenshot set with full-size visual QA at 1180x720, 200% logical DPI, Chinese UI, and themed popups.

The acceptance target is `READY FOR COLOR STUDIO PHASE 1 UX REVIEW`, then Phase 2 may begin.

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
- Logical DPI coverage is development evidence; physical-display validation is not claimed.
- Performance follow-up is P2 and non-blocking; historical baselines are not silently treated as current.

## H. Known issues

- P0: none observed in targeted runs (not a whole-app absence guarantee).
- P1: the three Color Studio UX/visual verification groups listed in `COLOR_STUDIO_PHASE1_CLOSURE.md` remain open.
- P2: controlled performance comparison/optimization follow-up.

## I. Environment and handoff rules

Read `docs/DEV_ENVIRONMENT_WINDOWS.md`. Run the bootstrap and bounded verification scripts after cloning. There are no submodules and no current Git LFS payloads. Do not rely on stash, old `bin/obj`, local DLLs, copied packages, company paths, real photos, customer data, credentials, or hidden machine state. Runtime user data belongs under `%LocalAppData%` at runtime; repository fixtures are synthetic and safe.
