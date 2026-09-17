# Visual intelligence pipeline

`AssetItem` / source fingerprint → `WpfVisualAnalysisDecoder` (background decode, ≤512-pixel proxy, ICC conversion where supported) → `IVisualAnalysisService` → versioned SQLite result/features cache → contextual library, board, canvas and duplicate consumers.

The engine takes an RGB24 buffer and emits palette, histograms, final-image zone distribution and similarity signatures in one pass. Board aggregation weights each photograph as one contributor. Asset ID plus source fingerprint prevents a changed source from silently reusing stale results; version bumps invalidate algorithm results. Canvas stores a reference-only palette object with source IDs. Duplicate matching consumes cached analysis, never a full decode merely for opening a context menu. Its scan still enumerates candidates and usages sequentially; large-library performance requires measurement and indexing before release.

Boundaries: decoded proxy measurements are objective approximations of current output appearance, not RAW scene exposure, aesthetic scoring or AI semantics. Project palette/tone references and Planning persistence are not wired yet.
