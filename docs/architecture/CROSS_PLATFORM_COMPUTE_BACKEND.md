# Cross-platform compute boundary

```text
Pixel Tart Core
  MatchTransformV4 / color science / HighBitDepthImageBuffer
          |
          v
  IMatchV4ComputeBackend
       /              \
  CPU reference       Windows DX12 / ComputeSharp
                      `-- future Metal (macOS/iPadOS)
```

The interface carries capability, memory budget, execution timings, cancellation, and typed
failure information without exposing ComputeSharp, DirectX, WPF, or platform file APIs. The CPU
backend remains the oracle. A Metal implementation is intentionally not present in this phase;
the boundary is the only commitment for future macOS/iPadOS work.
