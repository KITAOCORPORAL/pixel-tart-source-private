# TIFF16 production validation

The production TIFF16 route now uses `HighBitDepthImageBuffer` and the managed RGB48 writer. Resize uses float32 sampling; an 8-bit WPF render target is not used for TIFF16 resizing.

Automated evidence in `PublishingRenderingTests` covers:

- `BitsPerSample = 16`, RGB samples, dimensions, and DPI read-back;
- more than 256 distinct tonal values after resize;
- embedded ICC payload;
- source SHA preservation;
- explicit rejection of TIFF16 watermark requests that would require an 8-bit compositor;
- real multi-recipe JPEG/PNG/TIFF16 output and TIFF16 read-back.

The writer is uncompressed little-endian RGB and does not claim broad application compatibility without application-level testing.
