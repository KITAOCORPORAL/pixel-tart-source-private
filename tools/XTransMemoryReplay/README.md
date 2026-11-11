# Zero-RAW post-import memory replay

This tooling is isolated from the stopped desktop-02 batch. Use only a copy of its frozen RGB48 cache (7752 x 5184, little-endian RGB16, SHA-256 0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E). The injected decoder reads this copy, never a camera RAW. A tiny owned .raf text fixture selects the existing post-import route; it is not a real RAW sample.

Build XTransMemoryReplay.csproj with the workspace .NET SDK. It references the existing WPF product and uses a matching self-contained win-x64 host. No candidate native library is rebuilt or loaded by the replay.

Modes used in the evidence:

- default: viewmodel post-processing, no WPF window.
- window: explicit full-detail request with a real WPF viewport; diagnostic GC checkpoints are recorded separately.
- shell-auto-natural: collapsed legacy workspace plus visible photo page, no explicit detail request and no forced GC before measured idle/peak.
- shell-auto-natural-soak: same route, then 210 seconds minimized and a restore. A separate 360-second watchdog bounds this case.
- shell-main-auto-natural: the actual MainWindow class with an alternate inert shell context. This is a limited supplemental control, not full app acceptance.

Run run_guarded.py APP_EXE CACHE_COPY NEW_OUTPUT MODE [--trace]. Every run has a unique output and sibling guard directory. The external guard samples target PrivateBytes and OS peak WorkingSet every 50 ms and stops above 2 GiB. Parent/worker/cache plans and original STOP/ledgers are never changed. A no-RAW negative diagnostic stop does not grant permission to resume a stopped real-RAW batch.

The host records phase counters, GC observations, VirtualQuery commit categories, GDI/USER counts, known image holders, synchronous detail-request stacks, and whether raw_r.dll appears in its module list. Natural and explicitly forced-GC snapshots are distinct; no forced GC was added to production. Pixel verification reads each bitmap row into a small buffer and hashes the full RGB24 frame without another full-frame allocation.

For --trace, install dotnet-trace 10.0.750501 into the artifact root's profilers directory (not a user/system tool path). The gc-verbose profile samples managed allocations. XTransMemoryTrace uses TraceEvent 3.1.23 to resolve large allocation stacks. These tools are diagnostic-only dependencies.

analyze_memory.py ARTIFACT_ROOT ORIGINAL_ROOT NEW_OUTPUT verifies paired hosts, the expected view behavior, full-frame display hashes, selected TRX results, and the original 22-file failure binding. plot_memory.py produces a standalone numeric resource figure; Matplotlib is only a local analysis dependency. Raw RGB buffers, trace binaries, application packages and photographs remain local.

check_pending_worker.py verifies that an unauthorized Version2 successor worker rejects before even reading input/reservation fields. prepare_successor.py binds a prebuilt diagnostic app/worker into a pending, non-executable successor draft. The product's normal decoder and the frozen candidate native hashes remain unchanged.

See docs/research/xtrans-post-import-memory-20261111/REPORT.md for source commits, controlled cases, unresolved native/graphics attribution, and the authorized scope of each result. None of these replays replace actual darkroom save/reopen or TIFF acceptance.
