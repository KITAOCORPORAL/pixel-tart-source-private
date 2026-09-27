# Pixel Tart current development state — 2026-09-27

This file is a repository-derived baseline for the next development task. It was generated from branch `integration/pixel-tart-developer-preview` after fetching and fast-forward pulling `origin`; source, XAML, ViewModel, service, tests, scripts and committed evidence were cross-checked. It does not promote partial evidence to product completion.

## Git

- `START_HEAD`: `d06b800f6f9c76423b9a1bace08accd8ae34e755`
- `FINAL_HEAD`: `420456bcb73d0ffcc11c8b08cd03e4d73beef3a9`
- `REMOTE_HEAD`: `420456bcb73d0ffcc11c8b08cd03e4d73beef3a9`
- Final verification: `LOCAL_HEAD == REMOTE_HEAD`; working tree clean after push.
- Branch: `integration/pixel-tart-developer-preview`
- Before this baseline: `LOCAL_HEAD == REMOTE_HEAD`, working tree clean.
- No reset, force push, merge-main, user-data deletion or generated binary artifacts were used.

## Capability matrix

### Color Studio

| Capability | Status | Evidence / boundary |
|---|---|---|
| Simple Mode | IMPLEMENTED | Shared `TetherReferenceModeViewModel` state and WPF workspace. |
| Professional Mode | IMPLEMENTED | Canonical `ColorAdjustmentStack`; legacy look migration. |
| Adjustment Stack | IMPLEMENTED | Reference Match, Color Range, Transition, Film and Preset nodes. |
| Node Reorder | IMPLEMENTED | WPF drag/drop state, insertion feedback and command regression. |
| Native Drag / Drop | BLOCKED | Real production capture pipeline exists, but clean synchronized native visual drag closure is not proven. |
| Processing Order | IMPLEMENTED | Ordered stack is the canonical render order. |
| Reference Match | IMPLEMENTED | Existing matcher and stack integration. |
| Color Range | IMPLEMENTED | Positive/negative samples, OKLab selection and adjustments. |
| Eyedropper | IMPLEMENTED | Displayed-rect inverse mapping and sample tests. |
| Selection Preview | IMPLEMENTED | Preview-only selection mask path. |
| Keep Luminance | IMPLEMENTED | Color Range node option and renderer path. |
| Transition | IMPLEMENTED | Stack node and render stage. |
| Film | IMPLEMENTED | Film node and shared film pipeline. |
| Undo / Redo | IMPLEMENTED | Stack history and transaction grouping. |
| Scheme | IMPLEMENTED | Persistence, migration, apply, update, save-as and delete flows. |
| Batch Sync | IMPLEMENTED | Selected-node synchronization with target isolation. |
| Processing Stop | IMPLEMENTED | Cancellation/revision and safe previous-frame behavior. |
| Preview / Export parity | IMPLEMENTED | Shared headless pipeline used by WPF preview/export adapters. |
| Chinese UI | IMPLEMENTED | Targeted Color Studio source/resource scans pass. |
| Zoom / Pan | IMPLEMENTED | Shared zoom/pan state and mapping tests. |
| Fit / 100% | IMPLEMENTED | Commands and mapping coverage. |
| Sampling Coordinate Mapping | IMPLEMENTED | Letterbox, split and side-by-side mapping tests. |

### Color Match / V4

| Capability | Status | Evidence / boundary |
|---|---|---|
| Match v3 | IMPLEMENTED | Existing production reference matcher and photography evidence. |
| Match v4 | PARTIAL | Bounded OKLab sampling, Sinkhorn transport, residual correction, protection, gamut mapping and cache keys. |
| Reference Analysis Cache | IMPLEMENTED | Existing reference/cache services and tests. |
| CPU Pipeline | IMPLEMENTED | Deterministic CPU backend and cancellation. |
| GPU Pipeline | PARTIAL | GPU backend executes only representative pairwise OT kernel; remaining semantic stages remain CPU. |
| ComputeSharp DX12 | PARTIAL | Real device creation, smoke kernel and pairwise dispatch verified on current host. |
| CPU / GPU parity | IMPLEMENTED for tested scope | Eight-scene final 8-bit RGB/OKLab deltas are zero; not floating-point bit identity and not full-pipeline parity. |
| GPU fallback | IMPLEMENTED | Initialization/dispatch failures, OOM and unavailable device fall back to CPU with diagnostics. |
| VRAM management | PARTIAL | LOW/STANDARD/HIGH/ULTRA budget policy exists; peak VRAM and utilization telemetry are not instrumented. |

### RAW

