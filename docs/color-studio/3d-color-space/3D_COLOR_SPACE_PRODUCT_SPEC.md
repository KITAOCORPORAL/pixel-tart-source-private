# 3D Color Space / Color Map product specification

Status: `REQUIRED` · `DEFERRED_TO_COLOR_STUDIO_PRO_PHASE`.

This feature is not cancelled, optional, or removed. It is a future Color Studio Professional
surface built on the same platform-neutral Core color science as Match V4.

## Canonical data model

- `ColorPointCloud`: sampled OKLab D65 source, reference, and matched points.
- `ColorMigrationVector`: source point, matched point, delta L/a/b, weight, protection state.
- `ColorSelection`: eyedropper/range selection identity and source pixel mapping.
- `ColorSlice`: L-axis slice or a/b plane with sampling density and selection filter.

The model uses the existing OKLab conversion and the immutable `MatchTransformV4`; a renderer may
use Windows DX12 or future macOS/iPadOS Metal, but a renderer never changes color coordinates.

## Product interactions

1. Photo eyedropper locates and emphasizes the corresponding point/cluster.
2. Clicking a point/cluster highlights the corresponding photo region.
3. Color Range filters the cloud and migration vectors to the selected colors.
4. Match Strength 0–100% moves matched points and vectors continuously using the same transform.
5. SOURCE, REFERENCE, MATCHED, and overlay modes can be toggled independently.

Required views include L/a/b axes, rotate/pan/zoom, L slice, a/b plane, source/reference/matched
clouds, migration vectors, protection visualization, and a shared cross-platform data contract.
Sampling tiers (Preview/Standard/Dense) may change display density only; analysis and matching
remain independent of renderer sampling.

## Acceptance gates

The future phase must prove coordinate parity against Core, selection-to-photo mapping, live
strength updates, range filtering, CPU/GPU renderer parity, and stable behavior on images large
enough to exercise the existing tiled execution policy. Until those gates are closed, this spec
must remain visible in the Professional backlog and roadmap.
