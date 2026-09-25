# CPU / GPU parity report

Status: **PARTIAL — real GPU pairwise kernel validated**.

The parity seam is present through `IColorMatchComputeBackend`. ComputeSharp-DX12 creates a hardware device and executes the pairwise OT kernel. The current eight-scene run reports RGB max delta 0, RGB mean delta 0, OKLab max delta 0 and OKLab mean delta 0 after final 8-bit quantization. This is within the documented tolerance of RGB max 3, RGB mean 0.3; it is not a bit-exact floating point claim.

Required future evidence: full GPU Sinkhorn, local transfer, residual and pixel application parity; cancellation during dispatch; device-loss recovery; and preview/export parity. The current failure injection confirms GPU execution exceptions fall back safely to CPU with `ExecutionFailure` and `Dispatch` metadata.
