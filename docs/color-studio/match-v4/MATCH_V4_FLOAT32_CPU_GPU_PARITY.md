# Match V4 float32 CPU/GPU parity

## Current evidence

| Contract | Result |
|---|---|
| CPU float32 pixel oracle | PASS — focused contract tests |
| Transform/processing-generation preservation on fallback | PASS — focused contract test |
| DX12 adapter compilation | PASS — Release, 0 warnings, 0 errors |
| DX12 runtime float32 parity | PASS — GTX 1650 synthetic 256x192 run; mean `1.7353e-7`, P95 `5.6624e-7`, P99 `1.2666e-6`, max `6.7074e-6` |
| Real RAW parity | NOT RUN — no real RAW fixture is in the repository |

The runtime result is an adapter-level synthetic result. No Color Match product gate is closed by this document; a GPU parity result is not a WPF/TIFF16 product route result.
