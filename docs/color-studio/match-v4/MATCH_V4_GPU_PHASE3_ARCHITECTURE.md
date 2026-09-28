# Match V4 GPU Phase 3 architecture

Status: **PARTIAL — GPU full-pixel path implemented in the Windows adapter; product acceptance is not closed.**

The Core project remains platform-neutral. `IMatchV4PixelBackend`, `MatchV4ResolvedTransform`, `MatchV4PixelExecutor`, and `MatchV4CpuPixelBackend` are Core contracts. ComputeSharp and DX12 remain confined to `PixelTart.MatchV4.Dx12`.

The DX12 adapter now accepts a float32 `HighBitDepthImageBuffer`, applies the resolved Oklab transform in a ComputeSharp shader, and returns a float32 `HighBitDepthImageBuffer`. It uses bounded pointwise tiles and preserves source metadata, working color space, orientation, transform identity, and processing generation at the Core boundary.

The existing V4 analysis path is intentionally unchanged: representative sampling, Sinkhorn, barycentric mapping, and residual analysis remain CPU oracle work. This phase does not claim a fully GPU-resident analysis pipeline.

The DX12 adapter is not currently wired into the WPF Color Studio engine selector or TIFF16 product command. Therefore the classification remains `GPU_FULL_PIXEL_EXECUTION` for the isolated adapter and `MATCH_V4_GPU_EXPERIMENTAL / PARTIAL` for the product.
