# TIFF viewport memory replay

Production patches: `edd6d25e` and `e28074f1`. The product and test files at
`bdc913c9` are identical to unified source `939d540c` before these patches.

Build the existing WPF host (Windows, .NET 10), binding SourceRevisionId to the
product patch commit. Keep `ProductFunctionalDiagnosticBuild=true`.

```
dotnet build tools/ProductMemoryIntegrationReplay/ProductMemoryIntegrationReplay.csproj -c Release -p:ProductFunctionalDiagnosticBuild=true -p:SourceRevisionId=e28074f18ea6d76d7715451bce89d6c80c3cc2fd -o artifacts/tiff-memory-20261115/replay-bin
python tools/TiffViewportMemory/prepare_input.py --profile-from PATH_TO_AUTHORIZED_SRGB_TIFF
python tools/TiffViewportMemory/run_case.py --app artifacts/tiff-memory-20261115/replay-bin/ProductMemoryIntegrationReplay.exe --input-task artifacts/tiff-memory-20261115/INPUT_TASK.json --name UNIQUE_NEW_RUN
```

The stress fixture is row-generated RGB16, 7752x5184, with the existing sRGB ICC
SHA 2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e.
Python fixture generation uses numpy and Pillow. No new RAW decode is allowed.
The home Sony TIFF is separately bound to the previously authorized manifest;
photos, TIFF outputs, runtimes and profiler binaries stay in local artifacts.

`run_case.py` starts two independent product-view hosts, saves/closes/reopens the
session, exports twice, then probes input and actual outputs in separate guarded
processes. It checks decoded samples including alpha, normalized source RGB,
16-bit channels, orientation, ICC, visible layout, zoom, unchanged input and RAW
request count. All runs retain the original 2 GiB / 50 ms process guard. Existing
output directories are never overwritten. Phase attribution uses the host's
phase.txt; this adds no production memory or scheduling policy.

Authority: actual WPF PhotoGradePageView and production commands with fixed
dialogs and an offscreen visible window; it is not manual file-picker/display
acceptance. Preview camera/film research, pixel formulas and ICC policy are
unchanged. No forced GC is added to the measured TIFF runs.

Optional allocation capture uses the existing guard's `--trace --trace-seconds 7`.
A short completed trace avoids losing the EventPipe trailer when the guard kills
a baseline process. TraceReader was restored from 6e57eb26 and uses TraceEvent
4.0.0; its sampled GCAllocationTick weights are not a full allocation census.
Traces without a complete trailer remain preserved but are not cited as parsed
allocation evidence. No CPU/GPU/native heap attribution is inferred from them.
