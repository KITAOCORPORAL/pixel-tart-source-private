> 本轮人工截图重新打开相关缺陷，最新状态见 [MANUAL_ACCEPTANCE_DEFECT_CLOSURE_2026-10-03.md](MANUAL_ACCEPTANCE_DEFECT_CLOSURE_2026-10-03.md)。不得从历史CODE/TEST推导Runtime通过。

# Post-video runtime review — reopened

Baseline: `1857cb7608125f7144963365ab45fceaf07fd81e` on `integration/pixel-tart-developer-preview`. Correction Release source: `d497e9b207e98c5a907485dfd85555e33e157a4a` (asset `cfdd0b3`, studio `bf1aa29`, test infrastructure `d497e9b`). Subsequent documentation commits do not change that product source. The previous frozen Round 3 Release does not contain these corrections.

**VisualApproved = false · UserVerified = false · USER_ACCEPTANCE = NOT_APPROVED for every row.**

## Evidence rules

- The prior matrix is historical. CODE/TEST closure is not UX completion; no row here is FINAL PASS.
- The original `Pixel_Tart_2026-09-30_截图问题与Codex修改指令.docx` and the latest recording were not found in searched locations. They have not been read/viewed. The 21 screenshot count is user-provided.
- Current requirements were received through section 24; section 25 contains only its heading. Remaining instructions are pending.
- REAL_RUNTIME failures/partials below are attributed to the user's written recording report, not new assistant observation. They are not upgraded after code edits.
- Unreported/unverified items are NOT_RUN. Automated STA/WPF control tests are distinct from production runtime and user acceptance.
- IMPLEMENTATION uses NOT_AUDITED / REOPENED / PARTIAL / FIXED_PENDING_RUNTIME; AUTOMATED_TEST uses NOT_RUN / PASS_SCOPED / PARTIAL / FAIL. PASS_SCOPED means only assertions exercised by the named run.
- Browser, Computer Use, native input, recorder and automatic DPI changes were not used. No runtime MAX screenshot was captured. WPF tests use isolated test data/control trees; they are not the user's product session.

## HR-001–HR-050

