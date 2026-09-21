# Reference Color Engine Audit

The current engine is a real deterministic CPU implementation, not a v2 claim or a third-party reproduction.

- Sampling: the loaded display proxy is converted to RGB24; full preview pixels provide the source target and the existing visual-analysis palette/histogram provides persisted reference evidence.
- Target generation: multiple references are normalized by weight. Their normalized luma histograms, contrast metrics, saturation metrics and OKLab palette samples are combined.
- Tone fit: a bounded monotonic quantile curve maps the source histogram to the weighted reference histogram. Tone strength and “保持原片影调” govern disclosure.
- Color fit: global plus three smoothly overlapping OKLab D65 tonal zones provide `a/b` shifts with confidence weighting and bounded chroma movement.
- Contrast/saturation: ratios are bounded to `0.8–1.2` and `0.7–1.3`; their user strengths are applied independently.
- Protections: skin-like hues, low-chroma neutrals and highlights attenuate the final match strength inside `ReferenceLookTransform.Apply`.
- Multi-reference: weights are normalized in `ReferenceLook.Normalize`; ordering is UI metadata while normalized weights govern the target.
- LUT: the same transform builds 33³ preview and 65³ export LUTs. Film pixel effects are excluded.
- Residual correction: none. No claim is made that a learned or residual engine exists.
- Identity: `MatchStrength=0`, or both tone and color strength at zero, returns unchanged RGB pixels.
- Revision/cancellation: view-model revisions, linked cancellation tokens and asset identity prevent stale renders from replacing the latest result.

Known limitation: palette-based reference chroma statistics are global/zone summaries, not spatial semantic analysis. This is acceptable for the present product scope and should not be described as AI matching.
