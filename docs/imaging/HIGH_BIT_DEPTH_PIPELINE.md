# High bit-depth pipeline

Status: **PARTIAL**. `RawDecodedImage` records `BitsPerChannel`, pixel format and optional
RGB48 samples. `HighBitDepthImageBuffer.FromRaw` consumes native `ushort` samples when present
and exposes explicit source depth and working colour space. Precision tests require more than
256 unique tonal levels. End-to-end vendor RAW → Color Studio → V4 → Film → TIFF16 remains
fixture-pending.