| Capability | Status | Evidence / boundary |
|---|---|---|
| LibRaw | IMPLEMENTED / NOT VERIFIED by vendor fixture | `Sdcb.LibRaw` decoder, safe failure and cancellation exist. |
| RAW format coverage | PARTIAL / NOT VERIFIED | Extension recognition exists for ARW, CR2, CR3, NEF, NRW, RAF, RW2, ORF, ORI, DNG, PEF, 3FR, FFF, IIQ, SRW, RWL; no legal vendor fixtures are committed. X3F is not in the current candidate set. |
| Sony / Canon / Nikon / Fujifilm / Panasonic / OM / Leica / Pentax / Hasselblad / Phase One / Sigma / DJI | PARTIAL or UNSUPPORTED | Matrix records backend candidate status only; no full-decode product claim. DJI is not represented in the current candidate extension set. |
| Embedded preview | NOT IMPLEMENTED as a verified product path | No fixture-driven preview evidence. |
| Full decode | PARTIAL | Full LibRaw processing path exists, but current contract is 8-bit RGB sRGB and vendor decode is unverified. |
| Bit depth | BLOCKED for professional RAW | `LibRawDecoder` explicitly sets `OutputBps = 8`. |
| Working color space | PARTIAL | Decoder outputs sRGB; Color Studio stack declares OKLabD65 internally. No native wide-gamut RAW working path. |
| ICC | PARTIAL | TIFF ICC payload validation/embedding exists; RAW camera ICC/colorimetric round trip is not verified. |
| Camera metadata | PARTIAL | Make/model, timestamp and orientation fallback are read; complete metadata carry-through is not implemented. |
| HighBitDepthImageBuffer | IMPLEMENTED foundation | Shared float RGB buffer and RGB48 adapter exist, but currently ingest from 8-bit decoded RGB. |
| 16-bit pipeline | PARTIAL | Buffer and TIFF writer exist; RAW-native precision is not preserved end to end. |

### Export

| Capability | Status | Evidence / boundary |
|---|---|---|
| JPEG | IMPLEMENTED | Existing WPF publishing renderer. |
| PNG | IMPLEMENTED | Existing WPF publishing renderer. |
| TIFF 8-bit | PARTIAL | Publishing selection and bounded uncompressed writer exist; production round-trip coverage is limited. |
| TIFF 16-bit | PARTIAL | RGB48 writer and tests exist; source precision is only as good as the input buffer. |
| ICC embedding | PARTIAL | Validated ICC payload support exists; colorimetric round trip is not verified. |
| Metadata preservation | PARTIAL | WPF encoder attempts metadata clone; complete TIFF EXIF/orientation carry-through is not proven. |
| Batch Export | IMPLEMENTED | Existing publishing task service, cancellation, naming safety and batch tests. |
| Compression / alpha / full DPI-orientation contract | NOT IMPLEMENTED | Baseline is uncompressed RGB; no complete production contract. |

### Preset

| Capability | Status | Evidence / boundary |
|---|---|---|
| Preset Browser | PARTIAL | Adobe XMP list, selection and import UI exist; categorization/search/favorites are not implemented. |
| Preset import | PARTIAL | Adobe XMP batch importer is implemented; other vendor formats are not. |
| Adobe XMP | PARTIAL | Parser, source hash, exact/approximate/unsupported mapping, persistence, batch import and Color Studio Preset node. |
| Lightroom preset compatibility | PARTIAL | Numeric Camera Raw XMP subset only; curves, LUTs, calibration and full semantic fidelity unsupported. |
| Photoshop / Camera Raw preset compatibility | PARTIAL | Same XMP subset boundary; no Photoshop-specific package workflow. |
| Capture One compatibility | NOT IMPLEMENTED | No parser or fixture workflow. |
| Hover Preview | PARTIAL | Selection/hover preview routes through the shared stack; mouse-leave restoration and full browser UX are not formally closed. |
| Preset Strength | IMPLEMENTED foundation | 0–100% parameter-space interpolation with default 100% in the node workflow. |
| Preset Commit | IMPLEMENTED | Commit creates a canonical Adjustment Stack Preset node. |
| Preset Undo / Redo | IMPLEMENTED | Commit enters existing stack history. |
| Preset Persistence | IMPLEMENTED | Imported XMP records persist in the existing application data store; scheme persistence is separate. |
| Preset Batch Sync | PARTIAL | Existing stack sync can carry a committed Preset node; dedicated browser batch UX is not implemented. |
| Pixel Tart native preset | IMPLEMENTED for existing schemes/output presets | Separate from third-party import semantics. |

### Photography

