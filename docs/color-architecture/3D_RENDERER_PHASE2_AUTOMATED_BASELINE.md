# 3D Color Space renderer phase 2 automated baseline

The repository now has a bounded, platform-neutral projection core. It projects the existing OKLab cloud through the canonical `ColorSpaceCamera` and performs depth-prioritized hit testing on a batched point list.

- Modes remain the existing `ColorCloudMode` values: Source, Reference, Matched, Overlay, Migration.
- Color science remains in `OklabColorSpace`/`ColorSpaceCoordinate`; projection does not convert RGB to Lab.
- Projection and hit-test are deterministic and tested.
- A WPF surface, native interaction walkthrough, screenshot QA and FPS benchmark are not yet implemented.

This is an automated core checkpoint, not a native renderer acceptance.
