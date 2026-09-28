# Match V4 GPU Outlier Forensics

## Closed result for the four real fixtures

The pre-fix outliers were stable across tile sizes and clustered at protection boundaries, especially skin hue near 80 degrees. The first divergent stage was the hard protection classifier: CPU double and GPU float could choose different skin or luminance zone branches.

The fix moved hard branch decisions into `ProtectionClassifierContract`. CPU computes one integer decision key from double precision OKLab; the GPU receives that key per tile. Final color arithmetic remains GPU float. No parity threshold was changed.

Post-fix full-resolution, three-repeat results:

| Fixture | Max | Pixels > 1e-4 | Route |
|---|---:|---:|---|
| Canon EOS R6 CR3 | 2.00e-6 | 0 | GPU, no fallback |
| Fuji X-T5 RAF #1 | 1.31e-6 | 0 | GPU, no fallback |
| Fuji X-T5 RAF #2 | 1.19e-6 | 0 | GPU, no fallback |
| Fuji GFX100S RAF | 1.74e-6 | 0 | GPU, no fallback |

Proxy and full-resolution are reported separately. The four-fixture result closes the numerical gate for these fixtures; it does not claim universal camera or cross-hardware support.

Status: **FULL_RES_PARITY_CLOSED for listed fixtures**.
