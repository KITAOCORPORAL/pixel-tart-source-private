# Reference Match V4 architecture

Status: **PARTIAL / CPU foundation implemented**. This is an evolution of Match v3, not a second renderer or colour system.

## Reuse and boundaries

- Reuse `ReferenceLookMatcher`, `ReferenceColorTargetBuilder`, OKLab D65 conversion, tone curve, existing protection parameters, gamut mapping, `ReferenceLookStore` and the shared `ColorStudioRenderPipeline`.
- V4 adds a bounded representative-sample path, regularized Sinkhorn transport, a local three-band blend, decomposition metadata, residual correction with a hard cap, cancellation checks, cache-key revisioning and a compute-backend seam.
- Preview and export remain on the existing shared pipeline. No WPF V4 panel, second renderer, neural model, cloud API or CUDA-only path is introduced.
- Local region masks are soft luminance bands in this foundation. Semantic regions, 3D UI and learned features remain deferred.

## Data flow

`managed RGB → OKLab samples → global + luminance-band distribution → bounded OT → protected local blend → gamut mapping → residual correction → shared render pipeline`.

The CPU implementation bounds samples to 16–2048, Sinkhorn iterations to 1–128 and residual passes to four. Neutral, skin-like, highlight and low-light saturation protection are deterministic hooks. Tile planning includes overlap metadata for full-resolution work; the current engine does not allocate a full image OT matrix.

## Backend contract

`IColorMatchComputeBackend` defines semantic parity. `CpuColorMatchComputeBackend` is production-usable and deterministic. `GpuColorMatchComputeBackend` uses ComputeSharp 3.2.0 and a DX12 pairwise OT kernel after device creation and smoke readback. CPU Sinkhorn, residual and pixel application remain shared stages. Device or dispatch failures fall back safely to CPU and record the failure.

## Cache and cancellation

The V4 cache key includes source identity, reference identity, settings, analysis resolution, tile settings and algorithm revision. Cancellation is checked during sampling, matrix construction, Sinkhorn iterations, residual passes and pixel application. A cancelled operation throws before returning a partial result.
