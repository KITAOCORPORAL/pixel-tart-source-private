# Real RAW spatial sampling analysis

Source for all measurements: product baseline `3dab1b4f75dd696ba64ecfb431a28ce2fd933673`; five external RAW files identified in [the manifest](COMPANY_RAW_FIXTURE_MANIFEST.json). The immutable first-run values in [COMPANY_REAL_RAW_BASELINE.md](COMPANY_REAL_RAW_BASELINE.md) are unchanged.

## Observed

The product path creates a 1600-edge proxy with a center-nearest source sample:

```text
width  = round(sourceWidth  * 1600 / maxEdge)
height = round(sourceHeight * 1600 / maxEdge)
sourceX = floor((previewX + 0.5) * sourceWidth / previewWidth)
sourceY = floor((previewY + 0.5) * sourceHeight / previewHeight)
```

The proxy then goes through `HighBitDepthImageBuffer.ToVisualRgb24()` (`round(float * 255)`) and WPF `BitmapSource` display. Export re-decodes the RAW at full resolution and writes float output as RGB48. Orientation is retained as TIFF tag 1 or 8; the product currently does not rotate the decoded buffer before proxying.

Stage diagnostics used the same frozen Match v3 state and recorded legacy nearest-source comparison, same-resolution comparison after applying the same RGB24 display encoding, full-resolution TIFF precision, top outlier coordinates, local gradient and boundary flag. QA heatmaps, proxy-alignment images and top-100 JSON are external ignored output only.

| Fixture | Legacy mean/P95/max | Canonical same-display mean/P95/max | Legacy >.012 | Canonical >.012 |
|---|---:|---:|---:|---:|
| X-T5 company-02 | .001570 / .004176 / .048300 | .000002 / .000000 / .010271 | 2,582 (0.1508%) | 0 |
| X-T5 company-03 | .001250 / .002220 / .036945 | .000009 / .000000 / .008307 | 728 (0.0425%) | 0 |
| GFX100S company-04 | .001062 / .001882 / .004804 | .000055 / .000000 / .007007 | 0 | 0 |
| EOS R6 company-05 | .001357 / .003148 / .050767 | .000017 / .000000 / .011149 | 1,155 (0.0676%) | 0 |

The canonical result is not a threshold relaxation: both sides are the same-resolution display representation, while full-resolution float/TIFF precision remains separately checked. It shows the original `.05` tail is dominated by comparing 8-bit display RGB to 16-bit full-resolution values under a non-equivalent spatial/display contract. The product acceptance gate now keeps both `Legacy` and `Canonical`; it closes a fixture only on canonical limits and TIFF16/read-back conditions.

## Proven

* Same-buffer ProfessionalDecode → proxy sampling has zero observed float difference for the tested source sample mapping.
* Canon repeated decode has zero changed pixels and zero maximum U16 difference in a controlled pair; X-T5 repeated decode is not deterministic. GFX same-buffer pair was also zero-difference in the controlled pair.
* Applying proxy-analysis versus full-analysis Match transforms to identical full-resolution samples showed small drift and cannot explain the old `.05` tail.
* Canonical same-display comparison passes for all four decoded fixtures in the diagnostic runner. The product command gate independently produced canonical PASS for X-T5 #2, GFX100S and EOS R6; X-T5 #1 remained PARTIAL with max `.033891` in its cross-decode product run.

## Not proven / remaining risk

The X-T5 cross-decode failure is not yet isolated to one native cause. Two independent LibRaw decodes of the same RAF produced distinct full-buffer SHA256 values: company-02 had 6,705/40,186,368 pixels differ, max U16 delta 5,767; company-03 had 6,199 differing pixels, max U16 delta 5,930. The differences are sparse and spatially irregular in the sampled list. Same-instance and new-instance runs were both non-deterministic, so this is not explained by managed `LibRawDecoder` instance reuse alone. It may be native decoder state, thread/native lifecycle or LibRaw/RAF processing behavior; do not call it a proven X-Trans defect.

No orientation failure was observed in the current numbers: orientation 8 was retained for X-T5 #2 and GFX100S and their canonical diagnostics passed. Full visual sign-off and a true resampling implementation (bilinear/bicubic versus nearest) remain separate work. `Canonical` means same display encoding plus the existing center-nearest proxy contract; it is not a claim that the proxy is a high-quality resampler.
