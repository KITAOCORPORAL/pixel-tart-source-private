# Stage V.1 Test Summary

- Release build: PASS, 0 warnings, 0 errors.
- Planning Core: 8 passed, 0 failed, 0 skipped.
- Planning WPF: 17 passed, 0 failed, 0 skipped (including the new Stage V.1 UI contracts).
- Existing WPF full suite: 1252 passed, 0 failed, 1 skipped.
- Existing skipped test: `AssetLibraryP3PerformanceDiagnosticsTests.ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture`.
- Skip reason: explicit opt-in diagnostic; it requires a new synthetic fixture and output directory. It is not a Planning/UI failure and remains separated from the standard gate.
- Physical display/DPI, multi-display, real WPF screenshot, and real camera: NOT TESTED here.
