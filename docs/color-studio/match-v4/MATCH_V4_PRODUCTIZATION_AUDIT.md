# Match V4 productization audit

Baseline: `28ed79d649b9474f11ed6d0704c09b88dd728f0a`.

| Surface | Current truth |
|---|---|
| Match v3 | Production Color Studio renderer and TIFF16 RAW route |
| Match v4 analysis | Implemented in Core; bounded representative sampling, Sinkhorn mapping, residual and protection stages |
| Match v4 CPU | Deterministic Core reference executor; tested |
| ComputeSharp/DX12 | Real hardware smoke and representative pairwise OKLab kernel only |
| GPU Sinkhorn/residual/pixel application | CPU, not GPU |
| Tile planner | Planning helper; not a full tiled executor |
| GPU VRAM policy | Descriptor/tier helper; not a live allocator or reservation |
| Color Studio UI | Does not construct or call `ReferenceMatchV4Engine` |
| RAW/Preview/Export | FrozenRawMaster path remains Match v3 |

The new Phase 1 code adds a stable `MatchTransformV4` identity/hash, a product-facing
high-precision executor seam, and capability classification. It does not silently route the
existing UI to V4, because the current UI reference analysis is a V3 `ReferenceLook`/analysis
contract and a direct swap would create a second color-science result.
