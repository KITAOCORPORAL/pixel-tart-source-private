# Match V4 GPU Phase 3 closure

Final status: **MATCH_V4_GPU_EXPERIMENTAL / PARTIAL**.

The main Phase 3 implementation milestone is complete in the isolated Windows adapter: the GPU can transform complete float32 pixel tiles and return a complete high-precision buffer. The Core remains free of ComputeSharp, DirectX, WPF, and Win32 dependencies.

The product is not GPU-ready because runtime hardware evidence, real-camera evidence, WPF engine selection, and GPU TIFF16 export wiring are not complete. Match V3 remains the stable product engine.

The full repository test run is not green for an unrelated existing assertion: `WorkbenchVisualCorrection201Tests.WorkbenchDefaultDarkResources_AreComplete` expects the historical `#111016` value while the current resource contains `#111312`. This phase did not modify that resource. Focused Phase 3 contract tests remain 5/5 passing.
