# PixelTartProductVisualHarness — RC12

This release-only producer launches the real UI-review build of `App.xaml` and `MainWindow` with the Pixel Tart dark design system and synthetic JPEGs. Every capture owns a new application process and isolated profile; the previous process must exit before the next fixture starts.

Run `Invoke-RC12ProductVisualHarness.ps1`. It produces 84 captures: 12 product screenshots, ten UX simplification screenshots, eleven Asset Library UX closure screenshots, ten Free Canvas states, two additional contextual-inspector states, 32 current 100/125/150/200 logical-DPI captures, six Asset Library resolution captures and one aspect-ratio after capture. It also retains the previous-HEAD baseline and writes per-capture metadata, process identities, source hashes and `rc12-product-visual-evidence.json` under `artifacts/rc12-product-visual`.

Validate a sealed run against the current repository HEAD with `Test-RC12ProductVisualEvidence.ps1`. The validator requires all 84 captures and verifies source revision, file hashes, layout/theme results, source-media integrity, the before/after ratio evidence, actual submenu/preview visibility and leave-close state, canvas object/preview/save/transform state, and that every capture process exited before the next fixture. Windows may reuse an exited process ID; lifecycle exit acknowledgments are authoritative.

The producer never reads customer media and does not treat the historical 2.0.4 evidence as an RC12 pass.
