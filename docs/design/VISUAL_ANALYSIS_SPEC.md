# Visual Analysis v1

Status: implemented core; project-level persistence and several surface controls remain partial.

The same `IVisualAnalysisService` calculates a 3/5/7-color palette, RGB and luminance histograms, clipping ratios, 11 current-image luminance zones, a grayscale/zone-map preview, and perceptual signatures. The library, inspiration-board aggregation, and Free Canvas palette object consume that service. Analysis reads an off-thread, bounded raster proxy and never edits the source. The cache key includes asset identity, decoded-pixel fingerprint, analysis version, palette count and sort mode; the source-file fingerprint is retained to invalidate stale features.

User entry is contextual: library image → visual analysis; inspiration board → analyze board; Free Canvas image selection → palette/combined palette or monochrome reference. The canvas object keeps structured colors and source asset IDs, rather than a rasterized card. A group palette is recomputed from weighted visual samples per photograph, not concatenated swatches. Zone labels 0–X describe the **current rendered pixels**, never original scene exposure.

Acceptance: palette swatches show HEX/HSL/percentage and copy HEX; histogram shows luminance/RGB and clipping; zone distribution and map are non-destructive; board aggregates warm/cool, saturation, lightness and dark/mid/bright ratios. The UI does not assert aesthetic quality. Current limitations: no project palette or tone-target store; no zone-hover highlight control or HSL-copy action; grayscale original/black-and-white switch is a contextual preview rather than a two-up comparison.

Evidence: `artifacts/creative-intelligence-v1/visual-analysis/` contains seven real App/MainWindow logical-simulation captures and adjacent `.png.json` layout metadata. This is not physical DPI or photographer acceptance.
