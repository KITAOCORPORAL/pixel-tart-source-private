# Reference / Film Render Pipeline

Order: source display proxy → reference-color transform → Film profile → Halation → Bloom → grain → vignette → surface/texture → optional post-processor.

Reference color is OKLab D65 with weighted multi-reference target statistics. Preview samples a 33³ LUT; export writes a 65³ color-only LUT. Film effects operate on pixels and never enter the LUT.

Film luminance and highlight masks use linear-light sRGB. Halation and Bloom use separate spatial spreads; grain is monochrome, luma-modulated and deterministic; surface texture is multi-scale procedural noise. Output returns to sRGB encoding.

States: each render owns a revision and linked cancellation token. Old revisions and mismatched assets cannot replace the current preview. Errors retain the original and expose Retry.

Do preserve identity at 0%, cancellation and deterministic seeds. Don't process every slider movement as an uncancellable 24 MP export or claim GPU behavior while the backend is CPU. Implementation: `ReferenceLookPreviewService`, `ReferenceLookMatcher`, `PixelTartFilmPipeline`.
