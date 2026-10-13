# Final verification matrix

Product source: 924d48683f9995721335e2dcd1bc89fe7040305d.

|Run|Pass|Fail|Skip|Meaning|
|---|---:|---:|---:|---|
|Complete Core|1660|0|6|54s; all previous golden tests retained|
|Complete WPF|1542|0|23|9m30s; real/visual opt-ins separate|
|Release Core, first attempt|1657|3|6|SQLite disk-full environmental failures; retained, not PASS|
|Release Core, isolated D-drive temp|1660|0|6|59s; same released DLL|
|Release reviewed candidate and real node probes|2|0|0|2m37s; per-node reference limits separate|
|Spatial/Reference focused|79|0|4|Both reference versions and historical tests|
|Synthetic spatial golden|3|0|0|Full-frame vs tiles at boundaries, cancellation, budget rejection|

Tests cover both film spatial versions, non-default Details denoising/sharpen/clarity/structure, legacy Develop structure/detail, Reference Match V1/V2 global statistics, odd tile sizes31x37/67x64, global edge handling, explicit contract identity, budget refusal and cancellation release. Real Canon four nodes compare full-frame output exactly; real full Details reference is not attempted above2GiB estimate. Its output completion is not an independent real pixel-oracle PASS.

Candidate checks official pinned binary hashes, true16-bit output, authorized Sony/Canon/Fuji hashes before/after, bad candidate hash and cancellation. Old decoder/refusal path unchanged; new candidate numeric domain remains distinct.

Complete WPF source tests include existing headless/isolated windows used by old tests; no product EXE launch or Computer Use. They do not constitute physical100–200%DPI, end-user startup or visual approval. Windows manual gate remains NOT_RUN. GUI-layout code untouched.

Failure evidence: first candidate run used defaultdcrawgamma and then explicitgamma changed only candidate versioned configuration; authoritative final output is packaged-real. Earlier node profiling exposed large allocations and slow bounded Reference multiple-pass implementation, which were changed to scratch reuse and two-pass summation with exact golden tests. Released Core disk-full tests repeated after task-only TEMP/TMP redirected to D without deleting user folders. Full TRX hashes/outcomes retained in TEST_SUMMARY; no failed run discarded.
