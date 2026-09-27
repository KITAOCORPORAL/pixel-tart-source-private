# High bit-depth pipeline

Status: **PARTIAL**. `RawDecodedImage` records `BitsPerChannel`, pixel format and optional
RGB48 samples. `HighBitDepthImageBuffer.FromRaw` consumes native `ushort` samples and converts
once to normalized float RGB. Professional Color Studio and the V4 CPU path now consume that
float buffer directly; display conversion is isolated to `ToVisualRgb24()`. Preset and the
current deterministic film adapter preserve float processing. End-to-end vendor RAW → WPF
loading → full Film parity → TIFF16 remains fixture-pending.
