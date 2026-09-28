# High-resolution WPF batch test diagnosis

Test: `ColorStudioThirtyHighResolutionTargetsRecordProcessedExportBaseline` (30 copies of a 2400×1600 PNG). A bounded run previously aborted after 22 of 30 JPEG outputs. The current isolated rerun completed **PASS in about one minute**, producing all 30 outputs; it did not require a larger timeout. The earlier abort therefore appears intermittent/resource-sensitive rather than a proven deterministic deadlock.

Observed during the earlier bounded run: the test sequence marked the large test incomplete, the external temp directory contained 22 outputs, and the host was terminated while processing the next target. No product task or cancellation state was captured at the termination boundary, so thread-pool starvation, transient memory pressure and test-host termination cannot be distinguished from the evidence.

Classification: **INTERMITTENT / NOT PROVEN DEADLOCK**. The isolated rerun is a useful result, but this is not a performance closure. Keep it as a separate regression risk; do not let it change the real RAW parity conclusion.
