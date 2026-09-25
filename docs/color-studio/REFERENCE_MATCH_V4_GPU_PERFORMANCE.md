# Reference Match V4 GPU performance record

Run: 2026-09-25, synthetic production-resolution fixtures, CPU backend. Each resolution was run three times with 256 representative samples, 16 Sinkhorn iterations, zero residual passes, 1024 px tiles and 16 px overlap. The benchmark allocates the requested pixel buffers; it does not infer large-image timing from a thumbnail.

| Resolution | CPU runs (ms) | Approx. working allocation | Tiles | GPU |
|---|---:|---:|---:|---|
| 12MP | 1790–1849 | 37 MB | 12 | Not available |
| 24MP | 3525–3578 | 73 MB | 24 | Not available |
| 45MP | 6722–7052 | 137 MB | 54 | Not available |
| 60MP | 8734–8958 | 181 MB | 60 | Not available |

Host audit: NVIDIA GeForce RTX 5060 Ti, 17.10 GB reported by `nvidia-smi`, driver 610.62, DirectX feature level 12_2+. Device creation and smoke kernel were not performed because the repository contains no validated DirectML/ComputeSharp implementation. Peak VRAM and GPU utilization are therefore unavailable. The CPU runs completed without OOM and produced the expected pixel counts.

The current engine remains safe for CPU processing at these synthetic sizes. GPU speedup, GPU cancellation and device-loss recovery require a real backend before they can be measured.
