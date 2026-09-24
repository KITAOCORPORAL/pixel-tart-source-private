# Color Studio Phase 2 Known Issues

## P1

- Phase 1 native node drag threshold, insertion line, drop, and final processing order remain unverified on the current source SHA. This blocks production Phase 2 WPF integration.
- No production WPF 3D panel or native orbit/zoom interaction is implemented yet.

## P2

- End-to-end 3D rendering, 1180×720 layout, accessibility focus traversal, visual evidence, and large-target memory telemetry remain outstanding.
- Rendering technology choice remains bounded WPF 3D prototype versus projected 2D fallback; no new framework has been added.

## Baseline regression debt observed during overnight run

- Full Core suite: 1409 passed, 7 failed, 1 skipped. The seven failures are pre-existing Photography evidence hash sealing checks and legacy dark-theme literal assertions; no Phase 2 file is in their failure paths. They are not silently reclassified as PASS.
- Targeted Phase 2/Core suite: 29/29 PASS. Color Studio WPF suite: 41/41 PASS.

## Deferred / hardware gate

- Physical 125%/150%/200% monitor validation remains a release-hardware gate.
- GPU acceleration, before/after dual clouds, and complex mesh/volume/particle views are out of scope for this slice.
