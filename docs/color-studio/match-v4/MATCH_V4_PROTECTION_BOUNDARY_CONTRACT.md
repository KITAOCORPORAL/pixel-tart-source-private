# Match V4 Protection Boundary Contract

## Canonical decision contract

`ProtectionClassifierContract` is the single source of truth for hard V4 protection decisions. CPU computes the decision from double precision OKLab before GPU dispatch. The GPU tile receives the integer decision key and does not re-run `atan2`, skin comparisons, or `L * 3` zone selection.

- epsilon: `1e-4`
- skin luminance: `L > 0.2801 && L < 0.8999`
- skin hue: `hue > 25.0001 && hue < 79.9999`, hue normalized to `[0, 360)`
- skin chroma: `chroma > 0.0251 && chroma < 0.2199`
- luminance zone: `clamp((int)(L * 3), 0, 2)`
- decision key: zone in bits 0–1, skin bit in bit 2
- neutral, highlight and shadow weights remain continuous float/double calculations and are not quantized

The key is a branch decision encoding. It does not quantize input RGB, OKLab, mapped color, or output RGB.

## First divergence evidence

The pre-fix full-resolution forensics showed stable outliers at skin hue values around `79.9989°–79.9999°`. CPU and GPU used equivalent-looking inequalities over different precision values; one side selected skin protection and the other did not. Tile-size changes preserved the coordinates, ruling out tile geometry as the first cause. The first divergent semantic stage was therefore `ProtectionClassifier.SkinHueBoundary`, with `L×3` luminance zone selection as the second independent hard branch under review.

The post-fix GPU path consumes the CPU decision key. The four real RAW full-resolution reruns reduced all `>1e-4` counts to zero without changing the gate.

## Scope

Match v3 remains Stable/default. Match v4 remains guarded Beta. ICC and EXIF propagation remain separate partial gates.
