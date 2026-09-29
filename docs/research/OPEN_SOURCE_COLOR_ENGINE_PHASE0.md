# Open-source color engine research — Phase 0

## Decision boundary

Phase 0 is research only. No external implementation is copied into Pixel Tart and no dependency is added from a name-only match. Production adoption requires: license compatibility, Windows x64 deployment review, color-science equivalence, deterministic fixtures, performance evidence, and a rollback path.

## Candidate matrix

| Candidate | Potential use | Current decision | Required evidence |
|---|---|---|---|
| OpenColorIO | Explicit color-management transforms and config graphs | Research only | pinned release/license, config portability, ICC/OCIO equivalence |
| Colour Science | Reference colorimetry and Lab/OKLab validation | Research only | compatible runtime strategy, numerical comparison to Core constants |
| darktable filmic / filmic RGB | Tone-mapping and scene/display-referred study | Research only | algorithm attribution, GPL compatibility review, high-precision comparison |
| Spektrafilm | Film-character research | Research only | source provenance, license, characterization data, no LUT-only shortcut |
| Adaptive 3D LUT | Efficient learned/optimized transforms | Research only | training-data provenance, interpolation error, out-of-gamut policy |
| NeuralPreset | Preset suggestion/representation research | Research only | model/data license, offline packaging, explainability and deterministic inference |
| ColorTransferLib | Color-transfer algorithm comparison | Research only | license, algorithm-specific fixtures, artifact/error analysis |

## Film Lab / instant film

Film Lab is a product architecture layer above color science: characterization, exposure response, grain, halation, color cross-talk, and print/display intent must remain independently testable. Instant-film or Polaroid emulation cannot be reduced to a yellow LUT and a white border; it needs a documented response model and artifact policy.

## Current Pixel Tart boundary

Pixel Tart continues to use its existing Core color contracts, Match v3/v4 paths, and bounded 3D color-space sampling. The new `ColorProfileRegistry` makes profile availability explicit and keeps ICC assignment distinct from conversion. No research candidate is claimed as integrated or production-ready.
