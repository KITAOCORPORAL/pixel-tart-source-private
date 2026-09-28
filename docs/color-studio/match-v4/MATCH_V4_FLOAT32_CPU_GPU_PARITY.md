# Match V4 float32 CPU/GPU parity

## Current evidence

| Contract | Result |
|---|---|
| CPU float32 pixel oracle | PASS — focused contract tests |
| Transform/processing-generation preservation on fallback | PASS — focused contract test |
| DX12 adapter compilation | PASS — Release, 0 warnings, 0 errors |
| DX12 runtime float32 parity | NOT RUN — no recorded hardware execution in this checkout |
| Real RAW parity | NOT RUN — no real RAW fixture is in the repository |

No Color Match gate is closed by this document. A GPU build is not a GPU parity result.
