# Match V4 GPU Phase 2 closure

Status: **MATCH_V4_GPU_EXPERIMENTAL / PARTIAL**.

Completed:

* Core no longer references ComputeSharp or DX12.
* Platform-neutral backend contract and CPU oracle exist.
* Windows DX12/ComputeSharp adapter builds Release x64 outside Core.
* Transform hash, processing generation and cancellation contracts remain shared.
* Backend-neutral tile executor and VRAM tier parity tests exist.

Not completed:

* full GPU Sinkhorn/barycentric/residual/protection/pixel application;
* real GPU tiled residency and GFX100S 102MP acceptance;
* float32 CPU/GPU parity metrics;
* Color Studio v4 Beta UI and TIFF16 product route;
* device-loss/OOM/latest-wins full product tests;
* real RAW V4 acceptance and performance baseline.

Color Match remains **14/28**; no gate is newly closed.
