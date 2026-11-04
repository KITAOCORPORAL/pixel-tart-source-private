# NativeBoundaryV2.1 — RAW-REOPEN-001 only

Diagnostic-only, not a decoder replacement or a fix. Run only under explicit authorization for the single company-02 SHA. There is no retry loop. Do not re-run a completed pair to verify documentation.

## Safety and identity

- Uses the exact published `Sdcb.LibRaw.dll` 0.21.1.7/net6.0 and `raw_r.dll` 0.21.1-Release. It refuses other hashes, versions, layouts or inputs.
- `Inspect-NativeAbi.py` records actual PE instructions read-only (Python + installed Capstone). `Inspect-BindingIl.ps1` reads actual managed IL without running its type initializer. These supplement pinned upstream/binding source review; they do not prove compiler flags or bit-reproducible provenance.
- Never marshal `LibRawData.Image` as samples: that binding field is four U16s occupying the x64 pointer slot. Read the **verified pointer slot**, not its address, and only inside the live context. RawData offsets are validated against the exact DLL raw2image instructions and native getter addresses.
- `--self-test-no-raw <new-dir>` checks ABI/getters, readable ranges, length/pitch constraints, crop/padding/unused-channel exclusion and detection of changed valid samples. It calls init/recycle/close once, **zero** Open/Unpack/Process/MemoryImage.
- Three borrowed boundaries: active mosaic after Unpack (one U16 per site at actual margins/pitch); active post-process image RGB (3 valid U16 channels, 4-slot native pixels, omit slot 3); returned MemoryImage RGB16. Owners remain alive throughout capture. See the versioned layout review for exact source and instruction evidence.
- Stream rows to one local file per stage. Incremental full/64-row SHA; only one 46,512-byte compaction row for the 4-slot image, no full managed image copies. FileStream has a 64KiB I/O buffer. No float conversion or App run.

## Environment: compare the same time points

The original binding's `LibRawNative..cctor` uses `OpenMPThreadCount ??= ProcessorCount/2`, whose setter changes the **child process** environment. This is pre-existing product behavior, not an observer override. The observer never sets/clears OMP/KMP variables. Record startup separately from post-binding effective state; old V1.3 runtime receipts captured only the latter (`6`), so their description as necessarily *inherited* was unsupported. Do not compare a pre-initialization null against that post-initialization value.

Freeze a manifest with both `StartupEnvironment` and `Environment` (the original pair's effective state), all executable/dependency/runtime/source hashes and authorized input SHA. `Run-Two-Probes.ps1` compares startup state before spawning; the executable compares effective state immediately after unchanged binding initialization and **before OpenFile**. Any mismatch stops without retries.

## Bounded execution

Build in a new folder with `Build-Probe.ps1`. Run no-RAW self-test, source/static ABI review and `Compare-NativeStages.mjs --self-test <new-dir>`, commit/freeze the observer and manifest first. Only then `Run-Two-Probes.ps1` delegates to the preserved V1.3 serial runner: two independent processes, once per slot, 2GiB/600s external limits, 50ms memory sampling. Existing directories and consumed markers are never removed. Failure stops the pair; never silently start a replacement process.

Offline only: `node Compare-NativeStages.mjs --pair <pair-root> <new-out-dir>`. It verifies each frozen stage's length/hash, compares **same-stage** layouts, and records exact coordinates/counts/amplitudes. CSVs and binary pixels remain local. A completed observer or equal pair is not a product fix. No third decode is authorized.
