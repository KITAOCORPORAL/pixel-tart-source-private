# Bounds-aware 3D Fit validation

Date: 2026-09-30. Product change: `6aa3f1bcb396aa6bf66c5885f060e87fc0c236d6`.
Automated verification: PASS. Production native pointer verification: NOT_RUN.

## Previous and current contracts

Previously `ColorSpaceRendererState.Fit()` reset the camera to its default and
set IsFit; it did not examine points, orientation or viewport bounds. Projection
also scaled horizontal coordinates by width and vertical coordinates by height,
stretching the cloud in a non-square viewport.

The model contains **OKLab**, not CIELAB or pre-normalized display coordinates.
The existing `ColorSpaceCoordinate.FromLab` contract remains unchanged:

- X = clamp(a / .4, -1, 1)
- Y = clamp((L - .5) * 2, -1, 1)
- Z = clamp(b / .4, -1, 1)

Fit and rendering use the same yaw/pitch rotation after this mapping. Bounds are
computed from finite points in the **visible** clouds, not from hard-coded gamut
limits. Renderer mode, drawing, observation and Fit use the same visible-cloud
selection. Switching mode invalidates the previous fit until fit is requested.

## Projection and proof

For viewport width W and height H, FitPadding p = .10 (one Core constant):

```
k = min(W,H) * .42 * 1.8 / distance
screenX = W/2 + (rotatedX + panX) * k
screenY = H/2 - (rotatedY + panY) * k
panX = -(minX + maxX)/2
panY = -(minY + maxY)/2
requiredDistance = 1.8 * .42 / (1 - 2*p)
                 * max(spanX * min(W,H)/W, spanY * min(W,H)/H)
```

The same scale k on both axes preserves aspect ratio. The chosen distance gives
`spanX*k <= W*(1-2*p)` and `spanY*k <= H*(1-2*p)`. Centering therefore places every
finite point inside `[p*W,(1-p)*W] x [p*H,(1-p)*H]`.

The normalized cube's maximum rotated span is `2*sqrt(3)`; requiredDistance is at
most 3.274, below the camera upper limit 10. The minimum distance .5 only increases
padding. A single point is centered at default distance; an empty model is safe.
Invalid viewports do not newly claim IsFit. Invalid point values are excluded
without renumbering valid point identities. Projection tests use **1e-8 DIP**
tolerance; no photographic fidelity thresholds were changed.

Reset restores the canonical yaw/pitch/distance and zero pan, with IsFit=false.
Fit preserves finite orientation and changes centering/distance, with IsFit=true.
Resize re-evaluates bounds only while IsFit is true. A manually positioned/reset
camera is not silently refitted. Pan converts actual DIP deltas through k and
reverses screen Y, so a (37,19) DIP drag translates projected points by (37,19).
Zoom uses the actual .5..10 projection distance limits instead of a hidden .1..0.5
dead range. Mouse capture loss and Escape clear the viewport's active pointer.
The latter lifecycle behavior is implemented but **not native-verified** here.

## Automated evidence

`ColorSpaceBoundsFitTests`: 22 tests, including all requested named Fit cases,
mode selection, exact padding, degenerate/invalid input, aspect preservation,
pan, bounded repeated zoom, and a 216-case rotated cube/view-size matrix.

The matrix crosses six viewports, six yaw values and six pitch values, including
1180x720, 1600x920, 1920x1080, portrait and very wide layouts. It compares actual
`ColorSpaceProjection.Project` coordinates, not only camera parameters.

`ColorSpace3DViewportTests`: 2 PASS, including WPF layout 900x300 -> 300x900,
orientation preservation, fit recomputation, and Reset remaining unchanged after
another resize. This is a component test, NOT a native Production EXE walkthrough.

Related ColorSpace Core total: 36 PASS / 0 FAIL / 0 SKIPPED.
Targeted WPF total: 55 PASS / 0 FAIL / 0 SKIPPED, including Guardian 10,
read-only NativeClosureObservation 4 and CapabilityConsistency 1.
Raw TRX paths/digests: `NATIVE_HARNESS_MACHINE_EVIDENCE.json`.

## Remaining gate

Production pointer Orbit/Pan/Zoom/Reset/Fit/Resize, capture validation, actual
hardware DPI, input-to-render latency and current native Guardian counts remain
NOT_RUN / NOT_MEASURED. Do not infer them from these automated tests.
