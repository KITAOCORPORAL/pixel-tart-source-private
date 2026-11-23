# Isolated legacy high-precision export memory patch

Product patch `cf5a63d38365a0093524d87ed7670e3736b7bb24`, based on actual
`d07e4f4370c8e7d8505a46e3085cc6a069fb4bd4`. Do not merge shared product files
implicitly. Read `docs/validation/legacy-export-memory-20261122/HANDOFF.md` first.
The full product resource gate remains PARTIAL because Sony orientation/reopen
and processed-point-preview stops remain. Pixel parity is not global acceptance.

No camera RAW is decoded. `prepare_inputs.py` only streams the previously
existing uncompressed rendered RGB16 TIFFs into local frozen RGB48 payloads;
original company payload 0806D680 and company fairness panels are unavailable.
The synthetic 40MP fixture and authorized home Sony source are explicitly distinct.
Photos, payloads, TIFF outputs, native libraries and runtime roots are not committed.

Build the product in a fresh output directory with
`ProductFunctionalDiagnosticBuild=true`, `SourceRevisionId=<product patch SHA>`,
and `IncludeSourceRevisionInInformationalVersion=true`. Build Host.csproj with
`ProductAppDirectory` pointing to that output. `prepare_stage.py` copies only PE/CLI
managed assets and excludes native RAW DLLs, then binds their hashes.

Example (use a new stage and run directory each time):

```
dotnet build tools/LegacyExportMemory20261122/Host.csproj -c Release -p:ProductAppDirectory=ABSOLUTE_PRODUCT_OUTPUT -o ABSOLUTE_HOST_OUTPUT
python tools/LegacyExportMemory20261122/prepare_stage.py --product-app ABSOLUTE_PRODUCT_OUTPUT --host-build ABSOLUTE_HOST_OUTPUT --resources src/RAWSelectionAssistant/App.xaml --output NEW_STAGE
python tools/LegacyExportMemory20261122/guard.py NEW_STAGE FROZEN_INPUT_TASK.json NEW_RUN cycle neutral
```

`cycle neutral`: production VM, actual visible PhotoGradePageView plus collapsed
legacy view, 100%, actual TIFF export, save/release, same-process session reopen,
100% again and a second actual TIFF export. `cycle points`: same original-source
100% display, but processing preview is disabled to isolate export; frozen point
recipe still exports. Do not describe it as processed-point 100% preview passing.
`proxy-cycle` isolates the no-full-display boundary. `oracle` uses the original
Core math without WPF for an independent full-size output reference. Its source
orientation is normalized through the same existing product function as the VM.
The marker .raf contains task text only. IRawDecoder is a stored-RGB48 substitute.
No new heavy ledger, cold/rebuild batch or old stopped-batch credit is used.

The guard is unchanged: 2 GiB private bytes AND OS peak working set, 50 ms observer,
600 s watchdog. No forced GC, global cache clearing or budget increase. Run the
experiments serially after checking available memory and target process identity.
Failed output directories and every STOP remain preserved. Weak-reference liveness
and GC allocation counts are recorded, but are not heap-root or leak proofs.

`probe_tiff.py` streams every stored RGB16 channel, orientation and ICC.
`compare_outputs.py` checks original d07 Core output versus both actual exports.
`validate_samples.py` performs direct per-sample comparisons from those output files.
Core tests cover ordered point parity, last partial strip, borrowed/owned lifetimes,
cancel/write failures and existing fallback behavior; WPF tests cover cached source,
bit depth/ICC/directions and shared master ownership. One real-RAW test is skipped.

High precision row export borrows the full immutable source and owns at most small
strip backing arrays. Unsupported spatial/graph/composite stacks keep their original
high-precision math and omit only the unused output display adapter. No blanket
40MP resource guarantee is made for them.

The read-only highlight page contains four pre-existing representative numerical
comparisons and the authorized home source crop. Missing company panels stay marked;
no 180-panel/candidate rerun, Knee/Amount change, V2 development or sharpening V1
reopening. `serve_review.py` binds loopback only; it never opens a browser.
