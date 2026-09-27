# V4 GPU acceleration

Status: **PARTIAL**. ComputeSharp/DX12 executes the representative pairwise OT kernel with CPU
fallback and tested parity for that scope. Sinkhorn, residual application, gamut mapping and
telemetry/tiled high-resolution execution remain CPU/shared or pending. Device creation alone is
not treated as full V4 acceleration.
Current status: **PARTIAL**. The V4 engine has a float CPU entry point and preserves the CPU
fallback contract. ComputeSharp remains an optional accelerator for the existing representative
pairwise OT kernel; RGB↔OKLab, mask evaluation, residual application and gamut/pixel stages are
still CPU stages. No full-pipeline GPU completion claim is made.
