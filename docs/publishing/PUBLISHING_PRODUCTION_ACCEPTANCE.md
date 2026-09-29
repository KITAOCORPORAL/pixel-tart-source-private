# Publishing production acceptance

Source of truth: commit under test on `integration/pixel-tart-developer-preview`.

## Verified in this phase

- `ExportRecipe` validation rejects rooted paths, traversal segments, reserved Windows names, and unknown filename tokens.
- Multi-recipe execution accounts for every asset × recipe item, including cancellation of the current and pending items.
- Temporary `.publishing` files are removed after success, failure, or cancellation; the source SHA-256 remains unchanged.
- Real WPF encoders produced JPEG, PNG, and TIFF16 outputs in one multi-recipe run.
- TIFF16 read-back verified RGB, 16 bits per sample, dimensions, DPI, and embedded output ICC payload.
- TIFF16 resize uses the high-precision buffer path. TIFF16 watermark composition is explicitly rejected until a high-precision compositor exists; it is not silently quantized through `Pbgra32`.

## Not claimed

- RAW files are not accepted by the Publishing page; RAW must go through the canonical RAW pipeline first.
- Photoshop, Lightroom, and Capture One application compatibility was not physically tested on this machine.
- Full EXIF/IPTC/XMP policy coverage and every requested ICC family (Display P3 and ProPhoto RGB) remain open.
- Metadata payload round-trip beyond the tested ICC/DPI/orientation fields remains partial.
