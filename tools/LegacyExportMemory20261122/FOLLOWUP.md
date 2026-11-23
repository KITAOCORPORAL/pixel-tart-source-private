# Follow-up to the cf5 zero-RAW export patch

Read `docs/validation/legacy-precision-followup-20261122/HANDOFF.md`.
Product commits apply in order: `cf5a63d3` then `11a424c9`.
Formal product merge remains the product line's decision; the full resource gate
is partial. Existing 11-17 highlighters/180 panels are not rerun. Sharpening V1
stays sealed, no new RAW/cold/rebuild admission or release DLL replacement.

The new upright entry combines the existing RGB48-to-float division and orientation
permutation into one owned frame, without modifying the decoded array. Legacy raw
`DecodeMasterAsync`/`DecodeFrozenMasterAsync` retain their semantics. The display-only
consumer streams the existing point math into RGB24, with direct float*255 rounding;
it never derives display samples via quantized RGB48. Unsupported spatial/graph/mask
math retains the original processing path. Source/master remains borrowed, strip and
returned displays are owned by this operation only.

Build and managed-only staging use the same Host.csproj and prepare_stage.py as the
prior README/history. Bind product SourceRevisionId to `11a424c90199b32cbf73cc6a14eefd651612b213`.
The prior frozen tasks remain under the original isolated worktree's local artifacts.
Never overwrite prior run directories or introduce actual camera RAW.

```
python tools/LegacyExportMemory20261122/guard.py NEW_MANAGED_STAGE EXISTING_FROZEN_TASK NEW_RUN processed-cycle neutral
python tools/LegacyExportMemory20261122/guard.py NEW_MANAGED_STAGE EXISTING_FROZEN_TASK NEW_RUN processed-once points
python tools/LegacyExportMemory20261122/guard.py NEW_MANAGED_STAGE EXISTING_FROZEN_TASK NEW_RUN processed-cycle points
```

`processed-cycle` enables processing and result view, waits for actual detail/render
completion (not just the latest overwritten Task property), requires viewport Zoom=1,
visible/loaded viewport and full-size MatchedImage. It exports, saves/releases, reopens
in the same process, repeats 100% and exports again. `processed-once` explicitly stops
after the first successful actual processed display/export; its result does NOT claim
reopen. The old `cycle points` with disabled processing preview is still distinguishable.

The guard remains 2GiB / private bytes AND OS peak working set / 50ms / 600s.
No forced GC, no global cache clearing. Experiments are serial. Partial/failing Sony,
pending-render assertion failures, and repeated-reopen STOPs all stay in their original
local directories and are included in versioned evidence. The full 40MP processed
repeat-reopen capacity gate remains unsatisfied; never combine separate first/reopen
runs to claim one process passed.

`validate_followup.py` compares all samples, actual TIFF metadata and display SHA to
original Core oracles, checks actual viewport source/result layout, and keeps first-only
and full-cycle authority separate. It uses prior committed/frozen sources only.
No browser is opened. The existing selected read-only highlight page is reused with
missing company panels clearly labelled; raw photographs remain local.
