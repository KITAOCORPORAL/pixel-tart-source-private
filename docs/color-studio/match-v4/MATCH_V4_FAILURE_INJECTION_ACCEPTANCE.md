# Match V4 Failure Injection Acceptance

The production executor catches GPU out-of-memory and execution failures and falls back to the CPU oracle while retaining the same transform hash and processing generation. Contract tests inject a throwing GPU backend and verify CPU fallback, failure reporting, transform identity, and processing generation. Allocation failure maps to `GpuFailureReason.OutOfMemory`; cancellation is rethrown and cannot publish partial output. Real DX12 OOM/device-lost injection, disposal, device invalidation, and retry telemetry have not been run.

Status: PARTIAL / NOT RUN for injected hardware failures.