| ID | Issue | IMPLEMENTATION | AUTOMATED_TEST | REAL_RUNTIME | USER_ACCEPTANCE | Current finding / remaining verification |
|---|---|---|---|---|---|---|
| HR-001 | Dimension import | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED | Dimension parser exists; 6000×4000 JPEG 1/6/8, PNG and TIFF import→DB→query→Inspector test passed; recorded examples alone are not full coverage. |
| HR-002 | Old dimension backfill | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED | Backfill test passed for all five files after setting database dimensions NULL. Import had discarded orientation (SQL NULL); import and backfill now persist the parser value. Stored width/height remain 6000×4000 for orientation 6/8; display orientation is separate. |
| HR-003 | Rating click | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-004 | Rating persistence | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-005 | Color/Rating popup separation | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-006 | Popup mutual exclusion | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-007 | Popup overflow | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-008 | Context submenu collision | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-009 | Thumbnail max scale | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Width mathematics is insufficient. Min/middle/max production operation and MAX screenshot have not been captured. |
| HR-010 | Smart Folder click | FIXED_PENDING_RUNTIME | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Smart folders now precede tags and folders; existing query commands retained. Runtime query and unsaved-state behavior still require user replay. |
| HR-011 | Tag Group click | FIXED_PENDING_RUNTIME | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Tag groups moved above folders; selection appearance derives from current query values, not a second saved selection state. |
| HR-012 | Folder text clipping | FIXED_PENDING_RUNTIME | NOT_RUN | FAIL | NOT_APPROVED | Removed folder minimum content width, constrained horizontal scrolling, shared 30-DIP rows and ellipsis/tooltips. Long/deep names at 150% remain unverified. |
| HR-013 | Duplicate dialog size | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-014 | Visual similarity explanation | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Latest instruction ends at section 25 heading. Remaining similarity requirements and original DOCX/video unavailable. |
| HR-015 | Quick Loupe | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Fixed minimum 420×300 gray container and padding removed. Image controls loupe aspect; full viewer/compare commands retained. |
| HR-016 | Inspector Export | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-017 | Palette layout/runtime | PARTIAL | NOT_RUN | PARTIAL | NOT_APPROVED | Existing palette surface was audited; actual palette colors/card readability remain unverified without supplied runtime evidence. |
| HR-018 | RGB + Luma histogram | FIXED_PENDING_RUNTIME | PARTIAL | PARTIAL | NOT_APPROVED | Confirmed HistogramDrawing omitted HistogramLuma. Added measured Luma outline beside RGB channels; no fabricated histogram. |
| HR-019 | Tone/Zone distribution | FIXED_PENDING_RUNTIME | PARTIAL | PARTIAL | NOT_APPROVED | Confirmed Inspector showed only ToneKey/Contrast enum text. Added Black/Shadow/Midtone/Highlight/White bars and measured contrast; full analysis surface includes the same five ratios. |
| HR-020 | Header New context | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-021 | Header Import context | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-022 | Planning custom item | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-023 | Color Studio first-level professional route | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Confirmed absent primary route button. Added ReferenceColor to navigation policy and selected-state button; removed toolbox/pin duplication while retaining legacy lookup. Navigation policy and actual WPF navigation tests pass; visual prominence awaits user review. |
| HR-024 | Reference Navigator usability | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED | Existing 210-DIP navigator raised to 300 DIP with independent expandable reference viewport. Fit/100%/pan reuse existing navigator. |
| HR-025 | Target/reference duplicate labels | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED | Removed duplicate target/reference header pills; title now states 参考仿色 / Color Studio. Reference name/source remain in source list. |
| HR-026 | Match 0% identity | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-027 | Filmstrip | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Confirmed async command discarded clicks while processing. Latest activation now cancels prior load/render; active target explicitly highlighted; selected set stays separate. |
| HR-028 | Sync selected | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Renamed scope-specific actions: 同步到所选 / 同步到全部 / 选择调整节点 / 选择调整类别. Existing parameter engine retained. |
| HR-029 | Batch export | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Existing per-item export/failure tests rerun; actual Publishing workflow remains unverified. |
| HR-030 | Preview/export parity | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED | Old parity fixture emitted TIFF16/high precision PASS without executing those cases. Corrected report to NOT_RUN, removed fabricated tone/color metrics and fixed SourceHead; equal-resolution synthetic JPEG is not corpus parity. |
| HR-031 | Image → cloud | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Added preview-only image inspection mode separate from edit sampling; source OKLab selection reused. Expanded cloud shares highlight state. |
| HR-032 | Cloud → image | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Confirmed overlay truncated to first 12,000 matching pixels. Replaced with full bounded proxy mask; original viewport geometry respected; source-only cloud removes ambiguous matched/reference point index interpretation. |
| HR-033 | Free Canvas toolbar | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-034 | Free Canvas single zoom | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-035 | Free Canvas aspect ratio | NOT_AUDITED | NOT_RUN | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-036 | Professional context menu grouping | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-037 | Context submenu hierarchy | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-038 | Context disabled states | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-039 | Context keyboard hint | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-040 | Multi-select right click | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Not freshly audited end to end; prior CLOSED values intentionally not carried forward. |
| HR-041 | Filmstrip rating/color hydration | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED | Repository hydration/persist tests exist; AssetMetadataSaved callback was unconnected. Wired asset page refresh; full product Grid/Inspector/filter return remains unverified. |
| HR-042 | Copy Adjustments | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Existing selected-category parameter copy tests rerun; no new adjustment engine. |
| HR-043 | Apply Adjustments | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Existing apply tests preserve rating/color/asset fields; UI category action renamed to describe actual apply behavior. |
| HR-044 | Reference Navigator zoom/pan | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED | Read-only navigator retained; expanded view added. Real 100%/pan and DPI usability still require user review. |
| HR-045 | Color Studio layout | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Primary title and expandable reference/3D views added without allocating equal panel widths. Actual WPF control layout tested across 12 logical resolution/scale combinations. This is not Windows DPI/visual approval. |
| HR-046 | Filmstrip selection contract | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Activation concurrency now explicit, latest-click wins; click guard ignores non-left clicks and rating; item keyboard focus and selected binding retained. |
| HR-047 | Batch export partial failure | NOT_AUDITED | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Existing partial export failure/cancel tests exercised; not a user acceptance result. |
| HR-048 | Image → 3D highlight | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Cloud selection halo drawn last so dense points cannot obscure it. Preview-only image inspection does not modify adjustment stack. |
| HR-049 | 3D → image highlight | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED | Mask no longer drops matching pixels after first 12,000. Stale models/highlights invalidated on source/processed-frame changes; Esc/click away clear. |
| HR-050 | Overlay no-layout-shift | PARTIAL | PASS_SCOPED | NOT_RUN | NOT_APPROVED | Filmstrip rating/color height invariance tested across 12 logical resolution/scale combinations. Expanded inspection uses separate windows. Popup and Inspector layout-shift production recording is still pending. |

