# Pixel Tart UI Guardian Report

## Scope

This report covers the UI Guardian capture run against product source `5920ed935d7a298cdadef9857eb619ec85dff462`.
It reuses the existing WPF evidence harness and does not modify the Color Studio 3D product surface.

## Findings

| Severity | Area | Result | Evidence |
| --- | --- | --- | --- |
| P0 | Static XAML | PASS: no unapproved production P0 pattern found | `static-xaml-lint.json` |
| P0 | Runtime geometry | HARD GATE available; peer overlap and root bounds are modeled as failures | `geometry-violations.jsonl` when evidence runs |
| P1 | Hit targets | Measured by `FindGeometryViolations`; below 36 DIP is reported | `UiGeometryGuardianTests` |
| P1 | Workspace ratio | NOT RUN: requires a real production window capture | `SCREENSHOT_MATRIX.json` |
| P2 | Typography/color | Static inventory is recorded; design-system resources are exempted | `static-xaml-lint.json` |

## Guardian contract

The runtime model includes `TEXT_CLIPPED`, `TEXT_OVERFLOW`, `CONTROL_OVERLAP`, `OUTSIDE_ROOT`, `HEADER_COLLISION`, `BUTTON_TOO_SMALL`, `INPUT_TOO_SMALL`, `POPUP_CLIPPED`, and `CANVAS_STARVED`. The current harness hard-fails P0 findings when `PIXEL_TART_UI_GUARDIAN_HARD_GATE=1` is enabled by an acceptance run.

Overlap checks are peer-control checks. Parent/child composition, button content, badges and explicit overlay composition are excluded.

## Visual regression

Status: **BASELINE_MISSING**. No baseline is approved or committed. Baselines may only be written by an explicit human run with `PIXEL_TART_APPROVE_VISUAL_BASELINE=1`; the default test never sets that variable. The real run generated Received PNGs and per-state audit JSON; no baseline was written.

The required 33-screen matrix (11 views × 3 sizes) was executed with `PIXEL_TART_UI_GUARDIAN_HARD_GATE=1`: 16 captured, 14 failed on real P0 geometry violations, and 3 were not reachable because the seeded product chain did not expose `SourceImage + MatchedImage` for model generation. Generated PNGs remain in the ignored Received tree. Current-state contact sheets were produced; the 3D model states are absent and cannot be called complete.

## 3D current state

`ColorSpace3DViewport` remains **PARTIAL** and current-state-only. This task did not add L slice, a/b plane, photo↔3D linking, protection, native pointer evidence, performance gates, or final visual design.

## Test result

- `UiDesignLintTests`: PASS
- `UiGeometryGuardianTests`: PASS
- `UiVisualRegressionTests`: 1 PASS, 1 INCONCLUSIVE (`BASELINE_MISSING`)
- Whole-app capture: executed; optional DPI folder handling now records `NOT_RUN` JSON when absent.
- FlaUI desktop automation: NOT RUN; no compatible test-only FlaUI setup is present
- Native pointer walkthrough: NOT RUN
