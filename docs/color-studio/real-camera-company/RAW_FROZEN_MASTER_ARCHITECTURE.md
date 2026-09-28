# Frozen RAW master architecture

## Contract

For one Color Studio editing session:

```text
RAW path + source SHA
        |
        | ProfessionalDecode once (RGB48)
        v
FrozenRawMaster
  - immutable high-precision float working image
  - SourceSha256
  - DecodeGenerationId
  - ProcessingGenerationId
        |
        +--> preview proxy/display adapter
        +--> Match v3 state snapshot
        `--> full-resolution TIFF16 export
```

`FrozenRawMaster` is session-owned by `ReferenceTargetItem`. The RAW thumbnail, active preview,
Match processing, and TIFF16 export now reuse the same object. Export validates the source SHA
before writing; if the source changed, it fails without creating a destination file rather than
silently decoding a second version. RGB24 remains a display adapter only.

The old path-based pipeline API remains for callers that explicitly request a new standalone
operation. The product ViewModel path uses the frozen-master overload.

## Memory and eviction rule

The current implementation keeps one active master per imported target. A future bounded LRU may
evict a master, but eviction must invalidate its derived preview/Match state or create a new
decode and processing generation before export. It must never present decode A preview with
decode B export under one generation identity.

## Evidence

Core tests cover one decode for preview/render/export and source mutation rejection. WPF product
tests cover RAW import, Match preview, TIFF16 export/read-back, and one decoder invocation. The
private real-camera run remains required for final acceptance; the company binary fixtures are not
tracked.
