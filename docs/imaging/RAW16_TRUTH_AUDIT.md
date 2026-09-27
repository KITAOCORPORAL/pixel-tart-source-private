# RAW16 truth audit

Audited at the current development worktree (base `cc661b1`, precision increment uncommitted at audit time).

| Stage | Current truth |
|---|---|
| FastPreview | `LibRaw OutputBps = 8`; `ProcessedImage.AsSpan<byte>()`; RGB24/sRGB. |
| ProfessionalDecode | `LibRaw OutputBps = 16`; requires `ProcessedImage.Bits == 16`, 3 channels; reads `AsSpan<ushort>()` into `RawDecodedImage.Rgb48Pixels`. |
| Embedded preview | Best-effort `UnpackThumbnail`/`MakeDcrawMemoryThumbnail` API now exists and returns an 8-bit preview or null; no vendor fixture verifies availability. |
| RawDecodedImage | RGB48 is `ushort[]`; preview RGB24 remains available only on the fast path. |
| HighBitDepthImageBuffer | `FromRaw` consumes RGB48 and converts once to float RGB normalized by 65535. |
| Color Studio | Professional overload consumes `HighBitDepthImageBuffer` and keeps float RGB for preset, transition, color range and the reference-look transform. `VisualPixelBuffer` remains the display/legacy adapter. |
| V4 | `ReferenceMatchV4Engine.Match(HighBitDepthImageBuffer, ...)` now samples and applies residuals in float RGB with CPU fallback; GPU backend still covers only its existing representative OT kernel. |
| Preset | Professional preset node runs directly on the float working buffer with parameter-space strength. Legacy byte entry remains for JPEG/PNG/WPF callers. |
| Film | Professional path has a deterministic float profile/grain adapter; full parity with the existing film feature set (halation/bloom/texture/vignette) is not yet closed. |
| TIFF16 | `TiffExport.WriteRgb48` consumes the canonical processing buffer. Atomic writer validates read-back before publishing; ICC, DPI and canonical orientation are covered, while camera EXIF metadata and compression remain partial. |

Therefore: RAW16 decode is real at the decoder boundary and the core professional Color Studio/V4/preset path no longer requires an RGB8 round-trip. The end-to-end chain remains **PARTIAL** because vendor fixtures are absent, the WPF loading/export callers are not all migrated to the professional overload, film feature parity is incomplete, and GPU per-pixel stages are not yet accelerated.

No legal vendor fixture is present. Tonal-level tests use an in-memory RGB48 contract fixture and do not claim camera compatibility.
