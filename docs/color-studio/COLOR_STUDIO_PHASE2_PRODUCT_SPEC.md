# Color Studio Phase 2 Product Spec

**Status: PARTIAL CORE IMPLEMENTATION — PRODUCTION UI BLOCKED BY PHASE 1 REGRESSION.**

## Positioning

3D Color Space is an image-first visual explanation layer for the current photo. It is not another editor and must not turn Color Studio into a 3D application.

## First slice

- OKLab D65 point cloud with L*, a*, b* axes.
- Rotate, zoom, and reset view.
- Current image color distribution only; use bounded proxy/reduction.
- Canvas eyedropper highlights the corresponding point/sample.
- Positive and negative samples use distinct, non-purple Pixel Tart states.
- A selected cluster can request a temporary selection preview; it never writes the final adjustment automatically.

The current implementation provides the bounded core point/marker/cluster data contract. The WPF panel and native gestures are not yet implemented.

## Non-goals

No mesh, volume rendering, particle effects, AI auto-grade, generative color, cloud, video/HDR/CMYK, printing, marketplace, or browser plugin.

## Acceptance intent

The image remains the dominant visual surface. Opening the 3D view must not visibly stall current preview; all expensive work is cancellable and off the UI thread. Exact interaction and performance thresholds belong to the Phase 2 test plan, not this blocked audit.
