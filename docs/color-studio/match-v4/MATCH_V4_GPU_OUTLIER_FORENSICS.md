# Match V4 GPU Outlier Forensics

## Result

Full-resolution forensics on RTX 5060 Ti remains **PARTIAL**. Proxy parity is within the unchanged gate, but full-resolution runs still contain deterministic outliers above `1e-4`.

The runner compared 256, 512, 1024 and 2048 tile sizes. Where each run completed, outlier locations and magnitudes were stable across tile sizes. This rules out the currently tested tile edge, row pitch, tile origin, copy-back, and dispatch bounds hypotheses.

Representative full-resolution evidence:

- X-T5 `DSCF0347.RAF`: 12 pixels above `1e-4`, maximum about `0.0597`, with representative source hue `79.9989°`.
- GFX100S `LMAN1714.RAF`: 4 pixels above `1e-4` in the completed 256/512/1024 runs, maximum about `0.04`; the 2048 run produced no outlier in that execution and is not evidence of closure.

The CPU path remains the oracle. The shared `ProtectionBoundaryEpsilon = 1e-4` contract is retained, but the evidence shows that the remaining float/double protection classification is not fully identical at full resolution.

## Not established

This run does not establish full-resolution GPU parity closure, pixel-consistent export parity, or cross-hardware parity. No threshold was relaxed and no RAW/TIFF artifact is part of the repository.

**Status: PARTIAL / BLOCKED.**