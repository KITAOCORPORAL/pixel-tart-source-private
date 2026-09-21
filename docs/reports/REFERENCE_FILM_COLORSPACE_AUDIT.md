# Reference / Film Color-Space Audit

Status: audited against product source after `8c2999378651cbb3704d7a80d3512a6d485d67ef`.

| Stage | Space / encoding | Evidence |
|---|---|---|
| Reference analysis | color-managed display proxy | `ReferenceLookPreviewService.Prepare` |
| Tone calculation | OKLab D65 lightness and histogram quantiles | `ReferenceLookMatcher`, `ReferenceToneMapper` |
| Color fit | OKLab D65 global and three smooth tonal zones | `ReferenceColorTargetBuilder` |
| LUT input | normalized display-referred sRGB | `ReferenceCubeLutBuilder` |
| Preview LUT | 33³, trilinear sampling | `ReferenceLookPreviewService` |
| Export LUT | 65³ color transform only | `BuildExportLutAsync` |
| Film luminance | linear-light sRGB, Rec.709 coefficients | `PixelTartFilmPipeline.SrgbToLinear` |
| Profile | small Pixel Tart-owned bias in linear working values | `PixelTartFilmPipeline.Apply` |
| Halation mask | linear highlight mask, spatial Gaussian spread, warm edge composite | `halationSpread` |
| Bloom mask | linear highlight mask, separable Gaussian spread, neutral composite | `bloomSpread` |
| Grain | monochrome luma-modulated deterministic noise in linear working values | `SmoothNoise` |
| Surface | Pixel Tart procedural multi-scale deterministic noise | `Surface` |
| Output | sRGB transfer encoding | `LinearToSrgb` |

The source remains immutable. Pixel effects are applied after reference color and are not encoded into `.cube` exports. This pass intentionally avoids rewriting the full reference engine; it corrects physically sensitive highlight and luminance decisions while preserving its verified OKLab path.
