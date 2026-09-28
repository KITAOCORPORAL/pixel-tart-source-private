# Match V4 GPU Cross Hardware Comparison

## Evidence available

| Hardware | Backend | Real RAW fixtures | GPU route | Result |
|---|---|---:|---|---|
| RTX 5060 Ti 16GB | ComputeSharp-DX12 | 4 | Reached GPU, no fallback | 4 full-resolution parity PASS; cross-hardware comparison NOT RUN |

The repository contains no second hardware run in this acceptance. Synthetic parity and historical CPU evidence are not substituted for a cross-hardware benchmark. No claim is made for CUDA, ONNX Runtime, DirectML, or another backend.

## Open comparison

- Repeat the same four logical fixtures on a second supported DX12 adapter.
- Persist adapter, driver, VRAM budget, tile plan, GPU timings, fallback state, and unchanged parity thresholds.
- Compare preview and export transform hashes before interpreting throughput.

Status: NOT RUN for cross-hardware real RAW comparison. RTX 5060 Ti is the only real hardware run in this closure; prior GTX 1650 evidence is synthetic/runtime-only.
