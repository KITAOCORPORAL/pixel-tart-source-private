# Frozen RAW master acceptance

| Contract | Result |
|---|---|
| Professional decode is required | PASS in product pipeline contract; RGB24 fallback rejected |
| One master shared by preview, Match and export | PASS in focused Core/WPF tests |
| Source SHA recorded and checked before export | PASS in focused Core test |
| Decode generation identity | PASS; `FrozenRawMaster.DecodeGenerationId` |
| Processing generation identity | PASS; `FrozenRawMaster.ProcessingGenerationId` |
| Preview/export uses the same high-precision source | PASS for the product session path |
| Candidate LibRaw Sony/X-Trans compatibility | BLOCKED — no side-by-side candidate binary |
| Five-fixture 20-run candidate matrix | NOT RUN — candidate unavailable |

This closes the product-session safety issue without claiming that the native X-Trans decoder is
deterministic or that Sony A7 IV is supported. The next native decision still requires a pinned
candidate and a complete five-camera rerun.
