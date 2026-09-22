# Reference Match v3 Audit

## Baseline

The current matcher uses D65 OKLab for per-pixel transform, weighted multi-reference source analysis, a monotonic luminance quantile curve, soft skin/neutral/highlight protection, and gamut-mapped output through `OklabColorSpace.ToSrgbGamutMapped`.

## Remaining closure work

- Exposure normalization currently uses histogram quantiles for tone mapping, but does not yet expose explicit P05/P50/P95 diagnostics.
- Neutral, skin, and highlight protection are heuristic soft weights in OKLab; no face-recognition or semantic AI claim is made.
- Multi-reference weighting is normalized by `ReferenceLook.Normalize()` and reference analysis is reusable, but no persistent cache telemetry is emitted yet.
- Preview/export parity is covered by the shared matcher/LUT path; performance and clipping fixtures still need a dedicated evidence run.

This audit intentionally records the implementation boundary honestly; it is not a v3 completion claim.