## Additional recording corrections

| ID | Issue | IMPLEMENTATION | AUTOMATED_TEST | REAL_RUNTIME | USER_ACCEPTANCE |
|---|---|---|---|---|---|
| VR-001 | Gallery title/count duplication | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED |
| VR-002 | Smart folders → Tag groups → Folders order | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED |
| VR-003 | ReferenceColor toolbox duplicate | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED |
| VR-004 | Preview repeated work / delayed switching | FIXED_PENDING_RUNTIME | PASS_SCOPED | FAIL | NOT_APPROVED |
| VR-005 | 3D usable expansion | FIXED_PENDING_RUNTIME | PARTIAL | FAIL | NOT_APPROVED |

## Automated evidence (never REAL_RUNTIME)

- `artifacts/runtime-ux-tests/runtime-ux-focused.trx`: 40 passed, 0 failed.
- `artifacts/runtime-ux-tests/runtime-ux-core.trx`: 71 passed, 0 failed.
- `artifacts/runtime-ux-tests/runtime-ux-regression.trx`: 96 passed, 1 failed; organization row style statically referenced an unavailable application resource in embedded page loading.
- `artifacts/runtime-ux-tests/runtime-ux-regression-2.trx`: after correction, 99 passed, 0 failed. Tests rebuilt in Release, no stale binaries.
- New command-level regression blocks the second render, clicks the first target again, and verifies the latest target/source/result plus cancelled prior work and cache reuse.
- New preview-only inspection test verifies no adjustment stack mutation, nonempty highlight, clear, and stale-model removal on target switch.
- `artifacts/runtime-ux-tests/runtime-ux-dimensions-3.trx`: 1 passed, 0 failed, exercising five actual encoded synthetic files (not real camera corpus). Earlier runs failed cleanup/file retention then exposed missing orientation persistence; both are recorded in their original TRX files.
- `artifacts/runtime-ux-tests/runtime-ux-core-full-2.trx`: 1529 passed, 0 failed, 4 skipped. Earlier full Core run had one source-contract failure for the changed async command signature; default non-reentry behavior has its own regression.
- `artifacts/runtime-ux-tests/runtime-ux-wpf-full.trx`: 1395 passed, 3 failed, 11 skipped; testhost exited normally. Failures were the temporary noncanonical button style, explicit RAW target reactivation, and staged PowerShell file-lock setup. These are preserved as failures in that run.
- `runtime-ux-final-focused.trx`: 40 passed; `runtime-ux-final-behavior.trx`: 5 passed. Includes canonical styles, RAW reactivation, target cancellation, metadata import, masks and logical viewport layout.
- File-lock setup failure also reproduced from the unmodified baseline test body. The `-EncodedCommand` child did not execute its marker statement; executing the identical body as a temporary `.ps1` succeeded. File/process helper launch now uses `-File` and keeps the lock duration, stage deadlines and assertions unchanged; failed launch cleans up its owned child. `runtime-ux-lock-file.trx`: 2 passed, 0 failed, including the full staged exit/cleanup test. The intermediate `runtime-ux-lock-cache-fix.trx` remains 22 passed / 2 failed (later identity probe plus preview busy-state timing).
- `runtime-ux-processing-final.trx`: 60 passed, 0 failed, 1 existing visual-evidence skip. Superseded render completion no longer controls the current frame's processing indicator. Reference selection no longer starts an extra render while synchronizing its adjustment stack. Reference file timestamp invalidation is tested.
- `runtime-ux-dpi-contract.trx`: 91 passed, 0 failed. **Artifact validation only:** its existing capture manifest is from `de4c91a67c9146b5272bf20e10360172c4bf6189`, not these corrections. No new runtime/visual evidence is claimed from that result.
- Final `runtime-ux-wpf-full-final.trx`: **1403 passed, 0 failed, 11 existing skips**, 11m23s, serial testhost exited normally. Includes the delayed retired-render regression, file-change cache regression and staged file/process fixtures. Asset class subset: 385 passed / 2 skips; Studio/Reference workspace/batch subset: 76 passed / 1 skip; 3D viewport: 3 passed; Canvas/board subset: 22 passed; Guardian: 10 passed. Subsets overlap other subject areas and are not separate full-suite totals.
- Final `runtime-ux-core-final.trx`: **1529 passed, 0 failed, 4 existing skips**. Product source is unchanged from the tested correction source.
- Clean Release x64 publish: **PASS**, production flags only; no installer. `artifacts/releases/runtime-ux-correction/release-manifest.json` records SourceHead and SHA256 for all 287 publish files. EXE is an apphost, so application DLL/hash verification is also required. Product launch and production runtime replay: **NOT_RUN**.
- Build logs and TRX files are local ignored artifacts; final counts are recorded here for review.

