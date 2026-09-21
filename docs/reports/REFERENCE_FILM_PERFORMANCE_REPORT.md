# Reference / Film Performance Report

Measured locally on 2026-09-21. This is CPU evidence, not CI or GPU performance.

- Hardware: Intel Core i5-12400F, 6 cores / 12 logical processors, 31.8 GB RAM.
- OS: Windows 10 Pro 10.0.19045 x64.
- Backend: `ReferenceLookPreviewService` / `PixelTartFilmPipeline`, CPU.
- 1920×1080 Film combined effects: 702.72 ms; measured managed-memory delta 53.42 MB.
- 6000×4000 (24 MP) Film combined effects: 9502.60 ms; measured managed-memory delta 618.28 MB.
- Reference-only and combined UI timings: not separately claimed in this pass; visual harness evidence is not a microbenchmark.
- Cancellation: cooperative checks occur during pixel processing and revision protection prevents stale presentation; a stable wall-clock cancellation figure was not established.

Known bottleneck: the separable highlight blur and per-pixel procedural film pass allocate full-frame float masks. The 1080p result is suitable for settled preview but not a 60 fps interactive render. The 24 MP result is an output-quality path, not a real-time target. Future optimization should reuse buffers and render an interactive 1600–2048 px proxy before a settled high-quality refresh.

Machine-readable evidence: `artifacts/reference-film-quality-review/performance/film-performance.json`.
