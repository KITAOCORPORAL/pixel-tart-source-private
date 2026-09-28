# Match V4 productization audit

Baseline: `8e02b0f87f0c1a18b6b7e7bf210a7022b22e6153` (Phase 4 working tree).

| Surface | Current truth |
|---|---|
| Match v3 | Production Color Studio renderer and TIFF16 RAW route |
| Match v4 analysis | Implemented in Core; bounded representative sampling, Sinkhorn mapping, residual and protection stages |
| Match v4 CPU | Deterministic Core reference executor; tested |
| ComputeSharp/DX12 | Real hardware smoke and representative pairwise OKLab kernel only |
| GPU Sinkhorn/residual/pixel application | CPU, not GPU |
| Tile planner | Planning helper; not a full tiled executor |
| GPU VRAM policy | Descriptor/tier helper; not a live allocator or reservation |
| Color Studio UI | Stable/Match v4 Beta selector is wired; Stable/v3 remains the default |
| V4 execution mode | AUTO reports validated DX12 capability; CPU is an explicit and automatic fallback |
| RAW/Preview/Export | Guarded `FrozenRawMaster` session route uses one V4 transform/generation for preview and TIFF16 export when Beta is selected |
| Non-RAW product route | Stable/v3 remains the production route; V4 non-RAW parity is not claimed in this phase |
| Real RAW / 102MP acceptance | Not run in this phase; external camera fixtures remain opt-in and Sony A7 IV remains LibRaw-blocked |

The Phase 4 route adds a stable `MatchTransformV4` identity/hash, a product-facing
high-precision executor seam, capability classification, and a guarded Color Studio Beta
entry point. V4 is opt-in and limited to a single-reference FrozenRawMaster session while
the existing V3 route remains the stable default. The route intentionally does not claim
Color Range/Film/ICC/EXIF parity, real-camera V4 GPU acceptance, or live 102MP telemetry
until those are separately proven.
