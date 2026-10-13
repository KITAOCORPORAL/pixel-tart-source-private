# DSC09916.ARW — diagnostic refusal, not camera-wide support

SHA2566F596BB234EE5DE206C66594BC84205349C363FD6A89AE188FF76DAB7C5E53B9 matches existing company test manifest.24,563,712bytes; little-endian TIFF header49492A00; original opened read-only.

Actual main SubIFD at138498:5120x3584, Compression7, PhotometricInterpretation6(YCbCr), BitsPerSample15/15/15, StripOffset1642496, StripBytes22919824. The first payload JPEG SOF3 describes16bit512x512 three-component lossless-JPEG; later payload blocks repeat this structure. Reduced4096x3072 8bit preview is a separate directory. These tags are NOT CFA32803. Previous batch summary calling it CFA is corrected here, not silently edited in historical evidence. Exact Sony acquisition/re-encoding history is UNKNOWN; no standard sensor-mosaic compression claim is inferred from extension/model.

Native LibRaw0.21.1-Release, managed Sdcb.LibRaw0.21.1.7, x64. RawContext.OpenFile, production FastPreview, legacy ProfessionalDecode and PV4 all fail at OpenFile before Unpack/WB/process options; native code-2 FileUnsupported. Direct LibRawNative.OpenBuffer returns the same code, independent of filename/path and managed OpenFile wrapper. Loaded raw_r/jpeg8/lcms2/zlib DLL paths/hash recorded in SONY_DECODE.json. Earlier ASCII-copy result agrees. Thus path, x86 mismatch, missing DLL and decode parameters are not supported root causes. Proven blocker is current native identification/format rejection of this particular three-component YCbCr lossless-JPEG TIFF arrangement.

Text-only HTTP checked official NuGet flat-container indexes: latest listed managed0.21.1.7/runtime.win640.21.1 (no newer package in this channel). Pinned LibRaw0.21.1 tiff.cpp source identifies Sony lossless RAW only with phint32803/comp7; current fixture phint6 does not match that branch. This supports the rejection explanation; it does not prove file corruption or that a future LibRaw cannot decode it.

No dependency replacement was performed. Existing runtime license expression LGPL-2.1-only OR CDDL-1.0 retained; no unverified native binary downloaded/distributed. New stable refusal RAW_NATIVE_FILE_UNSUPPORTED includes stage/native code/name; previous valid edit result remains. No JPEG preview, lower precision or silent alternate decoder fallback.

Status: diagnostic rejection DELIVERED; decoding this file remains BLOCKED_NATIVE_FORMAT. A7M4 model listing is not file support. A validated upstream/native candidate with provenance/license/old-corpus regression is required before widening support.

Primary references checked via text HTTP: https://api.nuget.org/v3-flatcontainer/sdcb.libraw/index.json ; https://api.nuget.org/v3-flatcontainer/sdcb.libraw.runtime.win64/index.json ; https://raw.githubusercontent.com/LibRaw/LibRaw/0.21.1/src/metadata/tiff.cpp .
