# Whole-application follow-up — 2026-10-22

Frozen product/test baseline: 13ec9ca1c678622e5c7b884d8f69882c409c33a4. Independent worktree, no merge or cherry-pick.

These are reproducibility records for the current machine; paths are explicit and outputs must be new. Do not replay against old directories. Build and publish the full diagnostic shell first, then bind its DLLs to test hosts. Core release-location contract expects the bound loaded DLL path, not the original publish directory. Original first-run failure and driver startup failure are retained separately.

- build_current.py: full Release, WPF x64, discovery helper, Windows x64 self-contained publish.
- run_current_full.py: full Core, four WPF fixture-process shards, published modules; authorized two-photo manifests only, new runtime/output directories.
- rerun_core_corrected.py: original current Core full rerun with only release-location precondition corrected; preserve first run.
- run_current_logical_dpi.py: separate UiReview build, 106 offscreen logical states, independent validator and DPI contracts. NOT desktop/physical DPI.
- run_current_extras.py: independent DOCX prototype and research tests, not shipped-feature acceptance.
- audit_current.py: all remote branch merge-base/diffs, old ZIP identity and read-only dirty-file evidence.
- verify_build_current.py: complete file hashes, declared runtime assets, executable x64 machine.
- aggregate_current.py: exact adapter identity coverage, raw TRX archive, current module/action/planning ledgers. Refuses unfinished, missing, extra, timed-out results.

All related-module test mappings are coverage indexes, never per-action acceptance. No screenshots or generated examples from a test fixture establish physical mouse/keyboard, camera, WPS, multi-client, installer or household performance acceptance. Status vocabulary remains PASS/FAIL/PARTIAL/NOT_RUN/BLOCKED. Never relax product pixel contracts or thresholds to make this batch pass.

- verify_preservation.py: read back all prior worktree heads/statuses, 423 pre-existing dirty-file hashes, old diagnostic ZIP.
- branch_blob_equivalence.py: compare current blobs for non-ancestor branches; absent files and differences remain separate.
- run_planning_samples_native.py: supplementary current synthetic examples with canonical evidence paths; never counted again in full suite totals.
- verify_ledger.py: unique IDs, status vocabulary, adapter coverage and archived raw hashes.
