# RAW-REOPEN-001 isolated stage observer

Version: **RAW-REOPEN-001-StageProbeV1.3**. This is an opt-in diagnostic executable, not the product and not a fix. Only the explicitly authorized company-02 SHA is accepted. Do not run a fresh RAW pair merely to verify this delivery: the authorized pair has already completed and reproduced the failure.

## Delivery verification (no RAW decoding)

1. Run `node Verify-Generated.mjs` from this directory. It checks the exact frozen product source, every unique observation anchor and the generated source hash. No algorithm or production parameter statement is replaced.
2. Read `docs/validation/company-r55-raw-reopen001-20261104/TEST_SUMMARY.json` and its receipt manifest. Success of the observer is separate from the **FAIL** of pixel determinism.
3. `Compare-Stages.mjs <pair-directory> <new-output.json>` streams the existing full local binary stage files, verifies their hashes and compares pixels. It never invokes a decoder. The valid layout is the pinned company-02 7752x5184 RGB sample.
4. `Compare-Parameters.mjs <pair-directory> <new-output.json>` compares existing parameter snapshots; pointer addresses remain explicit differences.

## Building the observer only

`Build-Probe.ps1 -Sdk <existing-dotnet.exe> -FrozenPublishedRoot <verified-published-folder> -OutputDirectory <new-directory>` references existing published managed DLLs, copies the same raw_r/lcms2/jpeg8/zlib1 dependencies, and refuses an existing output directory. It does not build the product or alter machine settings. Missing APPDATA/DOTNET_ROOT are supplied in the command process only. No OMP setting is changed.

Before any newly authorized raw execution: run `<dotnet> RawReopenStageProbe.dll --self-test-no-raw <new-directory>`; freeze executable, DLL, runtime/native dependency hashes, explicit input SHA and environment; then provide a manifest with the same schema as the delivered one. Do not silently regenerate a manifest against changed binaries.

`Run-Two-Probes.ps1` runs first and second sequentially, once per slot, with create-new consumed markers, complete separate result folders, 50ms memory observation, existing 2GiB limit and no automatic retry after failure. Existing folders/markers are never deleted. The runtime environment may already contain OMP_NUM_THREADS; record it, do not set/clear it. The probe verifies the actually loaded native path/hash before OpenFile.

Stage binaries are full native RGB16, managed RGB16, float32 master and upright master, all local-only. Hashing/writing does not retain a product cache. The metadata observer reads the full exposed parameter fields; native XmpData span is not reflectively invoked and its content is not claimed captured. No vendor source is embedded or copied; the generated decoder is our frozen product source plus observation hooks.

Original failed setup runs, successful pair, exact comparison, and all input/output identities are preserved under the E: evidence root in the final report. No new RAW invocation belongs in normal CI.
