# Workflow correctness audit

Generated against the clean rc7 source of truth. This audit separates code contracts from UI presence and preserves historical acceptance documents.

## Implemented

- Publishing uses the existing TaskEngine and now exposes filtered task snapshots through `IPublishingTaskCoordinator`.
- Publishing VM subscribes only while its active task is running, filters by TaskId, applies updates through the captured UI synchronization context, and unsubscribes on terminal completion.
- Publishing cancellation and renderer cancellation remain token-based; temporary output is cleaned atomically.
- Compare has a canonical Core `CompareViewport`, explicit `CompareSide`, independent primary/secondary viewport values in the model, and a physical-pixel scale helper.
- Rapid Compare keyboard actions are only handled when Rapid Compare is active and the corresponding command can execute.
- Capability state is generated from an explicit mapping in `scripts/generate-current-capabilities.ps1`.

## Partial

- Native Tether WPF currently binds primary/secondary transforms through the existing VM properties. Full normalized-coordinate mouse routing and separate visual hit targets still need integration.
- Compare swap currently swaps rendered images and candidate/selected identities through the existing commands, but a complete persisted annotation/action-target walkthrough is not yet closed.
- TIFF16, ICC, RAW, Match V4/GPU, and 3D color space have meaningful source/tests but remain partial at product-acceptance level.

## Stale documentation

Historical handoff, roadmap, acceptance, and closure files describe earlier baselines. They remain immutable evidence. The generated files under `docs/current/` are the current capability source of truth.

## Not implemented in this phase

- New TaskEngine, Publishing engine, or Compare architecture.
- Full native 2-Up walkthrough automation at 100%/200% physical pixels.
- Spektrafilm integration, Polaroid engine, new LUT/preset architecture, new 3D renderer, macOS/iPad/cloud/AI culling/browser extension/installer work.

## Evidence boundary

Build success and targeted Core/WPF tests do not imply full native visual acceptance. Any item without a reproducible source/test/evidence path remains PARTIAL, BLOCKED, DEFERRED, or SPEC_ONLY in the generated manifest.
