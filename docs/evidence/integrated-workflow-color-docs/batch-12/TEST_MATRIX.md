# Final batch12 test / Release matrix

Base9e0fc52b0fd3598bae36ad1ccb5db2b1520ad0d8.
Tested production source0fec3c60bde763b3ea2df40c1f2cfc9a3f41032f (later delivery commits are evidence/docs only).
SDK10.0.401; Windows x64; process-only D-drive TEMP/TMP. No persistent setting changes.

|gate|pass|fail|skip|result|
|---|---:|---:|---:|---|
|Final full Core Release|1676|0|7|PASS;1683discovered|
|New validation methods loaded against actual published Core DLL|20|0|0|PASS|
|Existing WPF color/legacy RAW focused|5|0|1|PASS for executed subset; company RAW opt-in skipped|
|Official original X-Rite CGATS probe|2files/48patches|0|0|PASS; source/hash/edition gates; unknown whitepoint conversion refused|
|Public original Cube++ RAW probe|1sample|0|0|PASS; native/1:1HQ/region/source hash/domain/black/WB contract|
|Independent base golden|72recipes|0|0|PASS; PV4/PV5/PV6 EXIF1-8 filmoff/V1/V2; not72test methods|
|Release publish|290files|0|0|SUCCEEDED; manifest includes per-file bytes/SHA256|
|WPF full first attempt|132completed|0recorded|0recorded|ABORTED; NOT full pass|
|WPF full bounded retry|70completed|0recorded|0recorded|ABORTED; NOT full pass|
|App EXE/native mouse/physical film+scanner|0|0|0|NOT_RUN|

20new methods comprise12reference/math/domain tests,2public-grid/historical-golden tests,
5paired metadata/QC tests and1published-assembly gate. The released-assembly gate is deliberately
skipped during ordinary fullCore (no released-path env), then passes against the actual Release DLL.
FullCore includes the19ordinary new methods. Counts across repeated runs must not be summed as
unique tests. Released Core DLL identity/location is explicitly asserted; same tests are not labelled
native desktop/device acceptance.

Existing6Core opt-in skips: authorized real color corpus, authorized spatial RAW corpus, film CPU
performance, production resolution benchmark and2GPU parity gates. The7th is Release payload opt-in.
WPF focused skip: authorized company RAW pressure; licensed public RAW verified by its own probe.

## Failure/aborted history retained, not erased

- First new18method focused run:17pass/1fail; path traversal threw InvalidDataException instead of
  returning QC errors. Fixed and rerun18/0, then catalog admission hardened with one additional test.
- Catalog immutable snapshot test initially compared record list identity. Corrected to actual
  provenance/patch-content equality; final published suite20/0. Historical golden assertions unchanged.
- Initial Core full1638pass/37fail/6skip, including SQLite Error13 database/disk full in C: TEMP and
  copy workflows failing capacity admission. D: temp configured only for process; final1676/0/7.
  No unrelated product code or old tests altered to mask this failure.
- WPF full could not finish. Only this task's exact identified testhost was stopped after prolonged
  noncompletion; unrelated processes were not killed. Bounded run sequence points to
  AssetLibraryP1AutomatedEvidenceContractTests.IndependentValidatorRejectsEveryScenarioFaultWithoutChangingInputTree
  as active when interrupted, NOT proven root cause. Both full runs are open/ABORTED.
- Base archive/probe setup initially missed a non-existing targets path, imported build/Version.props,
  then lacked a model namespace. These failed setup/build attempts were not counted as golden proof.
- Research initially hit API/timeouts, legacyPNG0download404 and Polaroid direct403. Selected ZIP
  Range extraction and official browser inspection supplied evidence; no fabricated replacement data.
- A temporary evidence normalization script hit UTF-8/Windows encoding issues; regenerated report and
  all delivered documentation are validated UTF-8. Original reference/data bytes never normalized.

TEST_SUMMARY.json preserves every TRX run hash/count/failure/skip entry. Local artifacts/batch12
contains logs/TRX and excluded research originals. RELEASE_MANIFEST.json seals the290file payload;
original RAW, X-Rite reference values and licensed test PNG are absent from runtime Release.

Historical production renderer/decoder/film/UI files have zero diff from base. The independent golden
archive is baseSHA, not current renderer outputs relabelled as old evidence. No exact i-Type/600 LUT,
A7M4-specific profile or fix to current Sony native refusal is claimed.
