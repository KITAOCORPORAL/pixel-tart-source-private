# RAW colour pipeline

## Current implementation

`Sensor data → LibRaw black/white handling and demosaic → white balance → camera matrix → sRGB 8-bit RGB24 → Color Studio / Reference Match → Film → output transform`.

The current LibRaw contract requests an 8-bit sRGB bitmap. This is safe for the existing RAW-to-JPEG task but does not satisfy the professional high-bit-depth requirement. A future release must expose linear or wide-gamut high-bit-depth decoded samples before Color Studio and preserve them through 16-bit TIFF export.

## Boundaries

- Embedded JPEG preview is suitable for browsing only.
- Reference Match V4 must receive a full decode for final export.
- No source file is overwritten by the current conversion service.
- ICC profiles must be embedded from a validated profile source; this revision does not fabricate profiles.
