# 3D Color Space Windows Phase 2

Planned Windows view modes are Source, Reference, Matched, Overlay, and Migration. Camera reset, fit, L slice, a/b plane, eyedropper linking, protection filters, and strength updates must operate on the existing model.

Current state: PARTIAL. `ColorSpace3DViewport` is now a real Windows surface consuming `ColorSpaceVisualizationModel`; it supports source/reference/matched/overlay visibility, migration vectors, axes, rotate, pan, zoom, reset and fit. Production native pointer and screenshot gates remain open.
