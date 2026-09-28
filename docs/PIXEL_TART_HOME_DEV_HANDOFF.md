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
- Capability detection, memory tiers, quality presets, failure metadata and synthetic 12MP/24MP/45MP/60MP CPU benchmark fixtures are now implemented. The benchmark confirms CPU completion at all four sizes; it does not establish GPU speed or parity.

## L. Professional RAW and TIFF Stage 2 audit

- Existing LibRaw decoding remains the canonical RAW path and is documented as `BACKEND_SUPPORTED_NOT_VERIFIED` without vendor fixtures.
- A bounded uncompressed RGB TIFF writer now supports 8-bit/16-bit samples, optional ICC payloads and cancellation. It does not claim high-bit-depth RAW preservation or full RAW-to-TIFF release closure.
- Stage 2 is **PARTIAL / BLOCKED** by missing legal vendor fixtures and the current decoder's 8-bit output contract. A shared float working buffer and TIFF publishing path now exist; RAW sensor precision and RAW→TIFF integration remain open.
- Adobe XMP Stage 3 foundation is **PARTIAL**: parser, compatibility states, source hashing, persistent storage, cancellable batch import, Color Studio preset node, hover/selection preview, strength control and commit/cancel with Adjustment Stack undo are implemented. Capture One and full third-party semantic coverage remain open.
- GPU implementation decision and third-party preset boundaries are recorded in `docs/color-studio/GPU_BACKEND_IMPLEMENTATION_DECISION.md`, `docs/THIRD_PARTY_PRESET_ARCHITECTURE.md` and the related compatibility documents. ComputeSharp-DX12 hardware smoke and eight-scene parity now pass; full V4 GPU execution remains PARTIAL. Stage 3 and Stage 4 remain **NOT STARTED**.

## I. Environment and handoff rules

Read `docs/DEV_ENVIRONMENT_WINDOWS.md`. Run the bootstrap and bounded verification scripts after cloning. There are no submodules and no current Git LFS payloads. Do not rely on stash, old `bin/obj`, local DLLs, copied packages, company paths, real photos, customer data, credentials, or hidden machine state. Runtime user data belongs under `%LocalAppData%` at runtime; repository fixtures are synthetic and safe.

## J. Home first run

1. Open Codex on the home Windows PC and authorize access to the private GitHub repository; Git Credential Manager or browser OAuth is sufficient. `gh auth login` is optional if GitHub CLI is already installed. Never put a PAT in a URL, script, manifest, or repository file.
2. Clone `https://github.com/KITAOCORPORAL/pixel-tart-source-private.git` and check out `integration/pixel-tart-developer-preview` (or pull with `--ff-only` if already cloned).
3. Run `powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-home.ps1` from the repository.
4. Run `powershell -ExecutionPolicy Bypass -File .\scripts\verify-home-dev.ps1`; it checks remote parity before building.
5. Ask Codex to read `docs/CODEX_RESUME_PROMPT.md` and continue only the next product task above after verification passes.

Minimum SDK: 10.0.302 with `latestFeature` roll-forward in `global.json`; 10.0.401 was verified here. A compatible newer .NET 10 feature band is allowed.

## M. Phase 4 home RTX 5060 Ti and RAW corpus acceptance (2026-09-28)

- Home GPU probe: NVIDIA GeForce RTX 5060 Ti, 16GB dedicated VRAM, ComputeSharp-DX12, DX12 available, ULTRA tier, 1024 tile, four parallel tiles.
- Real external V4 run reached the GPU pixel executor for Canon EOS R6 CR3, Fuji X-T5 RAF, and Fuji GFX100S RAF. The four listed home fixtures now pass CPU/GPU full-resolution parity after canonical protection decision-key unification; TIFF16 export and pixel readback parity also pass with max one code value.
- Corpus baseline: 387 files, 350 RAW candidates, 269 LibRaw ProfessionalDecode PASS, 10 FAIL, 70 UNSUPPORTED, 159 isolated Core pixel-path PASS and 110 PARTIAL; no complete product-level full pipeline PASS. Details are in `docs/color-studio/raw-compatibility/`.
- Stable Match v3 remains default. Match V4 is guarded beta and is not a production completion claim. Native pointer walkthrough, OOM/device-loss injection, peak VRAM telemetry, complete ICC/EXIF propagation, and WPF 3D renderer remain open.
- External corpus and TIFF outputs remain outside Git; only hashes, metadata, results, source, tests, and reports are committed.
- Final home V4 rerun: Canon EOS R6 CR3, both Fuji X-T5 RAF fixtures, and Fuji GFX100S 102MP pass full-resolution GPU parity in three repeats; all four reach TIFF16 export and CPU/GPU TIFF16 readback max code delta 1.

## 2026-09-28 Photographer Workflow Foundation

- Implemented: Core Face Lock geometry, stable 2-Up state contract, ExportRecipe model/store, Booking buffered conflict rules, sync envelope/idempotency/high-risk conflict policy, 3D camera/renderer contract.
- Existing verified: Tether culling annotations, proxy-first preview, filtering/sorting, compare modes, PublishingExportService and preset persistence.
- Partial: WPF Face Lock detector/visual overlay, Rapid Compare UI, multi-recipe export UI, production Windows 3D renderer.
- Spec only: Pocket/cloud provider, Online Selection cloud gallery, Browser Extension, AI Culling, Skin Studio, Look DNA.
- Tests: `PhotographerWorkflowFoundationTests` 5/5; Core Release build 0 errors.
- Next: wire contracts into Photography WPF surfaces and run full Core/WPF/Photography/Calendar/Match regression.

## Phase 1B follow-up (2026-09-28)

- Implemented WPF compare toolbar controls and rapid compare keyboard routing in Tether.
- Face Lock toggle is explicit about missing detector; no fake landmarks are used.
- Added Booking schema v6 and repository/service persistence for buffers, HOLD expiry, PaymentState, Revision, DeviceId, DeletedAtUtc; editor fields are visible in the existing booking workflow.
- Core combined Face Lock transform test and 5 foundation tests pass; Release x64 WPF build passes with 0 warnings/errors.
- Partial: production face detector, WPF transform overlay, ExportRecipe manager/multi-recipe UI, Windows 3D renderer, native pointer/screenshots.
