# Professional RAW pipeline audit

Status: **PARTIAL / Stage 2 gate blocked**.

The current production path uses `Sdcb.LibRaw` (`LibRawDecoder`) for candidate RAW extensions and performs file identity checks before and after decode. It reads basic make, model, capture time and orientation metadata, supports cancellation, and maps decode failures to safe task results.

Current flow:

`RAW file → LibRaw unpack/demosaic → camera white balance or auto white balance → camera matrix → sRGB 8-bit RGB24 → existing Color Studio pipeline`.

`FastPreview` requests `OutputBps = 8`, while `ProfessionalDecode` requests `OutputBps = 16`
and reads native ushort RGB48 when LibRaw returns 16-bit data. Embedded preview reuse, full
decode cache, complete camera metadata preservation and legal vendor fixtures remain open
release work. No camera format is marked verified without a repository fixture.

The existing decoder package and licenses are retained. No Bayer or X-Trans decoder is implemented in Pixel Tart.

`HighBitDepthImageBuffer` provides a shared float working representation for RGB48-decoded
buffers and 16-bit export adapters. The display `VisualPixelBuffer` remains a presentation
adapter; professional Color Studio/V4 callers use the float representation directly.

Stage 1 GPU progress does not change this RAW gate. Reference Match V4 GPU currently accelerates the pairwise transport kernel after a full RGB buffer exists; it does not provide a RAW decoder or high-bit-depth sensor path.
