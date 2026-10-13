# Released per-node observations — cold paths, not desktop or speed claims

Source924d48683f9995721335e2dcd1bc89fe7040305d; authorizedCanon5496x3670 frozen decodedRGB16-originfloat. Real source untouched. SPATIAL_REAL_NODES.json contains all declared halos/ledger and10ms sampled working/private/managed peaks, plus full-reference lifetime counters. Same process retains allocator history; these are per-operation sampled maxima, not independent clean-process incremental memory. GC between nodes is measurement-only. Peaks include unreclaimed prior arrays/native pages; do not infer a leak solely from OS retention or call ledger an OS cap.

|Node|Tiled ms|Full ms|Max float error|Tiled working-set max(bytes)|Oracle|
|---|---:|---:|---:|---:|---|
|Reference Match|30960|22375|0|1203421184|Full real comparison|
|Details|22597|NOT_RUN|NOT_RUN|5754093568|Full real oracle budget rejected; synthetic full golden only|
|Structure|6645|3750|0|5967998976|Full real comparison|
|Bloom|12084|9087|0|3896471552|Full real comparison|
|Halation|8621|7528|0|3342303232|Full real comparison|

Tiling is slower in these cases. Reduced bounded scratch/global-stat-list use is not equivalent to totalOS peak convergence. EarlyDetails per-tile arrays produced highLOH churn; later scratch reuse kept exact synthetic output, but final OS maxima still severalGB. No statistically controlled speedup/no-leak claim. Reference old full algorithm materializes largeweightedlists; new two-pass fieldwise traversal retains exact math and reduces live statistics storage; CPU cost can be greater. Each full-reference measured separately from tile peak; fullrealDetails estimate140bytes/sourcepixel exceeds unchanged2GiB and was not executed. No oracle failure is relabelled asPASS.

All5 nodes completed tiled output and recordedbudget;4 have fullrealpixeloracle, all5 syntheticfullreference edges/halos. Results from different runs are preserved locally and NOT used as binarybefore/afterbenchmark. Detailed allocator/native instrumentation, fullrealDetails oracle and actualdesktopmemoryacceptance remain open.

Candidate finalSony/Canon/FujiRGB16 decode succeeds using official0.22.2, with explicitgamma and separate numeric domain. Outputgeometry differs fromhistoricaldecodedsource; not a defaultreplacement orold-pixelcomparison. Olderdecoder remainsrefusal forSony.

Environmental failure: first releasedCore full run had3SQLite failure cases(Cdrive160MB, database/diskfull). No userfiles deleted. Same releaseCore rerun with isolatedD-driveTEMP/TMP passed1660/0/6. FailureTRX retained. FullWPF1542/0/23; ordinaryopt-inreal/visualskip notPASS. ProductEXE notlaunched.
