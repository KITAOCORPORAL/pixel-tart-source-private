# R5.4 graph large-source benchmark
Build Benchmark/Benchmark.csproj with the workspace .NET10 SDK. Pass an isolated output root and one explicitly authorized RAW path:

`dotnet Benchmark.dll <root> prepare <authorized-raw-path>`

Then run separate processes for `<root> phase serial|branches|mixes preview|export|production-preview|rowband|tiff16|repeat-cancel|save-large|reopen-large`.

`preview` is an explicitly labelled1600 proxy node microbenchmark; `production-preview` uses the existing full-source area-preview pipeline and is the real preview timing. `export` measures full float graph result; `tiff16` writes and validates actual full TIFF16. Never label microbenchmarks as full app timings. The original baseline73ac/06e33 DLL must be separately retained to reproduce before cases; do not overwrite installed/shared output. Source pixels/TIFF stay local, commit hashes/records only. peakWorkingSet is5ms sampled process memory; ledger records retained RGB reservations, not all CLR/native memory. No chart purchase or household drive access is required.
