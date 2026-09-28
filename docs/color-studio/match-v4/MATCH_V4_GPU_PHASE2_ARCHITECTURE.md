# Match V4 GPU Phase 2 architecture

Phase 2 moves the backend boundary, not the product engine switch.

* `IMatchV4ComputeBackend` is the platform-neutral contract.
* `MatchV4CpuBackend` is the correctness oracle and records execution timing.
* `PixelTart.MatchV4.Dx12` is the Windows adapter; ComputeSharp types stop at this project.
* `MatchV4TileExecutor` defines deterministic tile coordinates and cancellation for future GPU dispatch.
* `MatchTransformV4` and `ProcessingGenerationId` remain shared inputs. A backend cannot re-analyze or mutate them.
* Metal is a future boundary only; no fake backend exists.

The current DX12 adapter still accelerates pairwise kernel creation and performs mapping on CPU.
The next phase may move reductions and full pixel application only after float32 parity proves the
same semantics.
