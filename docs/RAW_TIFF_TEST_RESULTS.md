# RAW / TIFF test results

Current automated evidence:

- LibRaw capability advertises the candidate extension set and does not claim Sony/Canon fixture verification.
- Synthetic 3×2 RGB buffer exported as 16-bit uncompressed TIFF; dimensions, bit depth and header validated. Invalid ICC payload is rejected. Valid ICC round trip remains unverified.
- TIFF cancellation before pixel writing raises `OperationCanceledException`.
- Release x64 solution build: 0 warnings, 0 errors.
- ComputeSharp-DX12 hardware smoke test and eight-scene CPU/GPU parity: PASS on the current host; this covers the pairwise OT kernel while the remaining V4 stages retain the shared CPU path.

Not yet verified: vendor RAW full decode, RAW → Color Studio → 16-bit TIFF end to end, LZW/ZIP compression, ICC colourimetric round trip, camera metadata preservation and reload.
