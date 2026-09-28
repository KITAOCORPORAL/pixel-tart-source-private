# Match V4 high-precision GPU parity

Phase 2 adds the contract required for float32 parity but does not claim a completed GPU result.
The existing V4 tests compare CPU/GPU after 8-bit quantization for representative synthetic
scenes. New product acceptance remains open until the backend returns a full float32 processed
buffer and metrics report RGB absolute error plus OKLab mean/P95/P99/max.

Required datasets: neutral/RGB ramps, skin-like, saturation, shadows, highlights, smooth
gradients, checker/high-frequency, then external GFX100S/X-T5/EOS R6 frozen masters. Sony remains
blocked by LibRaw and is not a GPU blocker.
