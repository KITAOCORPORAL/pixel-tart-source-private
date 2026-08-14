# Asset Library Visual Analysis

Status: V1.6 development preview. All analysis is local and read-only. It never changes source pixels, EXIF, tags or files.

## Pixel contract

The Core engine accepts an immutable RGB24 buffer no larger than 512 px on its long edge. The caller must declare that pixels were converted to the named analysis profile; otherwise analysis is rejected. The WPF adapter attempts embedded-profile conversion and records `UnknownAssumedSrgb` when no supported profile is available. This path has no real ICC reference-sample certification yet and must not be described as fully color-managed.

RAW files use an existing rendered proxy or embedded preview only. The preview does not demosaic RAW and reports analysis as unavailable when no proxy exists. The recorded source kind distinguishes `RasterOriginal`, `RenderedProxy` and `EmbeddedPreview`.

## Algorithms

- Palette: deterministic CIELAB clustering for requested maxima 3, 5 or 7, capped at 16,384 stratified samples, at most 20 iterations, and near-centroid merging. Weights are normalized and sorted by weight.
- Harmony: a tendency label only (`Neutral`, `Monochrome`, `Analogous`, `Complementary`, `Triadic` or `Mixed`); it is not an aesthetic judgment.
- Histograms: exact 256-bin R, G, B and luma arrays over all decoded pixels.
- Luma: sRGB channels are linearized before Rec.709 weights `0.2126 R + 0.7152 G + 0.0722 B`. Tone thresholds are defined on that linear-light 0–255 scale; gamma-encoded 50% gray is explicitly tested as mid key.
- Tone: fixed five-zone ratios, black/white clipping ratios, percentile contrast/span, saturation and warm/cool tendency. These are final-pixel statistics, not camera exposure, Kelvin temperature or sensor dynamic range.

## Canonical searchable features

V1.6 introduces `AssetVisualFeatures` as the single searchable contract. Its canonical variant is fixed to palette size 5, weight order and `visual-analysis-v2`; changing the Inspector's 3/5/7 or sort choice cannot replace it. The feature row contains queryable classifications and scalar statistics, signatures, source/proxy fingerprints and a child palette table. `Valid`, `Stale`, `NotAnalyzed` and `Failed` are derived by comparing the current imported source fingerprint with the feature provenance and analysis version. A missing analysis never appears as numeric zero.

Hue ranges are circular, so `350..20` crosses zero. Dominant hue requires a chromatic palette color with at least 15% weight; neutral black/white/gray is never treated as red hue zero. Lab/DeltaE76 color search can still find neutral colors. Similarity uses explicit 0–100 Color (weighted palette, hue and warm/cool), Tone (normalized luma histogram, zones and key), Contrast, and Saturation subscores with final weights 40/30/20/10. SQLite performs bounded multi-route candidate pruning and Core ranks at most 100 results with self exclusion and stable GUID tie-breaking.

## Cache and cancellation

`AssetVisualAnalysis` keeps Inspector cache variants keyed by stable `AssetId`, decoded-proxy content hash, `visual-analysis-v2`, palette size and palette sort. The searchable `AssetVisualFeatures` row is independent and canonical. The WPF decoder takes a stable in-memory read-only snapshot, hashes the exact source snapshot and decoded RGB proxy, and never stores either byte buffer. Publishing features verifies that the imported fingerprint still matches; it never overwrites a newer import/relink fingerprint. A per-asset async lock prevents overlap and removes idle locks safely. Selection and batch changes cancel decode and analysis; publication rechecks asset/generation. Multi-selection never presents the first asset as group analysis.

Batch analysis uses bounded workers, priority order, per-item failure isolation and cancellation. Current-selection analysis cancels same-asset background work and uses an independent interactive path.

## Verification boundary

Deterministic generated buffers cover black, white, 50% gray, primary colors, gradient, palette weights, cache hit/miss/version invalidation and A→B cancellation. The 100/1000-image test is an in-memory Core microbenchmark only. Real JPEG decode + SQLite cache + UI switching at 100/1000 scale, ICC reference samples and 10,000-JPEG performance evidence remain deferred.
