# CPU / GPU parity report

Status: **NOT RUN — GPU backend unavailable**.

The parity seam is present through `IColorMatchComputeBackend`; the CPU backend is deterministic and the GPU backend explicitly throws `NotSupportedException`, causing the engine to select CPU and record `UsedCpuFallback`. Therefore there is no honest GPU output to compare and no GPU tolerance claim in this revision.

Required future evidence: same target/reference/settings, CPU and DirectML/ComputeSharp outputs, OKLab and RGB max/mean deltas, cancellation behavior, device-loss recovery and preview/export parity. The algorithm contract should use a documented floating-point tolerance rather than claim bit-exact equality.
