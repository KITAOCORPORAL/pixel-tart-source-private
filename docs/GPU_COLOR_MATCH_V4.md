# GPU Color Match V4

Current implementation keeps CPU as canonical fallback. ComputeSharp executes the pairwise OT
stage; V4 telemetry now has stage dispatch/time, allocation peak, fallback/OOM and tile counters.
`V4TilePolicy` selects bounded tiles from image size and VRAM tier. OKLab transform, mask,
residual, gamut and final pixel stages remain CPU/shared until each receives a real shader and
float-parity acceptance evidence.
