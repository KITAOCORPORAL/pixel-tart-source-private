# Matching correctness, versioning and remaining comparisons

Call chain: ColorStudioBitmapRenderer → shared ColorStudioRenderPipeline → ordered stack node input → ReferenceLookMatcher → ReferenceLookTransform. Preview/export reuse this stack; inspection overlays are display-only. This is not a second match or export engine.

Old persisted looks default AlgorithmVersion=1. Their existing interpretation is retained; they are not silently upgraded. New locally imported references with actual PixelStatistics produce version2. Version2 validation requires real statistics. Parameters and statistics round-trip through the existing look store.

Version1 used ProcessingAnalysis.HistogramLuma, which represents linear Y, to build a curve later indexed by OKLab L. It could also carry original-image analysis past earlier adjustment nodes. Version2 builds target statistics from the actual current node input and reference statistics from shared ICC/orientation-normalized sRGB proxy pixels. Statistics, quantile curve and application now use OKLab L consistently. Encoded RGB display histograms and linear Y analysis remain distinct valid products; neither is relabeled OKLab L.

ReferencePixelStatistics stratifies at most65536 proxy positions, weights opacity, excludes alpha0/non-finite pixels, computes smooth shadow/mid/highlight weights and effective sample confidence. Multiple references combine normalized pixel distributions using normalized look weights. Empty/transparent statistics reject non-identity matching clearly; Strength0 exits before target statistics/math and preserves RGB/alpha. Byte, float and shared-renderer identity tests cover this contract. Cancellation checked in sampling/render loops. Shared reference cache includes path/length/mtime, bounded32 entries; it is in-process only and not a promise of persistent semantic caching.

Existing SmoothZoneWeights, confidence, contrast/saturation bounds and bounded shift limits remain. New continuous color-based skin weight transitions avoid binary hue cutoffs; color protection is separated from original-tone preservation. This is a color heuristic, not semantic person segmentation. Saturated clothing/background with similar colors can also receive protection. Floating output uses proper sRGB encoding rather than exposing linear values as encoded display bytes. Alpha length is validated.

Known limits: float **encoded display-referred sRGB**, not scene-linear sensor RAW/wide gamut; sampled proxies do not recover clipped sensor values. Small-image/corrupt/constant/transparent and version/domain tests are automatic evidence, not aesthetic approval. New domain correction may change newly created version2 looks as intended; old version1 images remain old behavior.

## Unfinished within confirmed scope

No empirical partitioned covariance/Bures candidate comparison has been implemented or measured. No real portrait/dark skin/flash/color-light/saturated garment set was approved. No comparative failure-case report or native new-release export files exist. Thus C07–C10 remain PARTIAL, not PASS. Do not imply quantile+bounded zones are equivalent to arbitrary covariance transport or decomposable entirely into three wheels. A LUT cannot represent grain/halation/masks/spatial effects. Existing V4 unsupported-stack feedback and preservation are retained, but must be tested natively before closure.
