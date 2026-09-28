# Match V4 Failure Recovery Acceptance

| Contract | Result |
|---|---|
| GPU execution exception | PASS: existing injected backend falls back to CPU, preserves transform and processing generation |
| GPU allocation/OOM exception | PASS: `OutOfMemoryException` maps to `GpuFailureReason.OutOfMemory` and CPU fallback |
| Cancellation before dispatch | PASS: `OperationCanceledException`, no partial result |
| Cancellation between tiles | PASS by token checks in tile loop; no partial publication |
| Device-lost abstraction | SIMULATED only through injected execution exception; real DX12 device loss NOT RUN |
| Resource disposal | PASS for per-tile `using` buffers; real device recreation NOT RUN |
| Latest-wins / superseded generation | PARTIAL: generation identity is preserved and stale publication is rejected by caller contract; no WPF concurrent race campaign |
| Real hardware OOM/device loss | NOT RUN |

No hardware failure is represented as a real run unless the adapter reports it.
