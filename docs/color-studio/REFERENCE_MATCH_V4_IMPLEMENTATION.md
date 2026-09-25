# Reference Match V4 implementation

Implemented in `src/RAWSelectionAssistant.Core/Services/Projects/ReferenceMatchV4.cs`.

| Area | State |
|---|---|
| bounded OKLab representative samples | IMPLEMENTED |
| regularized Sinkhorn OT | IMPLEMENTED, CPU |
| global + luminance-band local transfer | IMPLEMENTED, soft bounded foundation |
| neutral / skin / highlight / shadow protection | IMPLEMENTED as deterministic hooks |
| gamut compression | REUSED from Match v3 |
| decomposition metadata | IMPLEMENTED, foundation fields |
| residual correction | IMPLEMENTED, max four passes and early stop |
| cancellation | IMPLEMENTED |
| tile overlap planner | IMPLEMENTED metadata/helper |
| shared Preview/Export integration | REUSED existing pipeline; no UI integration this phase |
| GPU compute | DEFERRED; unavailable seam + CPU fallback implemented |
| semantic/AI matching | DEFERRED to V5/V6 |

The implementation deliberately avoids a second renderer, large model weights and an NVIDIA-only dependency. Production UI integration remains gated by the unresolved Phase 1 native drag visual evidence.
