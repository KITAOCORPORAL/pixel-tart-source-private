# Match V4 Tile Edge Acceptance

Post-fix forensics compared 256, 512, 1024, and 2048 tile edges. X-T5 and GFX100S had identical outlier counts and maxima across all four geometries before the protection-boundary fix. After the fix, all tested sizes had zero pixels above 1e-4, 1e-3, and 1e-2. The 2048 configuration is whole-image for the proxy and is evidence against a tile copy defect.

Partial width, partial height, bottom-right tile, odd proxy dimensions, and tile-local coordinates were recorded by the forensics runner. No tile-dependent coordinate movement was observed.

Status: PASS for proxy numerical tile independence and full-resolution parity closure.
