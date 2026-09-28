# X-Trans minimal reproduction

The thin reproduction is `tools/LibRawCompatibilityProbe`: it calls only the product's
`LibRawDecoder` boundary, requests `ProfessionalDecode` with `AutoRotate=false`, copies the
complete RGB48 `ushort` sample buffer, and hashes it. It does not load WPF, Match, preview, or
TIFF code. It records both one decoder reused for 20 serial calls and a new decoder for each of
20 calls.

The existing private run remains the observed result: both X-T5 fixtures produced 10 distinct
hashes under both managed lifetime strategies, while controlled Canon and GFX pairs were stable.
This narrows the issue below the WPF/Match/TIFF layers and rules out managed decoder dictionary
reuse as the sole explanation, but it does not prove the exact native cause. Padding, native
buffer ownership, LibRaw RAF processing, and uninitialized state remain hypotheses until the
probe is run with a separately instrumented native candidate.

No threshold was relaxed and no X-Trans conversion was changed.
