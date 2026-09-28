# Match V4 GPU stage matrix — Phase 2

| Stage | Status | Evidence / reason |
|---|---|---|
| Representative sampling | CPU_ONLY | deterministic bounded sampling in Core |
| sRGB → OKLab conversion | CPU_ONLY | shared CPU color science; GPU kernel receives OKLab samples |
| Pairwise cost/kernel | GPU_IMPLEMENTED | ComputeSharp/DX12 backend |
| Sinkhorn iterations | GPU_READY / CPU_EXECUTED | reduction/iteration remains CPU for stable oracle semantics |
| Barycentric mapping | CPU_EXECUTED | currently consumes GPU pairwise matrix on CPU |
| Residual refinement | CPU_ONLY | sequential bounded refinement |
| Neutral/skin/highlight/shadow protection | CPU_ONLY | shared CPU semantics |
| Full pixel application | CPU_ONLY | no production GPU pixel buffer yet |
| TIFF16 conversion/write | CPU_ONLY | AtomicTiffWriter remains CPU/IO layer |
| Tile coordinate execution | CPU_IMPLEMENTED | deterministic backend-neutral executor; DX12 tile dispatch not wired |

Overall classification: **GPU_PARTIAL**, not GPU_MAJOR_PIPELINE and not GPU_FULL_PIXEL_EXECUTION.
