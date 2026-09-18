# Reference Look Pipeline

```text
stable reference(s)
  -> cached visual analysis (palette / histogram / zones)
  -> normalized source weights
  -> input interpretation / working sRGB proxy
  -> bounded tone curve
  -> deterministic Lab color-distribution mapping
  -> heuristic skin + highlight protection
  -> optional existing LUT
  -> existing display profile transform
  -> frozen in-memory preview
```

`ReferenceLook` and `ReferenceLookStore` live in Core; WPF bitmap conversion lives in `ReferenceLookPreviewService`. The matcher takes immutable RGB bytes and returns a new buffer. Persistence uses a per-path lock, temporary file, flush and atomic replace.

The algorithm separates tone strength from chroma strength. Reference darkness is bounded rather than treated as exposure instruction. `ToneStrength=0` bypasses tone changes; `MatchStrength=0` returns an exact source copy. All calculations are deterministic and CPU-capable.

The reference stage is inserted before the existing optional LUT/display pipeline. It does not create another ICC transform or a second display cache. It never writes source paths.
