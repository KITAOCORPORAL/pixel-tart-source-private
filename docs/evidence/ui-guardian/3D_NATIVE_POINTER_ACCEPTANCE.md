# 3D native pointer acceptance — BLOCKED

Source: `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`.
Implementation = PARTIAL. Verification = NOT_RUN.
Evidence ledger: `3D_NATIVE_POINTER_ACCEPTANCE.json`.

Real model capture from the previous run is preserved. This run did not open/generate the model through native input. Orbit, pan, zoom in/out, reset, fit, resize, inspector collapse/expand, rapid gestures and Escape/focus recovery are NOT RUN.

Read-only instrumentation now exposes actual camera, model sample count, projected bounds, viewport dimensions/DPI, capture state, render count and last CPU OnRender duration. It cannot manipulate the model or camera. These fields have automated contracts, not native acceptance.

Source-audit gap: `ColorSpaceRendererState.Fit()` currently resets the default camera and sets IsFit; it does not compute real model bounds. This is distinct from the tool blocker and must be fixed/validated before the required bounds-aware Fit gate can pass. Projection aspect behavior remains unverified. No speculative camera rewrite was made while the native validation surface was unavailable.

Native blocker and bounded recovery: see `NATIVE_CLOSURE_MACHINE_EVIDENCE.json`.
BaselineApproved=false; VisualApproved=false; UserVerified=false.
