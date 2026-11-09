# RAW-REOPEN-001 InternalV3 tool

Original diagnostic code only. Isolated worktree/source generation, no production edit, native replacement or application package. Vendor source and generated trees remain local. Research addendum `71ba977ac2d2ca1b039a59fb3a4b2e3f28882a4f` F1/F2/F3 incorporated: P1 never reads uninitialized quality; missing typed state or run identity is rejected; serial-ready and initialized allhex/CIELab are mandatory comparisons before P6. Fixed research: `5a4f233dea5ba11622b18380fea137c8f3944e53`; LibRaw: `cccb97647fcee56801fa68231fa8a38aa8b52ef7`.

**Current delivery is not RAW-ready.** Managed host builds and static/synthetic checks are separate from MSVC native compilation, exact native ABI/lifecycle tests and original task allowance. None of the latter can be inferred from a managed build or static callsite. The old `RawNativeBoundaryProbe` V2.1 DLL hash/ABI checks are untouched.

## Source preparation and checkpoint contract

`Prepare-Internal.mjs <clean-pinned-LibRaw> <this-repo> <NEW-generated-root>` verifies all 27 locked LibRaw research files, inserts uniquely anchored read-only observations into seven local CPPs, and freezes every input SHA. Removing inserted spans must restore **all original bytes**, including control-flow statements. Missing/duplicated anchors fail before generating output. Header is included before dcraw aliases to avoid accidental macro expansion.

`Verify-Contract.mjs <prepared> <source> <repo> <NEW-contract.json>` re-generates and verifies exact text/header, pinned research identities, P2 order, full P5 state, actual dispatcher wrappers, scale/out_cam and tile/join sites. `staticContractComplete` means those designed source points are present; `nativeCompileVerified`, `nativeSelfTestVerified` and `executionReady` stay false until actual validation. It is not proof of safe executable code.

- P1: active CFA plus separate full initialized four-slot image audit. Actual allocation extent, copy_bayer/copy_fuji entry, crop/shrink/margins/pitch/CFA and pointer generation are explicit.
- P2: after black/max, optional green and pre_scalecolors callback, just before no_auto_scale. This corrects the older pre-maximum position.
- P3a/P3b: actual WB branch/white sums, cam/pre/user multipliers and **real scale_mul float bits** before original loop; complete initialized image4 after scale or skip.
- P4: current geometry/CFA/pointer generation after pre_interpolate. Unknown new allocations or transformed layouts invalidate capture rather than guessed reads.
- P5: exact selected interpolator entry, branch/quality/passes and **all initialized image slots across the full rectangle including borders**. Sparse CFA equality is explicitly insufficient. Effective CFA/matrix/black state is typed, without hashing structs/pointers/padding.
- XTrans auxiliary: full four-slot state after serial green-bounds generation, defined allhex, initialized cielab lookup hash and effective matrix columns. Worker/team/tile/scratch-extent records are bounded and worker-owned, emitted after join; no hash/file/log lock inside parallel region.
- P6: RGB after actual return including its border interpolation, plus separate fourth-slot state audit. Fourth slot is not called RGB.
- P7/P8/P9/P10: postprocessing/callback events; effective out_cam valid columns immediately before original loop (explicit bypass); postconvert RGB and initialized histogram; final callback/stretch/restore state.

Supported safe image layout is shrink0/fuji_width0, width=iwidth/height=iheight, inside the allocation initialized by original raw2image_ex. Actual selected branch must be XTrans for P5/P6. Other algorithms/callback-mutated allocations remain unsupported, not automatically generalized. At P5, uninitialized algorithm-private scratch is excluded because it has no defined input value; this is not a proof that every later scratch load is valid. H1 actual load timing is not captured or claimed. Observer adds hashing/I/O/allocations and per-worker bookkeeping; scheduling/heap perturbation remains material.

## Build and no-RAW checks

`Discover-Toolchain.ps1 -Output <NEW-receipt>` looks for existing x64 cl/nmake/dumpbin, VS/SDK registrations and Developer shell. Do not install/change machine settings, repeatedly invoke a failed build, or substitute .NET for native tooling.

`Build-Isolated.ps1 -PreparedRoot ... -BindingRoot ... -Sdk <existing-dotnet.exe> -Contract ... -OutputRoot <NEW-root>` preflights once; missing tooling stops before compiler invocation. A real build uses the pinned upstream Makefile DLL target and `/openmp`, preserving upstream optimization/default options. No RawSpeed/DNGSDK/LCMS/JPEG opt-ins are silently added; this is a **new non-production-equivalent library**, with BCrypt observation dependency. Full compiler log/PE/imports/exports/native hash are required. No source algorithm, init, quality, gamma, WB or thread override is changed.

