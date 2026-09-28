# Match V4 Full Resolution GPU Parity

## Gate

`mean <= 1e-5`, `P95 <= 3e-5`, `P99 <= 5e-5`, `max <= 1e-4`. Thresholds were not changed.

| Fixture | Mean | P95 | P99 | Max | >1e-6 | >1e-5 | >1e-4 | >1e-3 | >1e-2 | Repeatability |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Canon EOS R6 CR3 | 9.31e-8 | 2.98e-7 | 4.77e-7 | 2.00e-6 | 1900 | 0 | 0 | 0 | 0 | 3/3 stable |
| Fuji X-T5 RAF #1 | 5.34e-8 | 1.79e-7 | 2.98e-7 | 1.31e-6 | 138 | 0 | 0 | 0 | 0 | 3/3 stable |
| Fuji X-T5 RAF #2 | 6.76e-8 | 2.09e-7 | 3.58e-7 | 1.19e-6 | 79 | 0 | 0 | 0 | 0 | 3/3 stable |
| Fuji GFX100S RAF | 1.07e-7 | 3.58e-7 | 5.96e-7 | 1.74e-6 | 55,515 | 0 | 0 | 0 | 0 | 3/3 stable |

All four runs used ComputeSharp-DX12 GPU pixel execution with no CPU fallback. The remaining float arithmetic differs only below the required max gate.

## Route

`FrozenRawMaster -> one resolved Match V4 transform -> CPU canonical decision keys -> DX12 tiled pixel transform -> float32 result`.

Proxy parity and full-resolution parity are recorded separately. This document records full-resolution closure for these four fixtures only; it does not imply universal camera support.
