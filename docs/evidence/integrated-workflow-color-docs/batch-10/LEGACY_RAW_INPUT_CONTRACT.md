# Legacy RAW CurrentEdits — PV6 contract

## Persisted identity and migration

ProfessionalRawDefinition ProcessingVersion=6, InputConfiguration=LegacyRawEditsSrgbFloatV1, DisplayTransform=LegacyRawEditsIdentityV1, WhiteBalance=Rendered. InstantInputRecipe.Version=2 contains source edit version ID, complete Look, deep-cloned stack, old film settings and LegacyRawEditInputContract.Version=1. The nested contract records source SHA256, stack ProcessingVersion 1/2, stable engine/execution mode, ProfessionalDecode RGB16 sRGB camera-WB/no-auto-rotate contract, orientation normalization and full-resolution legacy renderer identifier. Complete recipe participates in DecodeKey.

PV4, PV5 and instant AlgorithmVersion1/2 are not redefined. LegacyRaw=null is omitted from old recipe serialization; IsRenderedInput is ignored in JSON. Existing no-token ToVisualRgb24 entry remains available. New PV6 disallows RAW gains, nonzero ExposureEv, camera WB and scene display transform. The instant V1/V2 layer follows already-rendered sRGB exactly once.

## Production call chain

GenerateInstantFilmAsync freezes current editor state and establishes the source edit version before asynchronous work. Initial old RAW conversion reuses target.RawMaster, the exact immutable full-precision decode used by legacy editing, validates its source hash, and calls LegacyRawInstantInput.Render. It never uses SourceThumbnail, Editor.MatchedImage or another UI proxy.

Render normalizes source EXIF orientation before processing and calls the existing RawMatchTiff16ProductPipeline.Render at maximumEdge=0 with the complete frozen Look/stack/old film. Its ProcessingPixels float16-origin sRGB buffer becomes the new master without RGB8 preview conversion. New RawInput describes adjustments already baked. The source version is retained; commit happens after ready pixels and the unchanged presentation timeline. Cancel/failure does not install a late result.

Reopen or cache miss decodes the full original source with the persisted legacy decode contract, then executes the same recipe. Source hash mismatch rejects reconstruction. Quick export and Publishing use the same PV6 definition; Publishing receives the full TIFF16 intermediate, not an enlarged preview. The existing TIFF/PNG/JPEG verification and paper mapping remain unchanged.

## Supported and rejected paths

Stable legacy engine: known persisted node types (ReferenceMatch, ColorRange, Film, TransitionBlend, Preset, Develop, WhiteBalance, BasicTone, ColorBalance, Levels, Curve, Details, SkinTone) execute through their existing renderer. ReferenceMatch without Look is explicitly rejected rather than skipped. Invalid stack/tool versions/parameters and absent full-precision processing output are rejected. MatchV4Beta is explicitly rejected for this bridge; existing result and version remain intact. No generic fallback silently drops tools.

The change does not guarantee that every real camera decodes deterministically: known Sony decode failure and Fuji cross-decode limitations remain. New recipe snapshots do not contain vendor calibration data.

## Lifetime and budgets

HeavyWork serializes reconstruction. Decode and full processing are separately reserved; processing reservation precedes orientation allocation and includes retained source plus conservative scratch. Budget failure does not reduce dimensions/precision. Active frozen references remain alive when the owning cache is replaced/evicted. On success the PV6 cache supersedes the old master; no duplicate native decode occurs on initial conversion.

No-reference stacks skip an otherwise unused full RGB8 source-analysis allocation. Full output's RGB8 display adapter now polls cancellation every8192 samples without changing rounding/math. Existing node loops poll cancellation. Native LibRaw calls remain cooperatively cancellable only at existing stage boundaries, not an OS hard abort. Native/GC memory is not a 2GiB working-set promise.
