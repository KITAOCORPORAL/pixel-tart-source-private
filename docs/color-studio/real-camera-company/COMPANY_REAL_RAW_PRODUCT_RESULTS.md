# Company real-camera product results

First-run product source: `ba58558682d5dcfa570017f338ed98ce9b3a9248`. Private source RAW, TIFF16, previews, full diagnostics and test gate outputs remain external to Git. The committed SHA256-only fixture manifest identifies five originals without distributing them.

| Camera / fixture | Decode | Precision | Match v3 | ViewModel import/preview/export | TIFF16 read-back | Parity | Full pipeline |
|---|---|---|---|---|---|---|---|
| Sony ILCE-7M4 / company-01 | FAIL (LibRaw open) | NOT RUN | NOT RUN | NOT RUN | NOT RUN | NOT RUN | DECODE_BLOCKED |
| Fujifilm X-T5 / company-02 | PASS | PASS | PASS | PASS | PASS | PARTIAL | PARTIAL |
| Fujifilm X-T5 / company-03 | PASS | PASS | PASS | PASS | PASS | PARTIAL | PARTIAL |
| Fujifilm GFX100S / company-04 | PASS | PASS | PASS | PASS | PASS | PASS | REAL_CAMERA_PRODUCT_PASS |
| Canon EOS R6 / company-05 | PASS | PASS | PASS | PASS | PASS | PARTIAL | PARTIAL |

Count: 5 discovered, 4 professional decode PASS, 1 decode blocked, 4 precision/Match/product export/read-back PASS, 1 full-pipeline PASS, 3 parity PARTIAL. This establishes **one independent real-camera fixture** product pass, not a Fujifilm RAF or GFX model-wide certification. The four product-route runs used real `ReferenceColorWorkspaceViewModel` commands and a distinct reference, not only a Core runner. Source representation: decoder RGB48, float32 sRGB working samples, RGB24 proxy for display/analysis, float32 Match output, full-resolution float32 export input and RGB48 TIFF read-back. The writer validates atomically. Source RAW files were only read.

The RAW Make/Model/CaptureTime values are available in decoder metadata; orientation tags 1/8/8/1 were retained. TIFF16 reader verifies dimensions/16-bit RGB/orientation/ICC payload, **not** all EXIF. ICC bytes were 0 for every exported TIFF; no ICC conversion claim. Make/Model/CaptureTime propagation into the TIFF itself remains NOT VERIFIED / not closed. Sony source orientation metadata describes a 270° rotation but has no decoded TIFF for comparison. Native bit depth, compression, CFA, black/white levels and color matrices are UNKNOWN unless independently decoded from RAW-specific structures; generic image metadata `JPEG` tag is insufficient.

The opt-in `CompanyRealRawProductAcceptanceGate` requires two explicit environment variables: `PIXEL_TART_COMPANY_RAW_ROOT` and an **external** `PIXEL_TART_COMPANY_RAW_EVIDENCE_ROOT`. With no private fixtures in ordinary CI it reports NOT RUN (MSTest Inconclusive); with these five fixtures it intentionally fails until Sony decode and three parity cases are closed. It writes only external output and never stages binary samples. The baseline above is kept independent of later gate runs.
