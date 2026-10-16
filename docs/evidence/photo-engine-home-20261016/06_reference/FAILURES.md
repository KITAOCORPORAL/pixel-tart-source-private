# Stage6 failure history (preserved)

1. Initial home reference executable refused the linear fixture because the typed expected SHA had a transcription error (F68 vs FC8). Compared with the repository original hash, corrected the audit constant, no profile bytes changed. First log retained.
2. WIC ColorConvertedBitmap RGB48-to-RGB48 then raised0x88982F80. This is an actual unsupported path, recorded as UNSUPPORTED_PIXEL_FORMAT; no lower-bit surrogate marked as16bit support.
3. Direct LCMS2.12 vs ideal analytic sRGB initially failed the unchanged12code bound (max38). We did not raise the tolerance or delete the first JSON. Independent calculation using actual profileD50 colorants and1024pointcurve gives max6; PillowLCMS2.19 separate binary8bit output matches max0. Different target-profile vs ideal formula assumptions explained, not “auto calibrated”.
4. Native-reference imports succeed from current existingproductLCMS2.12, not reviewedLCMS2.16. We do not change production hash allowlist, replace nativeDLL, or count this as cleanVM/source-build approval. Python dependency versions and binary hashes captured; no global install performed.

Both corrected reference and the original failures are retained. Fuji default-path nondeterminism is a separate product acceptance blocker regardless of these numerical passes.
