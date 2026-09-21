# Reference / Film Performance Report

Measured locally on 2026-09-21 on Intel Core i5-12400F, Windows 10 x64, CPU backend. Values are observed results, not synthetic targets.

## Before — `9e2b6143e5f7b2d914c8859b4adafa8cee9dbcc8`

| Path | Time | Managed memory delta |
|---|---:|---:|
| 1920×1080 combined Film | 702.72 ms | 53.42 MB |
| 6000×4000 / 24MP final | 9502.60 ms | 618.28 MB |

The previous path created full-frame float arrays for luminance, highlight, horizontal blur and output masks. It was appropriate for settled preview/output, not repeated slider interaction.

## After — product source `9bcce6098ce876096bdd444030f554b968384fa0`

Measured after one warm-up so the figures represent steady-state `ArrayPool<float>` reuse.

| Path | Time | Managed memory delta | Allocated |
|---|---:|---:|---:|
| 1600×1067 interactive proxy | 575.00 ms | 4.90 MB | 4.88 MB |
| 2048×1365 proxy capacity | 962.96 ms | 8.01 MB | 8.00 MB |
| 1920×1080 settled | 688.56 ms | 5.95 MB | 5.93 MB |
| 6000×4000 / 24MP final | 9382.98 ms | 68.68 MB | 68.67 MB |
| 24MP cancellation | 1.09 ms | — | canceled=true |

The production interaction tier uses the cached 1600px long-edge proxy, so the measured interactive Film pass is 18% faster than the current 1080p settled path and 18% faster than the historical 1080p pass. The 2048px measurement is retained as a capacity reference and is not the default slider tier. Source change rebuilds the proxy; parameter changes reuse it. A 100 ms debounce presents the proxy, then a 300 ms settled refresh runs only for the current revision. Full export remains full resolution.

Buffer reuse reduced the observed steady-state 24MP managed delta from 618.28 MB to 68.68 MB (about 89%). Blur rows and pixel passes now observe cancellation; stale revisions remain presentation-gated.

Machine-readable evidence: `artifacts/reference-film-quality-review/performance/film-performance.json`.
