# LibRaw real-camera matrix

The committed product evidence remains the current `0.21.1-Release` baseline. The new
`tools/LibRawCompatibilityProbe` can generate an external JSON matrix with 20 serial runs using
one decoder and 20 runs with a new decoder per run. It intentionally reads the five private RAW
fixtures from `PIXEL_TART_COMPANY_RAW_ROOT`; it never tracks the binary files.

| Fixture | Current 0.21.1 baseline | Candidate |
|---|---|---|
| Sony ILCE-7M4 ARW | `FileUnsupported` / ProfessionalDecode blocked | NOT AVAILABLE |
| Fuji X-T5 RAF #1 | ProfessionalDecode succeeds; 10/10 hashes distinct in both strategies | NOT AVAILABLE |
| Fuji X-T5 RAF #2 | ProfessionalDecode succeeds; 10/10 hashes distinct in both strategies | NOT AVAILABLE |
| Fuji GFX100S RAF | ProfessionalDecode succeeds; controlled repeated decode stable | NOT AVAILABLE |
| Canon EOS R6 CR3 | ProfessionalDecode succeeds; controlled repeated decode stable | NOT AVAILABLE |

`NOT AVAILABLE` is not a pass or fail. It means there is no isolated candidate binary to run.
The Sony capability-list entry is not treated as a decode result: the actual file is rejected by
the current native library on both its original and ASCII-only path.
