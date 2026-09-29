# Current color architecture

## Verified flow

```text
source / RAW decode
  -> preview or high-precision image buffer
  -> Reference Match / Match v3 or v4 boundary
  -> preset or LUT processor
  -> PixelTartFilmPipeline (color + spatial stages)
  -> output renderer / TIFF16 or JPEG publishing
```

The repository currently contains meaningful but separate implementations. This document is an inventory, not a claim that every branch is product-complete.

| Capability | Current implementation | Status |
|---|---|---|
| Match v3 | Reference workspace and production path | IMPLEMENTED |
| Match v4 / DX12 | Core and representative DX12 work | PARTIAL |
| Adobe XMP | Parser, mapping, persistence, strength interpolation | PARTIAL |
| Cube LUT | Parser, CPU processor, cache and cancellation | PARTIAL |
| Capture One | Explicit importer boundary, no verified fixture | SPEC_ONLY |
| Creative Film | `PixelTartFilmPipeline` with grain, halation, bloom, vignette, surface | PARTIAL |
| Spectral / Instant Film | Research and roadmap only | SPEC_ONLY |
| ICC registry | Profile discovery/description | PARTIAL |
| ICC conversion | Renderer-facing output handling; conversion acceptance incomplete | PARTIAL |
| RAW high precision | Existing high-bit-depth infrastructure; full compatibility remains open | PARTIAL |
| TIFF16 | Existing writer and publishing route | PARTIAL |
| 3D Color Space core | Sampling/model/test foundation | PARTIAL |
| 3D Windows renderer | No production renderer | NOT_IMPLEMENTED |

## Boundaries

Reference Match, Preset/Look, and Film Process remain separate product concepts. LUTs may represent color transforms but do not replace spatial grain, halation, bloom, diffusion, or texture. `ColorProfileRegistry` describes profiles; pixel conversion belongs to the rendering/output boundary.

## Duplicate systems observed

- Reference preview, LUT preview, and preset preview each own cancellation/revision or cache behavior.
- Film rendering is a separate pipeline from LUT processing.
- Publishing owns output verification and atomic file movement.

These are inventory findings only. No second engine is introduced in this phase.
