# Match V4 Phase 4 product route

Status: `MATCH_V4_GPU_BETA_READY` for the opt-in route; Match v3 remains Stable and the default.

The Color Studio Professional workspace now exposes two engines:

- `Stable` keeps the existing Match v3 renderer and export behavior.
- `MatchV4Beta` uses one high-precision analysis and one immutable `MatchV4ProductSession`.

The V4 session is created from a `FrozenRawMaster` and a selected reference buffer. It fixes the
source SHA, decode generation, processing generation, reference identity, settings, and analysis
transform. Preview proxies and full-resolution TIFF16 export execute that same transform. A GPU
failure does not re-run analysis; the pixel executor falls back to CPU with the same transform and
generation. AUTO only selects the DX12 backend after its capability smoke test; CPU is always
available and can be selected explicitly.

The UI deliberately exposes only Stable/V4 Beta, AUTO/CPU, and a short GPU/CPU status. DX12,
ComputeSharp, tile size, and VRAM details remain diagnostics. Strength is 0–100% and is part of
the immutable execution transform; Keep Luminance is passed to both CPU and GPU pixel semantics.

Known limits: V4 product preview/export currently requires a single online reference image and
does not claim Color Range, Film, ICC, or complete EXIF product parity. Those routes remain on the
existing stable stack until Color Studio Professional closure. Sony A7 IV remains blocked by the
known LibRaw 0.21.1 limitation. Real-camera V4 GPU acceptance and 102MP live telemetry still need
an opt-in run with the external company RAW corpus.
