# Company evidence handoff

Product code remains ac1c57e9689428cea3f6938e06ad26209c7fc3d5. Read docs/validation/company-precision-handoff-20261123/HANDOFF.md. Existing LegacyExportMemory20261122 host/guard/stage tools were reused without edits. verify_company.py only reads explicitly named company artifacts and bounded TIFF blocks; it does not invoke a decoder, renderer, host, or RAW file. The existing probe_tiff.py dependency is source-bound in the handoff.

To independently compare preserved local outputs, use a new JSON destination, not the sealed receipt:

```powershell
python -X utf8 tools/PrecisionProductHandoff20261123/verify_company.py --root E:/Kitao-Deliveries/precision-product-handoff-20261123 --output E:/Kitao-Deliveries/precision-product-handoff-20261123/new-comparison.json
```

The comparison creates a probe directory named after the new output JSON stem and refuses existing output paths. Each independent read-only comparison uses a fresh output filename; payload remains the original read-only hash-checked file. Do not rerun resource experiments as part of a documentation review. The failed same-process reopen is not upgraded by the separate first-only display observation. No product/decode DLL is installed or replaced.
