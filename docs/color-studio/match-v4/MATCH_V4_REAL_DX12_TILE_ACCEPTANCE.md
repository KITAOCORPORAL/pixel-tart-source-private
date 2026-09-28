# Match V4 real DX12 tile acceptance

The adapter now performs real ComputeSharp buffer allocation, dispatch, and readback for bounded pointwise tiles. Tile overlap is not required for the current pointwise transform.

Acceptance status:

- code path: IMPLEMENTED;
- compile: PASS;
- tile whole-vs-tile numerical result: PASS on GTX 1650 synthetic 256x192; mean/P95/P99/max `0`;
- boundary error: UNKNOWN;
- 102MP GFX100S: NOT RUN;
- cancellation: PASS — pre-dispatch cancellation observed;

The phase must not be described as 102MP-ready: GFX100S DX12 processing and live VRAM telemetry remain NOT RUN. The synthetic pointwise tile result has no boundary error, but it does not close a product gate.
