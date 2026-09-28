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
| tnan8886-b7d358517db0cd5b | Canon EOS R6 | CR3 | 20.17 MP | PASS | GPU, no fallback | PASS (mean 9.31e-8, p95 2.98e-7, max 1.44e-6) | PASS |
| dscf0347-52d054ed9a90306d | Fuji X-T5 | RAF | 40.19 MP | PASS | GPU, no fallback | PARTIAL (mean 8.46e-8, p95 1.79e-7, max 3.33e-2) | PASS |
| dscf0370-7d82d011ed47cc88 | Fuji X-T5 | RAF | 40.19 MP | PASS | GPU, no fallback | PASS (mean 6.76e-8, p95 2.09e-7, max 1.07e-6) | PASS |
| lman1714-ae1414f77b8a18bb | Fuji GFX100S | RAF | 102.07 MP | PASS | GPU, no fallback | PARTIAL (mean 1.24e-7, p95 3.58e-7, max 3.85e-2) | PASS |

Threshold retained: mean <= 1e-5, P95 <= 3e-5, P99 <= 5e-5, max <= 1e-4. Partial rows remain partial.

## Interpretation

Real 40MP and 102MP RAW files reached the GPU pixel executor and TIFF16 writer. One X-T5 sample passed CPU/GPU parity; another X-T5 and GFX100S show rare maximum-pixel divergence while aggregate errors remain small. The final runner predecodes all four selected candidates and chooses a distinct reference per target. All four rows reached Match V4; the Canon EOS R6 and one X-T5 sample passed, while the other X-T5 and GFX100S remain parity PARTIAL. This is guarded beta evidence and does not close production Match v4, prove all Canon/Fuji support, or claim WPF visual parity. Native pointer walkthrough is NOT RUN under the current environment. OOM/device-loss injection and peak VRAM telemetry remain open.

## Reproduction

dotnet run --project tools/HomeMatchV4RealRawAcceptance/HomeMatchV4RealRawAcceptance.csproj -c Release -- --root <external-corpus-root> --output <external-output>
