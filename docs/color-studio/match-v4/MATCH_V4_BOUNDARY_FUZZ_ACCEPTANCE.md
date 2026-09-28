# Match V4 Boundary Fuzz Acceptance

- Golden threshold-adjacent samples: PASS.
- Deterministic random samples: 100,000 generated with seed `20260928`, PASS.
- Dense boundary set: luminance, hue, chroma and zone boundaries at `±1e-4`, PASS.
- Classification mismatch between canonical CPU decision and the GPU-consumed decision key: `0`.

The test verifies that the GPU path receives the same integer decision key. It does not claim that GPU transcendental functions are bit-identical to CPU functions; those functions no longer decide the hard branches.
