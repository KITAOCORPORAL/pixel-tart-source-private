# Reference Match V3 / V4 quality report

Generated from 20 deterministic synthetic scenes. The corpus covers neutral, skin-like, highlight, shadow, saturation, mixed-lighting, colour-temperature, primary-colour, gradient, mixed-frequency and gamut-stress cases. Scene 01 comparison images are stored as PPM artifacts beside the test output: target, reference, V3 and V4 CPU.

The JSON report records per-scene OKLab error, neutral drift, highlight contamination, shadow oversaturation and gamut clipping. V4 is measured against the same target/reference as V3; this is quantitative evidence, not a visual-only claim.

GPU comparison is pending because no validated GPU backend exists. Preview/export parity continues to use the shared `ColorStudioRenderPipeline` and is not claimed as a separate V4 GPU result.
