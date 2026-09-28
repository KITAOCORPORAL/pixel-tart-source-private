# Match V4 Phase 4 acceptance

## Proven in this phase

- Core Release build: 0 warnings, 0 errors.
- WPF Release build: 0 warnings, 0 errors.
- Existing focused V4 and RAW product tests: 14 passed, 2 GPU-dependent tests skipped.
- Focused RAW/WPF product tests: 6 passed.
- Stable Match v3 remains the default selection.
- V4 Beta selection and AUTO/CPU status are part of the real Color Studio view model and XAML.
- Frozen RAW preview and V4 TIFF16 export share one decode generation and one resolved transform.
- GPU unavailable or pixel execution failure falls back to CPU without re-analysis.
- Atomic TIFF16 writer remains the only publication path.

## Not claimed

- This is not a claim that all real company cameras passed the V4 GPU route. External RAW files
  are not present in the repository and the Sony A7 IV remains blocked by LibRaw 0.21.1.
- 102MP GFX100S live GPU timings, VRAM peak, OOM/device-lost injection, and 24/45/60/102MP
  performance numbers require the opt-in hardware/corpus run.
- Color Range, Film, ICC, complete EXIF propagation, multi-reference V4, and 3D rendering are
  deferred to Color Studio Professional closure.

Final product status: `MATCH_V4_GPU_BETA_READY` for the guarded opt-in route, not a replacement
for the Stable engine.
