# Pixel Tart UI Guardian Report

## Scope

This report covers calibrated UI Guardian evidence and home end-to-end runtime validation against the current committed product source. The Guardian implementation is in the same source revision. The run reuses the existing WPF evidence harness and does not modify the Color Studio 3D product surface.

## Findings

| Severity | Area | Result |
| --- | --- | --- |
| P0 | Static XAML | PASS: no unapproved production P0 pattern found |
| P0 | Runtime geometry | CALIBRATED: 33/33 historical Guardian states captured with 0 remaining P0; scroll-reachable content classified separately |
| P1 | Hit targets | MODELED by UiGeometryGuardianTests; below 36 DIP is reported |
| P1 | Workspace ratio | REVIEW_REQUIRED: current home runtime screenshots are evidence, not a completed rendered ratio audit |
| P2 | Typography/color | Static inventory is recorded; design-system resources are exempted |

## Guardian contract

The runtime model includes TEXT_CLIPPED, TEXT_OVERFLOW, CONTROL_OVERLAP, OUTSIDE_ROOT, HEADER_COLLISION, BUTTON_TOO_SMALL, INPUT_TOO_SMALL, POPUP_CLIPPED, and CANVAS_STARVED. The acceptance run enabled PIXEL_TART_UI_GUARDIAN_HARD_GATE=1. Overlap checks are peer-control checks; parent/child composition, button content, badges and explicit overlay composition are excluded.

## Visual regression

Status: BASELINE_MISSING. No baseline is approved or committed. Baselines may only be written by an explicit human run with PIXEL_TART_APPROVE_VISUAL_BASELINE=1; that variable was not set. Received PNGs and audit evidence exist. VisualApproved = false and ApprovedBaseline = false.

The calibrated historical Guardian matrix was 33/33 captured, 0 failed, 0 not reachable with the hard gate enabled. The home runtime run generated 16 current production screenshots at the documented runtime size; this is current interaction evidence and user review material, not baseline approval.

## Home runtime validation

The process-owned UIA/Win32 harness launched the Release x64 production executable and passed end-to-end acceptance for import dimensions, rating persistence, color persistence, popup switching, outside click/Escape, context menu placement, export/cancel/error path, backfill, smart folder, tag group, custom planning persistence, context-aware toolbar, canvas entry and thumbnail maximum sizing. Camera remains BLOCKED because no tether cable is available.

## 3D current state

ColorSpace3DViewport remains PARTIAL and current-state-only. Existing 3D current evidence is carried into the review pack; this task did not add L slice, a/b plane, photo-to-3D linking, protection, native pointer evidence, performance gates or final visual design.

## Test result

- Release x64 build: PASS (0 warnings, 0 errors).
- Core final TRX: PASS (1527 passed, 0 failed, 4 skipped).
- Modular harness final TRX: PASS (14 passed, 0 failed).
- WPF final4 TRX: PASS (1389 passed, 0 failed, 11 skipped).
- Native contracts final TRX: PASS (19 passed, 0 failed).
- Home runtime smoke: PASS (production EXE, 16 screenshots).
- DPI suite: FAIL (74 passed, 16 failed) because repository-local RC12 evidence manifest and dpi-current directory are absent; this is recorded, not relabelled.
- FlaUI desktop automation: NOT_RUN; the current harness is process-owned UIA/Win32 and no FlaUI package is referenced.
- Native pointer walkthrough: BLOCKED / NOT_RUN.
- Luminance and contrast: REVIEW_REQUIRED for current screenshots; no reliable rendered audit result is claimed.

## Calibration result

- Before: Color Studio P0 = 651 / 611 / 584 (1180 / 1600 / 1920).
- After: Color Studio P0 = 0 / 0 / 0.
- False positives removed: 1,846 scroll-reachable offscreen descendants and valid clipped viewport content.
- Real P0 remaining: 0 in the calibrated Guardian run.
- Fixture correction: LoadTargetAsync was run before AcceptContextAsync, preserving the production SourceImage + MatchedImage path without changing product code.

## Provenance

- Product source SHA: bb88db920422325f94d25ec433c00ed671c36a7c
- Guardian implementation SHA: ec9f7cc50f6eeb369df091f79ea346b543a74bd2
- Evidence generated at: 2026-09-30T15:36:34.4442281Z
- Review pack: artifacts/ui-review/latest/
- Approved baseline: NO
- Visual approved: NO



