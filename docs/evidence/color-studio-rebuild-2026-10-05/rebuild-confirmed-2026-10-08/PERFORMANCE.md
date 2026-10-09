# Performance evidence boundary

Hardware: Intel Core i5-12400F, NVIDIA GeForce GTX1650; Windows10 build19045; SDK10.0.302. GPU matching performance not measured.

Reproduced curve starvation: synthetic RGB1200×20, one Curve node,35 edits20ms apart, no preview frames before release on baseline. Same test now demands more than1 intermediate frame and an800px proxy, then latest1200px final frame after release, one undo transaction and exact final control point. Assertions are unchanged, not weakened. `curve-baseline.trx` failure retained; final regression passes.

Production scheduling: one interactive pump;80ms lightweight cadence,300ms settled request, current revision guards, cancellation and stopped-edit revision prevent obsolete work resurfacing. These are policy intervals, **not** measured input-to-screen latency. Source/model changes can still cost decoding, processing and composition time.

Zoom regression uses source10000px, Fit8%, first wheel8.96%; old clamp incorrectly25%. Repaired finite/Fit-relative floor and pan bounds pass deterministic tests. This establishes correctness, not sustained native responsiveness or memory stability.

Native continuous wheel/pan/curve baseline-vs-repair timing, full image dimensions/node stacks, repeated sample median/P95, release-to-final duration and memory time series are BLOCKED by desktop input/capture. No50ms claim, fixed improvement percentage or single-unit-test duration is substituted for these measurements.
