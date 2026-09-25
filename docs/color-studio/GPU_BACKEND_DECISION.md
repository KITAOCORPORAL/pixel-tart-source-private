# GPU backend decision

## Audit

Host: NVIDIA GeForce RTX 5060 Ti, 16,311 MiB reported by `nvidia-smi`; DirectX feature levels through 12_2; driver `610.62` (`dxdiag` reports `32.0.16.1062`). .NET SDK is 10.0.401. The repository now uses ComputeSharp 3.2.0 for a Windows DX12 compute backend. DirectML and CUDA are not product dependencies.

## Decision

ComputeSharp was selected after a real PoC: the host created a DX12 device, ran an add-one smoke shader and read back the expected values. The backend currently accelerates representative pairwise OT kernel construction; CPU Sinkhorn, residual and pixel application remain in the shared semantic path. DirectML remains a possible future backend for broader adapter coverage. CUDA is rejected for the product baseline.

`GpuColorMatchComputeBackend` reports available only after device creation, hardware-device validation and smoke readback. A request that prefers GPU records the GPU backend; initialization, dispatch or allocation failures terminate that GPU attempt and retry on CPU with a reason and stage.

## Recovery and memory policy

Future GPU work must treat unavailable adapter, device reset/loss, unsupported shader, allocation failure and out-of-memory as a task failure followed by safe CPU retry. Tile overlap and bounded representative samples limit working-set growth. VRAM changes throughput, concurrency and analysis size—not image-quality semantics.

Status: **PARTIAL / REAL DX12 KERNEL VALIDATED**. Full V4 GPU execution, device-loss campaign and production preview/export integration remain open.
