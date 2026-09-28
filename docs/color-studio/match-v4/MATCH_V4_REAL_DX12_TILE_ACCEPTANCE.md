# Match V4 real DX12 tile acceptance

The adapter now performs real ComputeSharp buffer allocation, dispatch, and readback for bounded pointwise tiles. Tile overlap is not required for the current pointwise transform.

Acceptance status:

- code path: IMPLEMENTED;
- compile: PASS;
- tile whole-vs-tile numerical result: NOT RUN on a DX12 device;
- boundary error: UNKNOWN;
- 102MP GFX100S: NOT RUN;
- cancellation: contract checks exist, hardware dispatch test NOT RUN.

The phase must not be described as seam-free or 102MP-ready until those runtime measurements exist.
