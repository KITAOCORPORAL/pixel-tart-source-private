# Company real RAW first run — immutable baseline

Product source `ba58558682d5dcfa570017f338ed98ce9b3a9248` (before any code changes); Windows x64, .NET 10.0.401. Five originals discovered outside Git, not four. Private full JSON, decoded TIFF16 and PNG previews remain in the external QA output directory and are **not distributed**. The repository manifest contains only original-byte SHA256 and extracted metadata. Distinct real-camera references: Canon EOS R6 (`B7D358…33401`) for both X-T5 and GFX100S; X-T5 `DSCF0347.RAF` (`52D054…4E865`) for Canon; never target == reference. Match strength 72, tone 60, color 70.

| ID | Professional decode | RGB48 → float32 | Product target / Match preview / TIFF16 export / read-back | OKLab mean / P95 / max | Result |
|---|---|---|---|---|---|
| company-01 Sony ILCE-7M4 ARW | FAIL: LibRaw `Failed opening file` | NOT RUN | NOT RUN | NOT RUN | DECODE_BLOCKED |
| company-02 Fujifilm X-T5 RAF | PASS 7752×5184 | PASS | PASS / PASS / PASS / PASS | .001531 / .004183 / .048709 | PARTIAL |
| company-03 Fujifilm X-T5 RAF | PASS 7752×5184 | PASS | PASS / PASS / PASS / PASS | .001215 / .002082 / .053092 | PARTIAL |
| company-04 Fujifilm GFX100S RAF | PASS 11662×8752 | PASS | PASS / PASS / PASS / PASS | .001062 / .001882 / .004796 | REAL_CAMERA_PRODUCT_PASS (this fixture only) |
| company-05 Canon EOS R6 CR3 | PASS 5496×3670 | PASS | PASS / PASS / PASS / PASS | .001361 / .003145 / .050713 | PARTIAL |

Parity threshold unchanged from the existing product WPF contract: mean ≤ .003, P95 ≤ .012, max ≤ .012. First run used the real `ReferenceColorWorkspaceViewModel.LoadTargetAsync`, target snapshot, `ActivateTargetCommand`, `ExportSelectedCommand`, default `LibRawDecoder` and TIFF16 read-back. Private runner additionally recorded RAW-only diagnostic previews; those do not substitute for UI visual verification. Four decoded fixtures used true 16-bit/channel RGB input, `HighBitDepthImageBuffer` float32 normalized sRGB, 1600-edge preview proxy, full-resolution professional re-decode/export and 16/16/16 RGB TIFF. No 16→8→16 export bottleneck; RGB24 is analysis/display only. Orientation tags: 1, 8, 8, 1 respectively and retained in TIFF; ICC bytes 0 and comprehensive EXIF propagation NOT VERIFIED. No claim of camera-wide support or real visual UI responsiveness/cancellation based on a headless ViewModel run.

No fix was made before capturing this baseline. A separate opt-in test is added afterward and cannot overwrite these first-run numbers. Source files were never modified.
