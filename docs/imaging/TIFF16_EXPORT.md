# TIFF 16-bit export

Status: **PARTIAL**. The existing deterministic writer emits uncompressed little-endian RGB48,
supports cancellation and validates/embeds an ICC payload. DPI is currently the writer's 72 DPI
baseline and full EXIF/orientation/read-back round trip is not yet closed.
