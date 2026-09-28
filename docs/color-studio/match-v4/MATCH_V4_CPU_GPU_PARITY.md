# Match V4 CPU/GPU parity

Current evidence remains limited to synthetic scenes and the representative pairwise kernel.
The focused V4 suite passes CPU determinism, transform identity, high-precision generation
propagation, unavailable-GPU fallback, injected GPU failure fallback, and the existing eight
scene CPU/GPU 8-bit parity campaign when a validated DX12 device is present.

The GPU result is **not** a full-image GPU result: Sinkhorn iterations, barycentric mapping,
residual refinement, protection and pixel application still execute in the CPU semantic path.
Therefore this document does not claim production GPU preview/export parity or a speedup.

Required before Production: high-precision CPU/GPU parity metrics (mean/P95/max), real RAW
GFX/X-T5/R6 route, cancellation/device-loss/OOM campaigns, and full preview/TIFF16 parity.
