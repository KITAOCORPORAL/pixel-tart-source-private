# Match V4 Real RAW Parity Closure

## Current status: PARTIAL / BLOCKED

The RTX 5060 Ti run reached the DX12 GPU pixel executor without CPU fallback for all four required fixtures and completed TIFF16 export. Proxy previews pass the unchanged parity gate, but full-resolution parity is not closed.

| Fixture | MP | Full CPU ms | Full GPU ms | Mean | P95 | P99 | Max | Status |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Canon EOS R6 CR3 | 20.17 | 4778.6 | 847.0 | 9.63e-8 | 2.98e-7 | 4.77e-7 | 3.69e-2 | PARTIAL |
| Fuji X-T5 RAF #1 | 40.19 | 9101.3 | 701.6 | 6.40e-8 | 1.79e-7 | 4.77e-7 | 3.90e-2 | PARTIAL |
| Fuji X-T5 RAF #2 | 40.19 | 9534.1 | 639.8 | 7.21e-8 | 2.09e-7 | 4.77e-7 | 4.70e-2 | PARTIAL |
| Fuji GFX100S RAF | 102.07 | 23837.5 | 1744.0 | 1.09e-7 | 3.58e-7 | 5.96e-7 | 3.61e-2 | PARTIAL |

Unchanged gate: mean <= 1e-5, P95 <= 3e-5, P99 <= 5e-5, max <= 1e-4. Mean and percentile errors are small, but the full-resolution maxima fail the gate.

## Evidence

The full-resolution forensics runner compared CPU and GPU output at 256, 512, 1024 and 2048 tile sizes. Outlier coordinates remain stable across tile sizes where the run completes, so the evidence does not support a tile origin, row pitch, copy-back, or dispatch-bounds explanation. Representative outliers cluster at the skin hue boundary near 80 degrees; the remaining CPU/GPU branch semantics are not yet numerically identical for every float/double input.

The CPU implementation remains the correctness oracle. The shared `ProtectionBoundaryEpsilon = 1e-4` contract reduces the original proxy boundary divergence, but it does not close the full-resolution gate.

## Product route

`RAW -> ProfessionalDecode RGB48 -> FrozenRawMaster -> MatchV4ProductSession -> DX12 tiled pixel execution -> AtomicTiffWriter`

No CPU fallback was reported for the four full-resolution executions. That fact records route selection; it does not convert a failed parity comparison into a pass.

## Remaining blockers

- Resolve the remaining full-resolution protection boundary divergence without changing product thresholds.
- Re-run synthetic, proxy, full-resolution, and all tile-size parity after the fix.
- Complete controlled OOM/device-lost injection, VRAM telemetry, deterministic 4/8/16GB budget comparison, ICC/EXIF propagation, and cross-hardware real RAW comparison.

Match v3 remains Stable/default. Match v4 remains guarded Beta.

**Status: PARTIAL / BLOCKED — REAL RAW GPU NUMERICAL PARITY IS NOT CLOSED.**