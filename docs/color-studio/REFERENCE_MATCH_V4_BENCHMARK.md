# Reference Match V4 benchmark record

This foundation uses synthetic RGB fixtures only. No customer photographs or external model weights are included.

## Current bounded checks

- 96×64 synthetic target/reference, 64 representative samples, 8 Sinkhorn iterations: deterministic repeated output, CPU PASS.
- 32×32 high-chroma synthetic fixture with 4 Sinkhorn iterations and 0.02 chroma cap: finite sRGB output, residual cap respected, CPU PASS.
- 1024×768 tile plan at 256 px with 8 px overlap: multiple tiles and overlap metadata, PASS.
- Cancellation before a match: `OperationCanceledException`, no partial result returned, PASS.

The four requested 12MP/24MP/45MP/60MP production timing baselines are **NOT YET MEASURED** in this run. They require a controlled benchmark harness and will not be invented from small-fixture timings. Peak VRAM is not applicable while the GPU backend is unavailable.

## Quality dimensions to measure next

V3 vs V4 should report OKLab distribution error, luminance error, neutral drift, skin-candidate drift, highlight contamination, shadow oversaturation, gamut clipping, time and peak memory on the same synthetic fixture corpus. Multi-reference stability and preview/export parity remain regression requirements through the shared pipeline.
