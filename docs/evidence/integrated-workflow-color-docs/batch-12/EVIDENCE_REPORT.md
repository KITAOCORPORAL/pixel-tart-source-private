# Evidence conclusions and unclosed gates — 2026-10-13 Europe/Berlin

## Public data supports these bounded conclusions

- Both official original X-Rite Classic files parse as24Lab patches; separate pre/post-November2014
  identities, hash/version conflict and missing-whitepoint/redistribution refusal checks were executed.
  Neither source declares illuminant/whitepoint/observer, so NO actual X-Rite Lab-to-RGB accuracy
  claim is made. Original bytes stay in the local ignored research cache, not Git or Release.
- Synthetic explicit D50/2-degree, D65/2-degree, grey black/toe/mid/white, saturation and gamut tests
  exercise CIELAB/XYZ/Bradford. They are mathematical invariants, not sensor/printer measurements.
  A10-degree observer is refused at the RGB comparison boundary; Bradford is not observer conversion.
- Public native sample00_0010.CR2 SHA256:
  cd7492209f002050f24e4ae4c2365f8702145d917f4979e2456aeddb33aebc6f.
  Canon EOS550D decoded via PV4 to5202x3464 linear LibRaw ProPhoto-D65 master. Native/1:1HQ and
 16x16region pixels match exactly; source hash unchanged; rendered output finite bounded sRGB.
  PUBLIC_RAW_RESULT.json records matrix, gamma1/1, metadata WB, black policy and clipping counters.
- LibRaw on this RAW reports black2049 plus channel offsets[0,0,1,1]. The EXIF-based evaluation of
  the dataset PNG uses[2041,2050,2059], white14582 (green is explicit mean of two greens). These are
  DIFFERENT policies/representations, not silently asserted equivalent. Independent measured black
  truth is unavailable. PNG retains sensor black while production LibRaw already subtracts it.
- Published SimpleCube++16-bit PNG00_0010 (648x432 camera RGB, downscaled/target-cropped) and
  published mean illuminant,415valid sparse samples: gray-world angular error0.5123951155degrees;
  known-GT-balanced scene-grey residual0.9107890202degrees. Python independent PNG unfilter/CRC
  and metrics agree with C# operator tests. These are one-scene baseline metrics, not calibration.
  Algebraic neutralization of the GT against itself is a separate synthetic invariant, not independent
  scene proof. Native RAW probe uses camera metadata WB, NOT published GT WB.
-72serialized/reopened synthetic PV4/5/6 x EXIF1-8 x filmoff/V1/V2 output hashes match an independently
  compiled source archive of base9e0fc52b0fd3598bae36ad1ccb5db2b1520ad0d8. Golden JSON SHA256:
  07150c76014bc8bef5e41dbf560c20c51e1b970db90c2cffe83716b561925945.
  This proves covered recipes only, not an exhaustive authorized customer-project corpus.
- No historical renderer/decoder/film/UI source was modified. New validation math stays separate.

## Still unproven / unclosed

1. X-Rite commercial-use/redistribution consent and source-specific whitepoint/observer authority.
   No X-Rite original or derived reference values in source/Release/report packages. No default D50.
2. Broad RAW dataset accuracy, multiple cameras/light/scene classes, measured black and independent
   GT-based native-demosaic comparison. One high-ISO inherited Cube+ sample in Cube++; orientation1
   real, other orientations synthetic. No universal output/colorimetric accuracy claimed.
3. A7M4-specific camera characterization, spectral response, scene HDR and general ICC accuracy.
4. Genuine paired digital RAW + I-2 physical print + separately characterized scanner. No physical
   pair fabricated; no calibrated modern i-Type/600 exact LUT generated or advertised. T-79 unused.
5. Current Sony-file native decoder refusal persists unchanged. Canon success is NOT Sony support.
6. Paired importer metadata/hash QC is not ICC/pixel/registration/blur/saturation/light/authenticity QC.
   CalibrationEligible always false in this batch; any calibration claim explicitly rejected.
7. WPF full-suite hangs/aborts remain open. Both partial counts are ABORTED, not full passes.
   Focused existing WPF color/legacy contracts pass; details in TEST_SUMMARY.json/TEST_MATRIX.md.
8. Application EXE, native mouse interaction, physical device, film and scanner acceptance NOT_RUN.

Status: scoped public-data/validation foundation delivered; overall PARTIAL for physical calibration
and full desktop/product acceptance. Release publishing does not close unperformed acceptance gates.
