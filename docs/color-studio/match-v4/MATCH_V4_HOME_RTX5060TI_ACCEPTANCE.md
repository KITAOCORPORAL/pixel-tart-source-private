# Match V4 Home RTX 5060 Ti Acceptance

## Scope

External real files only; RAW/TIFF binaries remain outside Git. Match v3 remains Stable/default and Match v4 remains guarded Beta.

## Hardware

- Adapter: NVIDIA GeForce RTX 5060 Ti
- Dedicated VRAM: 16,829,644,800 bytes (adapter capability value; peak dedicated VRAM telemetry is UNKNOWN)
- Backend: ComputeSharp-DX12 / DirectX 12
- Product route: `RAW -> ProfessionalDecode RGB48 -> FrozenRawMaster -> Match V4 analysis -> CPU canonical protection decision keys -> DX12 tiled pixel execution -> TIFF16`

## Acceptance matrix

| Fixture | Camera / format | Decode | Proxy parity | Full-res parity | TIFF16 export | TIFF16 pixel parity |
|---|---|---|---|---|---|---|
| tnan8886-b7d358517db0cd5b | Canon EOS R6 / CR3 | PASS | PASS | PASS, max `2.00e-6` | TIFF16_EXPORT_COMPLETED | TIFF16_PARITY_PASS, max code delta 1 |
| dscf0347-52d054ed9a90306d | Fujifilm X-T5 / RAF | PASS | PASS | PASS, max `1.31e-6` | TIFF16_EXPORT_COMPLETED | TIFF16_PARITY_PASS, max code delta 1 |
| dscf0370-7d82d011ed47cc88 | Fujifilm X-T5 / RAF | PASS | PASS | PASS, max `1.19e-6` | TIFF16_EXPORT_COMPLETED | TIFF16_PARITY_PASS, max code delta 1 |
| lman1714-ae1414f77b8a18bb | Fujifilm GFX100S / RAF | PASS | PASS | PASS, max `1.74e-6` | TIFF16_EXPORT_COMPLETED | TIFF16_PARITY_PASS, max code delta 1 |

Full-res gate: mean `<=1e-5`, P95 `<=3e-5`, P99 `<=5e-5`, max `<=1e-4`. Threshold unchanged. Each fixture was repeated three times with stable output hashes and metrics. All runs used GPU with no CPU fallback.

## TIFF identity

CPU and GPU TIFF exports use the same source SHA256, reference identity, transform hash, decode generation, and processing generation. TIFF readback confirms RGB, 16 bits per sample, and max channel delta of one code value.

## Memory and recovery

The 102MP GFX100S run completed at 20.96 GB process working set with tile execution. Dedicated VRAM peak telemetry is UNKNOWN. Deterministic budget descriptors remain LOW/STANDARD/HIGH/ULTRA; real 4GB/8GB hardware and real device loss are NOT RUN. Injected execution failure, allocation/OOM mapping, cancellation, and CPU fallback contracts are covered separately.

Status: **FULL_RES_PARITY_CLOSED for the four listed fixtures; production-wide Match V4 remains guarded Beta.**
