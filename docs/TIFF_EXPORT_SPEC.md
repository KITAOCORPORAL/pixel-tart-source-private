# TIFF export specification

Implemented baseline: uncompressed little-endian RGB TIFF from a managed `VisualPixelBuffer`, with 8-bit or 16-bit samples, dimensions, samples-per-pixel, rows-per-strip, software tag, optional validated ICC profile tag and cancellation checks. Invalid ICC payloads are rejected.

The 16-bit writer expands each 8-bit source channel to the full 16-bit range (`channel × 257`). It does not claim a 16-bit RAW source; preserving RAW precision requires the high-bit-depth decoder stage first.

Compression values LZW and ZIP/Deflate, complete EXIF carry-through, wide-gamut colour transforms and production WPF export wiring remain deferred until round-trip fixtures are available.
