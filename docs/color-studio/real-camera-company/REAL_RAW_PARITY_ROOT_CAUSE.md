# Real RAW parity root-cause closure

Status: **PARTIAL — legacy metric root cause proven; product cross-decode RAF risk remains open.**

The immutable baseline reported `.048709` (X-T5 #1), `.053092` (X-T5 #2) and `.050713` (EOS R6) maxima. Those values are preserved as BEFORE. A staged diagnostic found the first divergence at the display/comparison boundary, not in Match v3:

1. Full ProfessionalDecode versus a second decode: Canon and GFX were identical; X-T5 was sparse non-deterministic (see [determinism](FUJI_REPEAT_DECODE_DETERMINISM.md)).
2. Same full float master versus its 1600 proxy: zero difference at the selected source samples.
3. Proxy float versus its own RGB24 display encoding: maximum quantization error reached `.051365` for X-T5 #1 and `.045667` for Canon.
4. Legacy preview RGB24 versus full TIFF float sampled nearest source pixels: `.048300` / `.036945` / `.004804` / `.050767` maxima.
5. Same-resolution comparison after applying the identical RGB24 display encoding to the full output: maxima `.010271`, `.008307`, `.007007`, `.011149`; all four are within the existing `.012` tail gate in the controlled same-buffer diagnostic.

**PROVEN:** the old preview↔full metric was not comparing equivalent spatial/display representations. It conflated 8-bit display quantization and proxy/full-resolution sampling with product color error. The correct contract is `DISPLAY_PARITY` (same-resolution display representation) plus `EXPORT_PRECISION` (full-resolution float/TIFF16 read-back). The old metric remains in evidence for history.

**NOT PROVEN:** the product commands currently decode preview and export separately. X-T5 #1's independent product run still had canonical max `.033891` (80 pixels > `.012`), and the RAF itself is non-deterministic across native decode calls. Therefore this change does not claim all product parity is fixed. The safe next fix is decoder lifecycle/determinism isolation or a frozen master handoff—not a threshold change.

No Match v4/GPU work, threshold widening, max-gate deletion, or algorithm rewrite was done.
