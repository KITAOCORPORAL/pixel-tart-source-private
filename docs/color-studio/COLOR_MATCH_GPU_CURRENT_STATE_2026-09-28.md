# Color Match / GPU current state — 2026-09-28

Before: **12 / 28 = 42.9%**. After this company-machine integration: **14 / 28 = 50.0%**, narrowly scoped to synthetic product tests. The exact 28 item definitions remain in [the 2026-09-27 matrix](COLOR_MATCH_GPU_CURRENT_STATE_2026-09-27.md); this update does not rewrite historical evidence.

| Gate | Before | After | Current evidence / limit |
|---|---:|---:|---|
| 24 Product RAW16 → match → TIFF16 workflow | 0 | 1 | WPF target/import/export command plus professional fake decoder; float Match v3 and atomic TIFF16 read-back. One synthetic product scenario; no real camera certified. |
| 28 High-precision product preview/export parity | 0 | 1 | Same frozen Match v3 state through product WPF preview and TIFF16 export; synthetic full-resolution fixture meets mean `.003`, P95/max `.012` OKLab thresholds. Scaled proxy, real RAW and other enabled adjustment nodes remain open. |

Every other gate is unchanged, in particular 13–21 (Match v4/GPU production), 22–23 (legal-camera professional decode and verified coverage), 25 (ICC color conversion), 26 (camera EXIF/orientation preservation as a whole), and 27 (real-photo performance). Product Match v3 remains the CPU engine. Match v4 is an isolated Core engine, **not Production**. ComputeSharp/DX12 calculates only the representative pairwise kernel; Sinkhorn and per-pixel processing remain CPU. No product GPU path was changed or measured in this phase.

The company machine lacks the home RAW corpus; `CORPUS RERUN = NOT RUN`. Product full pipeline is 1 synthetic PASS and 0 real-camera PASS. Real Canon/Fuji parity and complete metadata/ICC acceptance are not closed by this percentage.

## Later independent company-camera acceptance (same date)

The company obtained five real RAW files outside Git; see [company real-camera closure](real-camera-company/COMPANY_REAL_RAW_ACCEPTANCE_CLOSURE.md). The historical snapshot above remains the state *before* this acceptance. The source baseline for the first run was `ba58558682d5dcfa570017f338ed98ce9b3a9248`: one GFX100S real fixture completed the product chain including parity, three Canon/X-T5 fixtures completed export but not the max-error parity gate, and Sony ILCE-7M4 could not ProfessionalDecode. Home **349-file corpus rerun remains NOT RUN** on the company machine. Total remains **14/28** (newly closed: none); synthetic credits for 24/28 are not double-counted. ICC, comprehensive EXIF, camera-wide decode support, Match v4 product/GPU integration and bounded performance remain open.

### Parity diagnostic clarification

The immutable legacy maxima remain historical BEFORE evidence. A staged diagnostic proved the legacy comparison was not equivalent: WPF/display RGB24 was compared against a nearest-sampled full-resolution RGB48 value. When both sides use the same center-nearest and RGB24 display encoding, maxima for the four decoded fixtures were `.010271` (X-T5 #1), `.008307` (X-T5 #2), `.007007` (GFX100S) and `.011149` (EOS R6), with zero canonical outliers over `.012`. This does not close the product gate by itself: separate ProfessionalDecode calls for the X-T5 RAF are non-deterministic, and the product cross-decode run left X-T5 #1 canonical max `.033891`. The parity root cause is **PROVEN for the legacy metric**, while RAF decode determinism is **OBSERVED / NOT ROOT-CAUSED**. Color Match remains **14/28**; no new gate is credited.

### LibRaw and frozen-master follow-up

Production remains on `Sdcb.LibRaw 0.21.1.7` / native `0.21.1`; no candidate Windows x64 runtime
was available for a safe side-by-side upgrade. The new isolated probe records this explicitly.
The product session now uses a `FrozenRawMaster` with source SHA, decode generation, and
processing generation shared by RAW preview, Match, and TIFF16 export. This improves session
determinism but does not claim that native X-T5 decoding is intrinsically deterministic or that
Sony ARW is supported. Match v4/GPU remains **not ready**.

## Match V4 GPU Phase 2 boundary update

Core no longer references ComputeSharp/DX12. The platform-neutral `IMatchV4ComputeBackend` and
CPU oracle remain in Core; the Windows adapter is isolated in `PixelTart.MatchV4.Dx12`. A
backend-neutral tile executor and VRAM-tier contract are covered by focused tests. This is an
architecture and execution-boundary improvement, not full GPU productization: pairwise cost is
GPU-backed, while Sinkhorn mapping, residual/protection, full pixel application and TIFF16 remain
CPU. Color Studio still uses Match v3 and the gate remains **14/28**.
