# Match V4 real RAW Phase 3 acceptance

The external company directory contained five read-only RAW files: two Fujifilm X-T5 RAF, one Fujifilm GFX100S RAF, one Canon EOS R6 CR3, and one Sony ILCE-7M4 ARW. The existing real-RAW runner decoded four files with LibRaw ProfessionalDecode and wrote external TIFF16 artifacts; Sony remained Decode FAIL. Its route is a Core/match-v3 runner, not the V4 GPU product route.

| Camera | ProfessionalDecode | RGB48/float32 | Match v3 | TIFF16/read-back | parity | V4 GPU product route |
|---|---|---|---|---|---|---|
| X-T5 #1 | PASS | PASS | PASS | PASS | PARTIAL, max `.016641` | NOT RUN |
| X-T5 #2 | PASS | PASS | PASS | PASS | PARTIAL, max `.051715` | NOT RUN |
| GFX100S | PASS | PASS | PASS | PASS | PASS, max `.010348` | NOT RUN |
| EOS R6 | PASS | PASS | PASS | PASS | PARTIAL, max `.050827` | NOT RUN |
| Sony ILCE-7M4 | FAIL | NOT RUN | NOT RUN | NOT RUN | NOT RUN | BLOCKED_BY_LIBRAW_0_21_1 |

The GFX100S run measured 102.07 MP and completed the existing Core runner in 168,875.9 ms with a 12,282 MB working set. This is not a GPU timing and is not relabeled as one. RAW binaries and 1.2 GB of external TIFF output remain outside Git.

No real-camera gate is newly closed in Phase 3.
