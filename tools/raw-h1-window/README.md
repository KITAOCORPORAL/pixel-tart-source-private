# Bounded X-Trans architectural watchpoint experiment

Windows x64, fixed LibRaw R2 DLL SHA305eec49...15484 only. These tools are private diagnostic research, not an application feature or pixel fix.

`prepare_plan.py <local-evidence-root> <native-DLL> <original-xtrans.obj>` verifies the pinned optimized code and listing object, emits the six exact PC/operand maps and the marked synthetic test configuration. The source listing must be compiled with the original /O2 /openmp /MD options; all377 .text sections must match. Sources/listings/binaries and full GPR traces stay local.

`debug_watch.py <frozen-config> <NEW-result-directory>` uses Windows debug events and four 2-byte hardware data breakpoints in the spawned process only. It verifies actual instructions and effective addresses, reading executed load destinations or store sources from post-instruction GPRs. It never substitutes a shared-memory reread for a consumer operand. Two restored parent-function breakpoints arm after serial preparation and disarm after OMP join. Unknown instructions remain unknown; records and runtime/memory are bounded. Debug stops materially perturb scheduling; sequence numbers are debugger delivery order.

The seven-event native synthetic test covers main/new-thread read/write, wide extraction and boundary restoration. Negative byte identity and cap tests are separate. H1Host uses exact4c322975 processing assignments, the same native/binding hashes and no R2 sink/full-frame payload. Its no-RAW mode verifies ABI, parameters, native synthetic helper and Initialize/ManagedDispose.

`run_batch.py <frozen-root>` requires GATE.json, all frozen file identities and unchanged old consumed evidence. It exclusively creates batch-01, reserves at most two single-use .consumed files before spawn, runs serially and stops on failure without replacement. A present batch-01 is not resumable/retryable by this command. Do not create another batch to evade the task cap. Runtime invocation is subject to direct user authorization and fresh gates; this completed batch remains preserved.

`analyze.py <completed-root>` only re-reads the existing bounded receipts. It checks register-derived values, matching PC/bytes/effective-address proof, point/phase coverage, identities, old ledgers and the predeclared H1 criteria. No RAW is opened or decoded by the analyzer. Result support concerns the observed local mechanism, not every original pixel difference or a product fix.