1. Build managed host only using `RawNativeInternalHost.csproj -p:BindingRoot=...`. `--managed-self-test <NEW-dir>` validates original default assignments without any native load/init/RAW.
2. After an actual native build, `node Freeze.mjs <host-root> <prepared> <contract> <NEW-selftest-manifest> <original-input-identity-json>` freezes new library, binding, host, dependencies and prepared sources. It never issues RAW permission.
3. `<dotnet> RawNativeInternalHost.dll --self-test-no-raw <manifest> <NEW-dir>` validates new exported sizeof/offsetof against unchanged binding, known hash/length/layout tests, exclusive create/close/missing-checkpoint lifecycle guard, actual loaded DLL identity, native init/recycle/close and accessors. No Open/Unpack/Process/MemoryImage. Keep startup environment separate from post-original-binding initialization (null may become6 through existing binding behavior). Do not set/clear OMP/KMP.
4. `Seal-Execution.mjs <selftest-manifest> <native-selftest.json> <budget-config> <NEW-execution-manifest>` requires matching native/preparation identities and verified remaining original allowance. Current original allowance is exhausted, so **do not run/hand-edit a new execution manifest**.

## Task-wide quota and runner

`Budget.mjs --audit <config> <NEW-receipt>` hashes and reads original company process receipts plus explicit mandatory company roots. It separates zero-RAW tests, deduplicates reservations, treats unknown/missing evidence as blocked, and never refunds failed launches. `--reserve` uses an exclusive shared-ledger transaction and durable `.consumed` record before spawn. A lock is temporary; consumed evidence is never deleted. A second machine or zero-run home receipt cannot create more allowance.

`Run-Bounded.ps1` audits quota **before** loading a manifest/native library. It then requires all identity/self-test/environment gates, reserves at most remaining slots, and runs independent processes serially. Max one Open/Unpack/Process/MemoryImage per host; 2GiB/600s external guard, 50ms memory sample. Failed launch or execution consumes its reservation and stops without replacement. Host has a single-use ticket and never accepts the old V2.1 library by weakening its guards. Current company readback binds original V2.1 PIDs15284/4908 as two consumed processes, remaining0. New-binary-local execution0 is also recorded but does not authorize two additional processes.

## Offline comparison and limitations

`Compare-Checkpoints.mjs <first> <second> <NEW-output>` checks completed status, complete six-stage payloads, exact file length/hash and same-stage layout; writes local coordinate/count/amplitude CSVs and earliest different payload/state boundary, including XTRANS-serial-ready between P5/P6. Typed state is compared cumulatively between checkpoints; pointer numbers/event sequence and tile ownership are recorded separately rather than mistaken for pixel input. P5 sparse/border-truncated state is rejected. Never byte-compare different stages/layouts. All equal -> `NEW_BUILD_THIS_PAIR_NOT_REPRODUCED_NOT_FIXED`; no third decode.

`Tests.mjs <pinned-source> <NEW-root>` exercises source preservation, wrong anchors, P2 ordering, P5 full-state non-CFA/border detection, out_cam placement, exhausted/unknown quota, deduplication, persistent failed reservation, missing roots, tampered receipts and comparison failures. These are synthetic/static checks, **not native ABI/self-test or original defect reproduction**.

Private RAW, pixel payloads/CSVs, generated vendor sources and binaries stay local. No production build or application packaging unless product functionality changes and an actual fix is separately validated through original preview/save-reopen/TIFF path. Current root cause remains OPEN / NOT_FIXED.

## F2 phase and JSON member validation (2026-11-09)

CIELab lookup, its three consecutive matrix rows and allhex must occur after the P5 payload and before the serial-ready state/payload, in that order. Duplicate decoded JSON object member names are rejected before replacement, including nested objects in event, result, identity and loaded-native inputs. Existing schema, record uniqueness and identity gates remain active. `F2-Comparison.Tests.mjs <original-research-fixtures> <research-evidence-hashes.json> <NEW-output> [review]` verifies the three byte-frozen research counterexamples; acceptance mode also covers one-sided failures, parser cases and all four F3 controls. Review mode performs only the three original counterexamples and one normal control.
