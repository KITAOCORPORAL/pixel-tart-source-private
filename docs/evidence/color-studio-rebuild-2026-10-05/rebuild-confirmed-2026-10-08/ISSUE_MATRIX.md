# Root cause / repair matrix

VisualApproved=false; UserVerified=false; NOT_READY_FOR_USER_RETEST. Historical failures and pictures in ../computer-use-review and ../company-repair-2026-10-08 remain unmodified.

| Issue | Root cause and production path | Automatic evidence | New Release native result / remaining |
|---|---|---|---|
| CU-01 / B08 | Inherited width/margin and nested rail bounds; responsive dock and minimum editing width in WorkspaceView | StudioToolPanelLayoutTests / WideRatioTests; automated layouts are DIP, not actual system DPI | BLOCKED: native input/capture. Three-size/four-scale matrix not achieved |
| CU-05 / B09 | Numeric binding and shell Esc priority; prior repair StudioNumericEditor/curve transactions retained; graphical editors rollback own gesture first | StudioToolEditingTests / numeric/Esc/transaction regressions | BLOCKED: actual key/blur priority sequence unavailable |
| CU-02 / B05 | Separate floating3D viewport obscured photo and had independent camera; same viewport expands in auxiliary dock | Expanded view identity/non-overlap tests | BLOCKED: continuous native orbit/pan/zoom/pick |
| CU-03 / B10 | Different count and destination enumerations; prior frozen request retained | ColorStudioFilmstripStateTests source-only/hidden/changed-active boundaries | BLOCKED: native dialog boundary sequence |
| CU-04 / B11 | Static/dynamic resource omissions; added range/source labels, global menu, Export key, reactive XMP summary | StudioLocalizationTests resources/live labels/state keys | PARTIAL: legacy composed errors and complete native sweep unclosed; input failure separately BLOCKED |
| CU-06 / B12 | Tone entry buried in internal histogram scrolling | First-screen tone/bounds and pixel inspection tests | BLOCKED: actual hover/leave/Esc and UI output comparison |
| A11 | Decoder affinity caused cross-thread bitmap conversion exception; stale loading revision and oversized thumbnails | ReferenceNavigatorDecodeTests valid/missing/corrupt/re-add/detach/portrait | PARTIAL: automated repaired; natural image native not complete |
| A13 | Minimum25% clamped large source after Fit8%; expected8.96 became25 | zoom-baseline6pass1fail; zoom-fixed7pass; follow/clamp tests | PARTIAL: continuous wheel and pan not measured |
| A14 | Each curve movement superseded delayed preview before any frame; serial proxy pump publishes during gesture | curve-baseline0pass1fail; StudioInteractiveSchedulingTests now passes | PARTIAL: test1200×20 synthetic, not native response benchmark |
| B03 | Drawing artificial floor disguised single-color peak; mixed histogram domains in old matcher | reference-histogram-baseline2 failures retained; corrected tests pass | PARTIAL: mathematical tests, actual photo audit outstanding |
| B14 | Canvas “Copy” called Duplicate; callbackless actions could appear inert | CanvasContextMenuContractTests real commands/undo/capabilities | PARTIAL: menu pointer and full topbar migration review |

## Failed runs retained

TEST_RESULTS.json includes every local TRX hash and failed test names, not only the final green runs. Initial new-category visibility and filmstrip compact-height failures were fixed without deleting assertions. Missing generated DPI fixtures caused17 environment failures; complete existing visual harness generation made91 checks pass. Those fixtures are not the final native binary's OS-DPI acceptance. Locked DLL build failure during overlapping harness work was environment contention, rerun successfully. Existing analyzer warnings remain visible in test builds.

## Native obstacle

New production process was launched through the repository's isolated launcher; actual window and startup log read. Windows.Graphics.Capture returned `FrameArrived timed out: timed out waiting on channel`. Text-only current UI tree worked; click on observed exit-tutorial button returned `coordinate input geometry is unavailable`. The supported secondary action API rejects `Invoke`; no synthetic click is counted. Earlier `GetCursorPos` access-denied and unrelated fallback captures were rejected. One screenshot-only PrintWindow capture succeeds and was inspected. User-driven navigation between observations is not agent-performed acceptance. No authentication/security permissions were bypassed.
