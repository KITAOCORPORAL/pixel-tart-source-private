# PixelTartProductVisualHarness — RC12

This release-only producer launches the real UI-review build of `App.xaml` and `MainWindow` with the Pixel Tart dark design system and synthetic JPEGs. Every capture owns a new application process and isolated profile; the previous process must exit before the next fixture starts.

Run `Invoke-RC12ProductVisualHarness.ps1`. It produces the required 12 product screenshots, ten Asset Library UX closure screenshots, the current 100/125/150/200 logical-DPI matrix, six Asset Library resolution captures, an aspect-ratio after capture plus the preserved previous-HEAD baseline, per-capture metadata, process identities, source hashes and `rc12-product-visual-evidence.json` under `artifacts/rc12-product-visual`.

Validate a sealed run against the current repository HEAD with `Test-RC12ProductVisualEvidence.ps1`. The validator accepts only the RC12 12/10/32/6/1 contract and verifies source revision, file hashes, layout/theme results, source-media integrity, the before/after ratio evidence, and that every capture process exited before the next fixture.

The producer never reads customer media and does not treat the historical 2.0.4 evidence as an RC12 pass.
