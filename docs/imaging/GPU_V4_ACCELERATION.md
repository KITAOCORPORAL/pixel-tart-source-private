# V4 GPU acceleration

Status: **PARTIAL**. ComputeSharp/DX12 executes the representative pairwise OT kernel with CPU
fallback and tested parity for that scope. Sinkhorn, residual application, gamut mapping and
telemetry/tiled high-resolution execution remain CPU/shared or pending. Device creation alone is
not treated as full V4 acceleration.
