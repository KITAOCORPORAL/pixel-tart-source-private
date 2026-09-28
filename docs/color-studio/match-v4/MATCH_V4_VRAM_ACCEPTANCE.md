# Match V4 VRAM acceptance

The four/eight/sixteen-GB tier contract is now tested as a throughput descriptor: it changes
sample/tile suggestions, never the transform or color semantics. There is no live free-VRAM
query or 100%-of-VRAM allocation. OOM shrink and CPU fallback remain required for the future
full-image backend.

Current status: **PARTIAL**. Unknown peak VRAM is reported as UNKNOWN, never guessed.
