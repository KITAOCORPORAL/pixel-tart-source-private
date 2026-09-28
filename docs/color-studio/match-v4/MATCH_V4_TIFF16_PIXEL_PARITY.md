# Match V4 TIFF16 Pixel Parity

CPU and GPU exports were produced from the same FrozenRawMaster, reference identity, transform settings, strength, and processing generation after full-resolution parity passed.

| Fixture | TIFF export | Exact RGB48 pixel ratio | Within ±1 code | Max code delta | Mean code delta | Mean OKLab ΔE | Transform identity |
|---|---|---:|---:|---:|---:|---:|---|
| Canon EOS R6 CR3 | TIFF16_EXPORT_COMPLETED | 98.18598% | 100% | 1 | 0.00610 | 1.45e-7 | same |
| Fuji X-T5 RAF #1 | TIFF16_EXPORT_COMPLETED | 98.94446% | 100% | 1 | 0.00354 | 9.22e-8 | same |
| Fuji X-T5 RAF #2 | TIFF16_EXPORT_COMPLETED | 98.66542% | 100% | 1 | 0.00447 | 1.12e-7 | same |
| Fuji GFX100S RAF | TIFF16_EXPORT_COMPLETED | 97.94091% | 100% | 1 | 0.00693 | 1.49e-7 | same |

`TIFF16_PARITY_PASS` means both files were reopened as RGB 16-bit TIFF and every channel differed by at most one code value; it is separate from successful file writing. ICC and EXIF propagation remain partial and are not credited by this numerical gate.
