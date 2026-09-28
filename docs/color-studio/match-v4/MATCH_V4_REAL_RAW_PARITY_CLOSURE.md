# Match V4 Real RAW Parity Closure

## Current status: FULL_RES_PARITY_CLOSED for four listed fixtures

The RTX 5060 Ti run reached the DX12 GPU pixel executor without CPU fallback for all four required fixtures and completed TIFF16 export. Full-resolution parity now passes the unchanged gate after canonical decision-key unification.

| Fixture | MP | Full CPU ms | Full GPU ms | Mean | P95 | P99 | Max | Status |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Canon EOS R6 CR3 | 20.17 | 4778.6 | 847.0 | 9.31e-8 | 2.98e-7 | 4.77e-7 | 2.00e-6 | PASS |
| Fuji X-T5 RAF #1 | 40.19 | 9101.3 | 701.6 | 5.34e-8 | 1.79e-7 | 2.98e-7 | 1.31e-6 | PASS |
| Fuji X-T5 RAF #2 | 40.19 | 9534.1 | 639.8 | 6.76e-8 | 2.09e-7 | 3.58e-7 | 1.19e-6 | PASS |
| Fuji GFX100S RAF | 102.07 | 23837.5 | 1744.0 | 1.07e-7 | 3.58e-7 | 5.96e-7 | 1.74e-6 | PASS |

Unchanged gate: mean <= 1e-5, P95 <= 3e-5, P99 <= 5e-5, max <= 1e-4. Mean and percentile errors are small, but the full-resolution maxima pass the gate.

## Evidence

The full-resolution forensics runner compared CPU and GPU output at 256, 512, 1024 and 2048 tile sizes. Outlier coordinates remain stable across tile sizes where the run completes, so the evidence does not support a tile origin, row pitch, copy-back, or dispatch-bounds explanation. The pre-fix representative outliers clustered at the skin hue boundary near 80 degrees. The post-fix canonical decision key removes the hard-branch divergence; residual float arithmetic remains below the gate.

The CPU implementation remains the correctness oracle. The shared `ProtectionClassifierContract` computes a CPU double decision key once and passes it to the GPU, removing hard-branch float/double divergence without lowering precision globally.

## Product route

`RAW -> ProfessionalDecode RGB48 -> FrozenRawMaster -> MatchV4ProductSession -> DX12 tiled pixel execution -> AtomicTiffWriter`

No CPU fallback was reported for the four full-resolution executions. The unchanged full-resolution gate passes for all four listed fixtures.

## Remaining blockers

- Continue wider camera and hardware coverage; these four fixtures do not imply universal format support.
- Expand synthetic, camera, and hardware coverage while preserving the closed four-fixture gate.
- Complete controlled OOM/device-lost injection, VRAM telemetry, deterministic 4/8/16GB budget comparison, ICC/EXIF propagation, and cross-hardware real RAW comparison.

Match v3 remains Stable/default. Match v4 remains guarded Beta.

**Status: FULL_RES_PARITY_CLOSED for the four listed fixtures; production-wide Match V4 remains guarded Beta.**