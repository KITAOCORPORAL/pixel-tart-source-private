# Match V4 GPU OOM and device-loss handling

The Core `MatchV4PixelExecutor` retains the same transform hash and processing generation when a GPU backend throws, then executes the CPU pixel oracle. The DX12 adapter shrinks its pointwise tile after an allocation failure and retries the current row.

Acceptance status:

- CPU fallback identity: PASS — focused test;
- DX12 allocation shrink on real hardware: NOT RUN;
- device-lost injection: NOT RUN;
- partial-result publication: source result is only returned after all tiles complete;
- WPF recovery: NOT RUN.

This is safe infrastructure, not a claim of complete production fault-injection coverage.
