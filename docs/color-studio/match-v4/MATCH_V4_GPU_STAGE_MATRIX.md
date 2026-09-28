# Match V4 GPU stage matrix — Phase 3

| Stage | Status | Evidence / reason |
|---|---|---|
| Representative sampling | CPU_ONLY | deterministic bounded sampling in Core |
| sRGB → OKLab conversion | GPU_IMPLEMENTED / CPU_ORACLE | full-pixel DX12 shader mirrors Core float32 contract; runtime synthetic parity max `6.71e-6` |
| Pairwise cost/kernel | GPU_IMPLEMENTED | ComputeSharp/DX12 backend |
| Sinkhorn iterations | GPU_READY / CPU_EXECUTED | reduction/iteration remains CPU for stable oracle semantics |
| Barycentric mapping | CPU_EXECUTED | currently consumes GPU pairwise matrix on CPU |
| Residual refinement | CPU_ONLY | sequential bounded refinement |
| Neutral/skin/highlight/shadow protection | GPU_IMPLEMENTED / CPU_ORACLE | protection parameters are applied in the full-pixel shader; focused CPU oracle retained |
| Full pixel application | GPU_FULL_PIXEL_EXECUTION | DX12 returns complete float32 `HighBitDepthImageBuffer` from real tiled dispatch/readback |
| TIFF16 conversion/write | CPU_ONLY | AtomicTiffWriter remains CPU/IO layer |
| Tile coordinate execution | GPU_IMPLEMENTED | DX12 tile upload/dispatch/readback with bounded tile size and cancellation |

Overall classification: **GPU_FULL_PIXEL_EXECUTION adapter / MATCH_V4_GPU_EXPERIMENTAL product**. Sinkhorn, barycentric mapping, residual analysis, WPF engine selection, and GPU TIFF16 product routing remain outside the GPU product path.
