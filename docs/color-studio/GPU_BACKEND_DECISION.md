# GPU backend decision

## Audit

Host: NVIDIA GeForce RTX 5060 Ti, 16,311 MiB reported by `nvidia-smi`; DirectX feature levels through 12_2; driver `610.62` (`dxdiag` reports `32.0.16.1062`). .NET SDK is 10.0.401. The repository currently has no DirectML, ComputeSharp, ONNX Runtime or CUDA dependency.

## Decision

Keep the algorithm and backend contract in Core, ship deterministic CPU fallback now, and defer GPU execution until a small Windows-native backend can be validated for deployment, device loss and parity. DirectML is the preferred future investigation because it avoids a CUDA toolkit and preserves non-NVIDIA compatibility. ComputeSharp is a possible alternative if its package/deployment footprint is acceptable. CUDA is rejected for the product baseline.

The current `GpuColorMatchComputeBackend` intentionally reports unavailable. A request that prefers GPU records `UsedCpuFallback=true`; it never claims acceleration from the adapter name alone.

## Recovery and memory policy

Future GPU work must treat unavailable adapter, device reset/loss, unsupported shader, allocation failure and out-of-memory as a task failure followed by safe CPU retry. Tile overlap and bounded representative samples limit working-set growth. VRAM changes throughput, concurrency and analysis size—not image-quality semantics.

Status: **AUDIT COMPLETE / GPU IMPLEMENTATION DEFERRED**.
