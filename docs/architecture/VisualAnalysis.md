# Asset Library Visual Analysis

Status: V1.5 development preview. All analysis is local and read-only. It never changes source pixels, EXIF, tags or files.

## Pixel contract

The Core engine accepts an immutable RGB24 buffer no larger than 512 px on its long edge. The caller must declare that pixels were converted to the named analysis profile; otherwise analysis is rejected. The WPF adapter attempts embedded-profile conversion and records `UnknownAssumedSrgb` when no supported profile is available. This path has no real ICC reference-sample certification yet and must not be described as fully color-managed.

RAW files use an existing rendered proxy or embedded preview only. The preview does not demosaic RAW and reports analysis as unavailable when no proxy exists. The recorded source kind distinguishes `RasterOriginal`, `RenderedProxy` and `EmbeddedPreview`.

## Algorithms

- Palette: deterministic CIELAB clustering for requested maxima 3, 5 or 7, capped at 16,384 stratified samples, at most 20 iterations, and near-centroid merging. Weights are normalized and sorted by weight.
- Harmony: a tendency label only (`Neutral`, `Monochrome`, `Analogous`, `Complementary`, `Triadic` or `Mixed`); it is not an aesthetic judgment.
- Histograms: exact 256-bin R, G, B and luma arrays over all decoded pixels.
- Luma: sRGB channels are linearized before Rec.709 weights `0.2126 R + 0.7152 G + 0.0722 B`. Tone thresholds are defined on that linear-light 0–255 scale; gamma-encoded 50% gray is explicitly tested as mid key.
- Tone: fixed five-zone ratios, black/white clipping ratios, percentile contrast/span, saturation and warm/cool tendency. These are final-pixel statistics, not camera exposure, Kelvin temperature or sensor dynamic range.

## Cache and cancellation

`AssetVisualAnalysis` stores result JSON only, keyed by stable `AssetId`, content hash and `visual-analysis-v1`. It never stores source pixels or proxy bytes. A per-asset async lock prevents an older content analysis from overwriting a newer cache entry. Selection changes cancel decode and analysis; publication is marshalled to the captured synchronization context and rechecks asset/generation immediately before publishing. Multi-selection shows an explicit selected-count state and never presents the first asset as group analysis.

## Verification boundary

Deterministic generated buffers cover black, white, 50% gray, primary colors, gradient, palette weights, cache hit/miss/version invalidation and A→B cancellation. The 100/1000-image test is an in-memory Core microbenchmark only. Real JPEG decode + SQLite cache + UI switching at 100/1000 scale, ICC reference samples and 10,000-JPEG performance evidence remain deferred.