| Capability | Status |
|---|---|
| Asset Library | IMPLEMENTED |
| Tether Capture | IMPLEMENTED |
| Rating | IMPLEMENTED |
| EXIF | IMPLEMENTED / bounded metadata scope |
| Viewer | IMPLEMENTED |
| Loupe | IMPLEMENTED |
| Import | IMPLEMENTED |
| Batch operations | IMPLEMENTED |

## Color Studio Phase 1

Real implementation is substantial and targeted regression is green. The phase remains **BLOCKED** only at the native visual closure gate: the repository's Production WPF capture path validates real pixels and stable frames, but the synchronized 18-case native drag run has observer timeouts/order mismatches. This is not a reason to redo the implemented stack, schemes, batch sync, undo, recovery or fixture work.

## High Bit Depth and professional RAW chain

The current actual chain is still:

`RAW → LibRaw 8-bit sRGB → HighBitDepthImageBuffer(float) → Color Studio`.

The float buffer prevents later components from requiring byte storage, but it cannot restore sensor precision already discarded by `OutputBps = 8`. The target chain `RAW native precision → high precision working buffer → Color Studio → V4 → Preset → Film → 16-bit TIFF` is therefore **NOT YET CLOSED**.

## GPU

ComputeSharp 3.2.0 creates a DX12 device and runs a hardware smoke test plus pairwise OT kernel. V4 CPU Sinkhorn/residual/pixel application remain shared CPU stages. Current measured synthetic timings (three-run ranges) are: 12MP CPU 1794–1855 ms / GPU 1797–2042 ms; 24MP 3528–3596 / 3524–3580; 45MP 6794–7079 / 6762–7098; 60MP 8778–8965 / 8761–8965. Host record: RTX 5060 Ti, about 16.83 GB reported by ComputeSharp. Peak VRAM/utilization are not instrumented.

## Tests

Latest baseline verification:

- Release x64 solution build: **0 warnings, 0 errors**.
- Core targeted Color/V4/RAW/TIFF/XMP/photography suites: **58 passed, 1 skipped, 0 failed**.
- WPF targeted Color Studio/Photography/RAW/Asset Library suites: **111 passed, 0 failed**.
- Evidence/publishing/theme drift repair suite: **37 passed, 0 failed** after updating assertions and committed SHA manifests.
- Production-resolution benchmark test remains opt-in/skipped unless explicitly enabled; no new timing claim is generated by this baseline.

## Performance / RAM / VRAM

- Existing synthetic V4 record covers 12/24/45/60MP and bounded working allocations of approximately 36–181 MB for the measured RGB buffers.
- Historical photography evidence records a 30 × 2400×1600 processed export peak working set of 857 MB; it is historical evidence, not a new controlled comparison.
- CPU/GPU timing is near parity for the currently GPU-covered pairwise stage, not proof of full-pipeline acceleration.
- VRAM peak, utilization and adaptive concurrency telemetry are **NOT IMPLEMENTED**.

## Known issues

### P0

- None observed in the current targeted suites.

### P1

- Native Color Studio drag visual closure remains BLOCKED.
- RAW sensor precision and vendor fixture verification are missing.
- TIFF production metadata/compression/colorimetric round-trip is incomplete.
- V4 full GPU execution is incomplete.
- Preset browser UX is only a foundation; Capture One is not implemented.

### P2

- Adaptive VRAM telemetry and controlled performance comparison.
- Phase 2 3D Color Space WPF visualization and native gestures.
- Broader preset semantics, search/category/favorites and dedicated batch browser UX.

## Next development, in dependency order

1. **ALREADY IMPLEMENTED:** Color Studio stack, processing order, schemes, undo/redo, batch sync, preview/export parity and targeted regressions. Keep them stable; do not re-architect.
2. **P1:** Replace the LibRaw 8-bit output contract with a legal, fixture-driven high-precision decode API (float or 16-bit), while retaining the existing safe 8-bit JPEG path.
3. **P1:** Pass the high-precision buffer through Color Studio/V4/Preset/Film and add RAW-to-16-bit TIFF end-to-end fixtures.
4. **P1:** Complete TIFF 16-bit production metadata, ICC round-trip, DPI/orientation, compression decision and cancellation validation.
5. **P1:** Expand Preset Browser UX: category/search/favorites, robust hover-leave restore and dedicated batch sync, preserving the existing Preset node.
6. **P1:** Expand ComputeSharp coverage one measured stage at a time (reference statistics, OKLab transforms, masks or pixel application), with parity and CPU fallback at every stage.
7. **P2:** Capture One compatible subset only after legal fixtures and semantics are defined.
8. **P2:** Integrate Phase 2 3D Color Space WPF visualization only after the Phase 1 native closure gate.
9. **P2:** Add RAM/VRAM telemetry and adaptive quality/concurrency policy.
