# Runtime correction evidence — NOT_READY_FOR_USER_RETEST

START_HEAD: c6a8c405b71f0161e4fdad17f39def4f70b5e0b1
Production SourceHead: dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7
Branch: integration/pixel-tart-developer-preview

Current candidate: artifacts/releases/runtime-correction-2026-10-04-r9/publish/win-x64/KitaoPhotoSelector.exe
Release x64 / self-contained win-x64. Manifest protects all 287 publish files, not only the apphost.

Read docs/evidence/human-review/RUNTIME_CORRECTION_2026-10-04.md, especially the final r9 section.

## Index

- test-summary.json: full TRX results and hashes; category subsets overlap.
- runtime-evidence-manifest.json: captures grouped by actual SourceHead/EXE; filenames are not pass assertions.
- changed-files.json: 28 production/test files (24 production, 4 tests), status and type.
- commits.txt: 10 implementation/test commits; later documentation commit is separately visible in Git.
- r9-runtime-session.log: final candidate startup, normal exit and restart.
- summarize-tests.ps1 / summarize-runtime.ps1: rebuild these summaries from local evidence.

Screenshots/photos/raw library stay local. r9-after contains the final candidate captures; older after/final-after/r5/r6/r7 folders are diagnostic history. They cannot be combined to claim same-EXE acceptance.

Core 1537/0/4 (unchanged Core source); final WPF 1439/0/11; DPI contract 91/0/0. WPF testhost exited normally. The DPI suite still references older de4c91a visual evidence, so it does not prove current runtime DPI coverage.

All four groups: implementation FIXED_PENDING_RUNTIME, automated PASS_SCOPED, runtime PARTIAL, user NOT_APPROVED.
Missing: complete final-EXE size/DPI matrix, modal rename keyboard/validation/persistence chain, final Studio reference/parameter persistence and complete camera/content/output invariance runtime chain. See report for exact observed and unobserved steps.

VisualApproved=false; UserVerified=false. No installer, observer, recorder, browser or roadmap development. Windows scaling unchanged.