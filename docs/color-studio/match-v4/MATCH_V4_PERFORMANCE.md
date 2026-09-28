# Match V4 performance

The existing historical synthetic record reports approximately equal CPU/GPU wall times because
only the pairwise kernel is accelerated. It does not measure peak VRAM, utilization, or full
product throughput. No new benchmark is claimed in Phase 1.

The next benchmark must report analysis, upload, compute, readback, pixel application, total,
peak RAM and peak VRAM for 24/45/60/102MP buffers, with CPU and GPU using the same transform and
processing generation. A speedup target is intentionally not hard-coded before those numbers
exist.
