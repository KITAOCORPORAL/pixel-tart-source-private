# Real and synthetic results are separate

## Real authorized input

- Canon EOS R6,5496x3670: full legacy RAW decode, non-default exposure/WB/old-film rebuild, PV6, instant V2 square output, HQ1600, native256 block, serialized definition, TIFF16/PNG16/JPEG readback, source unchanged hashes. Actual workspace session save/reopen and full Publishing intermediate+resized PNG pixel verification are exercised.
- Sony ILCE-7M4 fixture: current LibRaw rejects opening the file. DECODE_BLOCKED, not a successful pipeline test.
- Fuji X-T5,7752x5184: legacy decode succeeds; conservative full reconstruction reservation exceeds2GiB ledger. BUDGET_REJECTED, no resize or precision fallback.
- Thus three camera/size inputs attempted, only one completes the full chain. Do NOT report two successful camera pipelines. Physical calibration and vendor-wide support remain open.
- Real JPEG input: NOT_RUN; none in the authorized directory. RAW→JPEG output is not equivalent to JPEG-input coverage.

## Actual workspace ViewModels / real large image

Two independent ReferenceColorWorkspaceViewModel instances use the authorized Canon original. Master/HQ and tile requests run in separate owners and queued heavy work. Save/reopen compares exact64x64 full output pixels, Publishing full TIFF then resized PNG reads actual pixels. Ledger pressure, retained reference, budget refusal, switch to rejected Sony/back to Canon, close during export and Dispose are recorded. Final ledger reaches0. No Window or product EXE is opened.

The1.9GiB explicit reservation is an admission test, not an allocation of1.9GiB physical RAM. Process working set/private bytes/peak are recorded separately. Process memory need not return immediately after Dispose; actual multi-desktop-window responsiveness remains manual.

## Synthetic

- EXIF1–8 full float old-export oracle equals PV6 output; non-default exposure1.25EV and grain visibly/numerically change source.
- No double gains/WB/display transform, wrong recipe/version/source mismatch and unsupported engine/reference cases reject.
- Reopen, source version switch, exact TIFF16 and Publishing recipe retain input contract.
- Cancel while decoder awaits and inside large edit loop, timeout/ledger release; no late version commit.
- Existing V1/V2, paper mapping, encoded readback, dice and timeline regressions remain in complete Core/WPF runs.
- Two camera sets of12 offscreen keyframes use a synthetic gradient, not client images.

All real per-input outcomes live in REAL_COLD_PATHS.json; the test-run pass means the probe and asserted supported path completed, NOT that rejected inputs passed.
