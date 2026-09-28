# Match V4 architecture (Phase 1)

```text
FrozenRawMaster + canonical high-precision reference
        |
        v
MatchTransformV4 (immutable, validated, hashed)
        |
        +--> CPU reference executor
        `--> GPU representative-kernel executor (experimental)
                    |
                    `--> CPU fallback with the same transform
        |
        v
HighBitDepthImageBuffer -> display adapter / TIFF16 export
```

`MatchV4ProductExecutor` requires a `FrozenRawMaster`, a high-precision reference buffer, and
the master processing generation. It validates the source SHA and carries the generation and
transform hash in the result. GPU failure does not re-analyze or create a new transform.

This is a product seam, not a Production UI switch. Full Color Studio integration requires a
canonical high-precision reference analysis and an explicit v3/v4 engine setting in a later phase.
