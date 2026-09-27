# RAW16 truth audit

Audited at commit `b14d8fa`.

| Stage | Current truth |
|---|---|
| FastPreview | `LibRaw OutputBps = 8`; `ProcessedImage.AsSpan<byte>()`; RGB24/sRGB. |
| ProfessionalDecode | `LibRaw OutputBps = 16`; requires `ProcessedImage.Bits == 16`, 3 channels; reads `AsSpan<ushort>()` into `RawDecodedImage.Rgb48Pixels`. |
| Embedded preview | Best-effort `UnpackThumbnail`/`MakeDcrawMemoryThumbnail` API now exists and returns an 8-bit preview or null; no vendor fixture verifies availability. |
| RawDecodedImage | RGB48 is `ushort[]`; preview RGB24 remains available only on the fast path. |
| HighBitDepthImageBuffer | `FromRaw` consumes RGB48 and converts once to float RGB normalized by 65535. |
| Color Studio | **PRECISION BREAK**: current `ColorStudioRenderPipeline.Render` accepts `VisualPixelBuffer` only and all stages operate on `Rgb24` bytes. |
| V4 | Existing V4 entry points consume `VisualPixelBuffer`; high precision RAW is not yet wired into V4. |
| Preset | Existing preset node is applied inside the byte-based Color Studio pipeline. |
| Film | Existing `PixelTartFilmPipeline` consumes/returns `VisualPixelBuffer`. |
| TIFF16 | `TiffExport.WriteRgb48` consumes `HighBitDepthImageBuffer`; it is real RGB48 when called with RGB48-derived buffer, but current Color Studio output cannot reach it without quantization. |

Therefore: RAW16 decode is real at the decoder boundary, but the professional end-to-end chain is **PARTIAL**. The byte conversion occurs at the Color Studio API boundary, before V4, Preset and Film. Display previews must remain separate from the canonical processing source; this separation is not yet complete in the existing WPF path.

No legal vendor fixture is present. Tonal-level tests use an in-memory RGB48 contract fixture and do not claim camera compatibility.
