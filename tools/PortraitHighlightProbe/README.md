# Portrait highlight research CLI

Bounded headless Core probe; no WPF windows, RAW decode or product recipe registration.

Input arguments: input.f32 output-directory width height EV domain [trace] [candidate-recipe.json] [icc=path]. Domain is encoded-srgb or linear-prophoto-d65. Input little-endian interleaved float32, side<=512 and area<=512x512. Exact length, finite data and domain checked first.

Trace observes real production functions and writes double observations plus float32 materialization. Rendered RGB16 cannot be relabeled as scene ProPhoto. The candidate class is compiled only into this research tool and tests. Saved JSON is accepted by a fresh process. Product TiffExport writes candidate RGB16; source ICC bytes are independently checked by runner.

See docs/research/portrait-highlight-20261111/REPRODUCE.md. Photos, rendered rasters, float arrays and GPL reference tools stay local.