## Scope of key new regressions

| Behavior | Test |
|---|---|
| JPG orientation 1/6/8, PNG, TIFF → database/query/Inspector; NULL dimensions → backfill | `ImportDimensionsReachRepositoryQueryInspectorAndBackfill` |
| Rapid target selection cancels superseded work and reuses unchanged frame | `FilmstripRapidActivationKeepsLatestClickAndReusesUnchangedPreview` |
| Changed reference file invalidates cached preview | `PreviewCacheInvalidatesWhenReferenceFileChanges` |
| Current ready frame is not held busy by retired rendering work | `CachedCurrentFrameDoesNotStayBusyWhileCancelledRenderUnwinds` |
| Inspection is preview-only, clears, and invalidates on target switch | `ColorInspectionOnlyChangesPreviewAndClearsOnTargetSwitch` |
| Complete mask beyond the former 12,000-pixel limit | `PreviewMaskIncludesMatchesBeyondTwelveThousandAndClears` |
| Active target vs selected set; stable Filmstrip height at logical 100/125/150/200%, 1180×720/1600×920/1920×1080 | `FilmstripActiveAndSelectionRemainDistinctAcrossLogicalViewportSizes` |

The new regressions supplement existing checks. They do not certify every interaction listed in an HR row. Source/string contract tests prove wiring only; no user approval is inferred.

## Real blockers / unfinished verification

- Full DOCX, latest recording, and continuation after section 25 needed to audit the complete original handoff.
- Production runtime replays, Windows 150% review, Min/Mid/Max thumbnail operation and MAX screenshot are NOT_RUN. No test screenshot is substituted.
- Long folder names/deep nesting, filmstrip keyboard/mouse feel, reference navigator usability, 3D overlay readability, visual analysis palette and Free Canvas require actual product review.
- JPEG/TIFF/high precision parity claims require separate measured corpus evidence; historical constant-image fixture does not establish that.
- No new installer or approval. New correction Release is available in `artifacts/releases/runtime-ux-correction/publish/win-x64/`; it has not been launched or visually accepted. Physical Tether remains WAITING_FOR_HARDWARE. GPU/Variant/Film Lab/Polaroid/mobile/browser roadmap remains deferred.
