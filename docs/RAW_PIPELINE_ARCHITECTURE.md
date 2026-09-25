# Professional RAW pipeline audit

Status: **PARTIAL / Stage 2 gate blocked**.

The current production path uses `Sdcb.LibRaw` (`LibRawDecoder`) for candidate RAW extensions and performs file identity checks before and after decode. It reads basic make, model, capture time and orientation metadata, supports cancellation, and maps decode failures to safe task results.

Current flow:

`RAW file → LibRaw unpack/demosaic → camera white balance or auto white balance → camera matrix → sRGB 8-bit RGB24 → existing Color Studio pipeline`.

The decoder currently requests `OutputBps = 8`; therefore it is not yet a high bit depth working pipeline. Embedded preview reuse, full decode cache, complete camera metadata preservation and RAW-to-16-bit-TIFF integration remain open release work. No camera format is marked verified without a repository fixture.

The existing decoder package and licenses are retained. No Bayer or X-Trans decoder is implemented in Pixel Tart.

Stage 1 GPU progress does not change this RAW gate. Reference Match V4 GPU currently accelerates the pairwise transport kernel after a full RGB buffer exists; it does not provide a RAW decoder or high-bit-depth sensor path.
