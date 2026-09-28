# Match V4 GPU memory model

`GpuMemoryBudget` remains a conservative descriptor derived from dedicated/shared memory. It
selects representative sample/tile suggestions; it is not proof that a large image is tiled or
that VRAM is reserved safely.

The current ComputeSharp implementation allocates representative source/reference channels and
an `n × m` float pairwise matrix, then reads it back. Full source/output images remain CPU
buffers. GFX100S full-image GPU execution, live VRAM peak, OOM-triggered tile shrink, and seam
validation are not yet implemented.
