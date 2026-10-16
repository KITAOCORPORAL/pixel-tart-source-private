# Home Fuji repeatability: production default is blocked

Same exact authorized DSCF0347.RAF SHA52d054...; native raw_r.dll SHA F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9 / LibRaw0.21.1-Release; legacy full product pipeline7752x5184 /16bit/sRGB. Two full TIFF exports differ at38,375 channel samples, maximum11,713/65535, mean absolute0.138235 codes. Source/hash/geometry/profile unchanged. Independent existing full render equals decoded master in each run, so difference precedes downstream neutral editing/export.

Two additional default14-thread fresh processes yield different decoded float hashes, proving it is not only TIFF timestamp or file metadata. Two additional *audit subprocesses only* with OMP_NUM_THREADS=1 yield identical68E617... decoded/master hashes. Caller environment restored immediately afterward, no persistent OS/user/product change. This narrows suspicion to native X-Trans parallelism but is not complete race diagnosis/source correspondence proof.

Do not change default decoder/threads/interpolation, hide the mismatch or rebaseline old output to1thread: would change historical pixel result and requires open-source-first comparison/version gates. First/second default TIFFs and exact diagnostic logs stay ignored artifacts/home-photo, reference metadataJSON is safe to commit; no RAW or output images committed.

User-required acceptance gate: **BLOCKED** until default-path repeatability is explained/fixed under an explicit compatible version or previous-output preservation contract. FullCore/WPF and35/other passing tests cannot override this default product inconsistency. Single-thread research result is not a product fix, Sony support or camera calibration.
