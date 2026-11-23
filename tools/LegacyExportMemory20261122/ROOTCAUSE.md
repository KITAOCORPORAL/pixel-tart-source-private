# Reopen root-cause closure

Product increment: ac1c57e9, on top of cf5a63d3 and 11a424c9.
Read docs/validation/precision-reopen-rootcause-20261122/HANDOFF.md.

Diagnostic captures are separate from acceptance. `processed-diagnostic` stops at
DIAGNOSTIC_READY immediately before second ushort allocation. The local dump and
SOS logs are sensitive local-only data. Collection pauses/perturbs execution; even
though GC counters stayed equal, that run cannot be a resource PASS.

The old float array and disposed VM have zero roots at that boundary. SOS live/dead
and LOH/free-block statistics distinguish uncollected garbage from retained objects.
WIC memory-pressure values are estimates, not exact native committed memory. Native
private residual includes runtime/other heaps; never call all of it a WIC leak.

The product change is only a per-buffer representation: legacy upright decode owns
a copied/normalized RGB48 source, converts consumed strips with original /65535f,
and preserves lazy legacy Rgb32 access. Neither shared decoder arrays nor masters
are disposed. Point display/export/proxy reads avoid whole float expansion. Spatial,
graph and other legacy full-float consumers retain their API/math and may expand;
those paths do not receive a blanket memory-capacity guarantee.

The host's descriptor and logical-float digest now read through non-materializing
APIs. That is an observer correction for the new representation, not a claimed cause
of the old failure. `Release` waits known owned tasks (10s fail deadline) and dispatcher
idle; it no longer uses the old fixed 1s close delay. Original 700ms UI settling remains;
it is not extended or conditioned on GC/private-memory decline.

Acceptance has no diagnostic pause/dump/profiler, no forced GC/LOH compaction or
working-set trimming, no global-cache clearing and no altered GC settings. Guard:
2GiB private bytes AND OS peak working set, 50ms, 600s. Run serially.

```
python tools/LegacyExportMemory20261122/guard.py NEW_STAGE FROZEN_TASK NEW_RUN processed-three-cycle points
python tools/LegacyExportMemory20261122/guard.py NEW_STAGE FROZEN_TASK NEW_RUN processed-cycle neutral
```

`processed-three-cycle` verifies first processed 100%, save/export, release/reopen and
second processed 100%/export, then one more release/reopen/processed 100%/third export.
All source backings and actual viewport layout are recorded. `validate_rootcause.py`
checks every output sample, 16-bit/direction/ICC, display SHA against original Core,
three output identities, per-cycle peaks and actual storage/no full materialization.

No new camera RAW, cold/rebuild batch, X-Trans wiring, native decoder/release DLL,
old ledger or stopped balance is touched. Existing highlight results/page and sharpening
V1 seal remain unchanged. Ordinary product EXE acceptance belongs to the product line.
