# Professional RAW pipeline

Status: **PARTIAL**.

The decoder now has explicit `FastPreview` and `ProfessionalDecode` modes. The professional
mode requests LibRaw `OutputBps = 16` and carries interleaved RGB48 samples into
`HighBitDepthImageBuffer`; the existing 8-bit path remains the safe JPEG/thumbnail path.
No legal vendor RAW fixtures are present, so camera decode and metadata claims remain
`NOT VERIFIED`.
