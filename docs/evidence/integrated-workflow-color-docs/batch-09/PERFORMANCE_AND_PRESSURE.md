# Measurement scope and limitations

Detailed released-component measurements: cold-stages.json and workspace-pressure.json.

Cold stages use synthetic 1800x1200 source, no preview cache: 100000 geometry preparations 24.083ms; instant image-only math 1065.1594ms; per-pixel geometry reference full render 1085.6132ms; prepared-geometry full render 908.1397ms, exactly identical float pixels; HQ edge800 458.5506ms; TIFF16 encoding of already-rendered pixels 43.6293ms. Layout iteration cost is not one-frame cost. Film-only and full-render metrics overlap and MUST NOT be summed. Comparison is same-input reference versus prepared geometry, not a batch08 binary benchmark. Both are cold paths within one process; JIT/scheduling noise remains and no statistically general speed claim is made.

Pressure uses three real ReferenceColorWorkspaceViewModel instances with a synthetic 1800x1000 decoder, not three opened desktop windows or full-size camera files. It exercises masters/HQ/tiles, active-reference cache eviction, explicit budget refusal, full export, close during export, cancellation, photo switch and Dispose. Each stage records ledger, WorkingSet64, PeakWorkingSet64 and private bytes. A 1900MiB synthetic reservation tests admission without allocating 1900MiB; it must not be described as that much physical memory load. Final ledger is zero. Actual process peak is separately recorded; it can stay high after disposal until GC/native cleanup.

Lifetime findings: retained task/master references survive owner cache eviction because the managed buffer is not disposed. Master bytes are included in heavy work reservations, including export after crop. Close no longer disposes an export CTS while that operation is still using it; operation finally owns disposal. This validates the exercised paths, not every old cache or arbitrary interleaving.

Remaining: authorized large-photo cold benchmarks, isolated no-other-load repeated trials, real desktop multi-workspace/native memory stress and arbitrary cancellation races. 2GiB remains an admission ledger, never an OS memory hard limit.

