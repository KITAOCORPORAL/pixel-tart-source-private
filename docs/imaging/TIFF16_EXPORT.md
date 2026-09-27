# TIFF 16-bit export

Status: **PARTIAL**. The deterministic writer emits uncompressed little-endian RGB48 from the
canonical float processing buffer, supports cancellation, atomic temporary-file publication,
ICC payload validation/read-back, explicit DPI and TIFF Orientation (canonical default `1`).
Camera EXIF metadata, compression and complete production batch evidence remain open.
