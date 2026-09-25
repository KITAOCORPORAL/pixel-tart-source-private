# Reference Match V4 GPU performance record

Run: 2026-09-25, synthetic production-resolution fixtures, CPU and ComputeSharp-DX12 pairwise-kernel backend. Each resolution was run three times with 256 representative samples, 16 Sinkhorn iterations, zero residual passes, 1024 px tiles and 16 px overlap. The benchmark allocates the requested pixel buffers; it does not infer large-image timing from a thumbnail.

| Resolution | CPU runs (ms) | Approx. working allocation | Tiles | GPU |
|---|---:|---:|---:|---|
| 12MP | CPU 1794–1855 / GPU 1797–2042 | 36–38 MB | 12 | Pairwise OT kernel |
| 24MP | CPU 3528–3596 / GPU 3524–3580 | 72–73 MB | 24 | Pairwise OT kernel |
| 45MP | CPU 6794–7079 / GPU 6762–7098 | 136–138 MB | 54 | Pairwise OT kernel |
| 60MP | CPU 8778–8965 / GPU 8761–8965 | 180–181 MB | 60 | Pairwise OT kernel |

Host audit: NVIDIA GeForce RTX 5060 Ti, 16.83 GB reported by ComputeSharp, DirectX 12 device created, hardware smoke kernel passed. Peak VRAM and GPU utilization are not instrumented. Both CPU and GPU runs completed without OOM and produced the expected pixel counts.

The current engine remains safe for CPU processing at these synthetic sizes. Full GPU speedup is not expected yet because Sinkhorn, residual and pixel application remain CPU stages; the current result validates device execution, output parity and bounded memory behavior.
