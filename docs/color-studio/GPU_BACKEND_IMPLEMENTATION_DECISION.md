# GPU backend implementation decision

## PoC result

The current host exposes an NVIDIA GeForce RTX 5060 Ti, 16,311 MiB from `nvidia-smi`, driver 610.62 and DirectX feature levels through 12_2. ComputeSharp 3.2.0 was restored and a real DX12 device created. An add-one shader returned the expected values. The eight-scene parity run reports zero final 8-bit RGB and OKLab deltas.

## Decision

Keep the Core `IColorMatchComputeBackend` seam and CPU fallback. DirectML remains the preferred first implementation because it targets Windows GPU adapters across AMD, Intel and NVIDIA without a CUDA toolkit. ComputeSharp is the fallback investigation if the Sinkhorn matrix and vector workload is easier to validate through DirectX 12 compute shaders. CUDA is not a product dependency.

## Required implementation proof

Before changing the backend status, a future change must create the selected device, dispatch a smoke kernel, read back a known result, report adapter/feature level/device state, run the eight-scene parity corpus, and exercise cancellation, allocation failure and device-loss recovery. Package deployment and license review must be recorded with those results.

Current status: **PARTIAL / COMPUTESHARP DX12 PAIRWISE KERNEL ACTIVE / CPU FALLBACK ACTIVE**. The smoke kernel and eight-scene parity campaign pass on the current host; complete GPU Sinkhorn and full-resolution pixel application are not yet GPU accelerated.
