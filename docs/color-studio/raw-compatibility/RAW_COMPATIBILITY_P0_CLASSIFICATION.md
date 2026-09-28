# RAW Compatibility P0 Classification

This classification uses the 387-file corpus result. It records decoder outcomes without treating extension presence as support.

| Category | Count | Evidence |
|---|---:|---|
| ProfessionalDecode PASS | 269 | LibRaw returned RGB48 and the high precision buffer accepted the result. |
| Decoder FAIL | 10 | Decoder or metadata execution failed and requires fixture-specific review. |
| UNSUPPORTED | 70 | Current LibRaw route reported unsupported format/camera or equivalent unsupported path. |
| Duplicate fixture | 1 | SHA256 duplicate excluded from independent counts. |

The unsupported/failed set includes legacy CRW, DCR/KDC, MDC, MEF/MOS/MRW, RAW, SR2/SRF and X3F/Foveon cases in the current corpus. Sony ARW rows that decode remain separate from any Sony A7 IV modernization claim. No LibRaw upgrade is included in this closure.

Priority: preserve fixture IDs and classify decoder version limitation, legacy format limitation, corrupt file, configuration, product adapter, or unknown cause before changing decoder code.
