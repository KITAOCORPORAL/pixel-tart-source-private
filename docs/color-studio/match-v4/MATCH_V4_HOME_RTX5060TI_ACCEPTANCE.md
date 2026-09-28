# Match V4 Home RTX 5060 Ti Acceptance (2026-09-28)

## Scope

External real files only; corpus remains outside Git. This report records the guarded MatchV4Beta route on an NVIDIA GeForce RTX 5060 Ti 16GB. Stable Match v3 remains the product default.

## Hardware and route

- Adapter: NVIDIA GeForce RTX 5060 Ti
- Dedicated VRAM: 16,829,644,800 bytes
- Backend: ComputeSharp-DX12 / DirectX 12
- Memory tier: ULTRA; tile 1024; parallel tiles 4
- Route: RAW -> LibRaw ProfessionalDecode -> RGB48/float32 FrozenRawMaster -> one resolved MatchV4ProductSession -> CPU or DX12 tiled pixel execution -> Atomic TIFF16
- Output is external; no RAW/TIFF binary is committed.

## Real fixtures

| Fixture | Camera | Format | Resolution | Decode | GPU preview | Parity | TIFF16 |
|---|---|---|---:|---|---|---|---|
| tnan8886-b7d358517db0cd5b | Canon EOS R6 | CR3 | 20.17 MP | PASS | GPU, no fallback | Proxy PASS; full PARTIAL (max 3.69e-2) | PARTIAL |
| dscf0347-52d054ed9a90306d | Fuji X-T5 | RAF | 40.19 MP | PASS | GPU, no fallback | Proxy PASS; full PARTIAL (max 3.90e-2) | PARTIAL |
| dscf0370-7d82d011ed47cc88 | Fuji X-T5 | RAF | 40.19 MP | PASS | GPU, no fallback | Proxy PASS; full PARTIAL (max 4.70e-2) | PARTIAL |
| lman1714-ae1414f77b8a18bb | Fuji GFX100S | RAF | 102.07 MP | PASS | GPU, no fallback | Proxy PASS; full PARTIAL (max 3.61e-2) | PARTIAL |

Threshold retained: mean <= 1e-5, P95 <= 3e-5, P99 <= 5e-5, max <= 1e-4. Partial rows remain partial.

## Interpretation

Real 40MP and 102MP RAW files reached the GPU pixel executor and TIFF16 writer. The pre-fix run showed rare maximum-pixel divergence on one X-T5 and GFX100S. After the shared protection-boundary fix, all four fixtures pass the unchanged parity gate. The final runner predecodes all four selected candidates and chooses a distinct reference per target. All four rows reached Match V4; all four fixtures pass the unchanged parity gate. This is guarded beta evidence and does not close production Match v4, prove all Canon/Fuji support, or claim WPF visual parity. Native pointer walkthrough is NOT RUN under the current environment. OOM/device-loss injection and peak VRAM telemetry remain open.

## Reproduction

dotnet run --project tools/HomeMatchV4RealRawAcceptance/HomeMatchV4RealRawAcceptance.csproj -c Release -- --root <external-corpus-root> --output <external-output>


## Full-resolution correction

The later full-resolution rerun found maxima of 0.0369 (Canon), 0.0390 and 0.0470 (X-T5), and 0.0361 (GFX100S). These exceed the unchanged 1e-4 max gate, so the acceptance is PARTIAL until the remaining boundary divergence is fixed.
