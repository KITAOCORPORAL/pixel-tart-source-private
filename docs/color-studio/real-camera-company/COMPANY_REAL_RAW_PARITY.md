# Real-camera preview versus full-resolution TIFF16

Same frozen Match v3 state is used in the ViewModel preview/export commands. Comparison converts 8-bit WPF display and 16-bit TIFF read-back through sRGB→OKLab, using center-nearest coordinate mapping from the 1600-edge preview to the full-resolution source. No threshold changes: mean `.003`, P95 and max `.012`.

| Fixture | Mean | P95 | Max | Result | RAW-only proxy max, without Match |
|---|---:|---:|---:|---|---:|
| X-T5 company-02 | .001531 | .004183 | .048709 | PARTIAL | .051365 |
| X-T5 company-03 | .001215 | .002082 | .053092 | PARTIAL | .043566 |
| GFX100S company-04 | .001062 | .001882 | .004796 | PASS | NOT MEASURED |
| EOS R6 company-05 | .001361 | .003145 | .050713 | PARTIAL | .045667 |
| Sony company-01 | NOT RUN | NOT RUN | NOT RUN | DECODE_BLOCKED | NOT RUN |

The tail already exceeds the limit without Match on X-T5 and Canon; a real-frame 1600-pixel spatial proxy versus nearest full-resolution samples is implicated. The staged same-display diagnostic then reduced the four decoded maxima to `.010271`, `.008307`, `.007007`, `.011149` (zero canonical outliers), proving the legacy comparison was not equivalent. This **does not yet prove** that the product cross-decode route is fixed: X-T5 #1's product run retained canonical max `.033891` because native RAF decode is non-deterministic. Repeated decode proxy was byte-identical for Canon and not for the two X-T5 files in the diagnostic run; this observation is now reproduced with full RGB48 hashes. Directly shifting nearest-sample coordinates made errors much worse, not better. No threshold relaxation and no speculative decoder fix.

Resolution-dependent transform drift was measured separately by applying proxy-analysis and full-analysis Match v3 transforms to identical full-resolution source pixels (17,956 sampled coordinates each):

| Fixture | Mean | P95 | Max |
|---|---:|---:|---:|
| X-T5 company-02 | .000001 | .000002 | .000002 |
| X-T5 company-03 | .000005 | .000007 | .000011 |
| GFX100S company-04 | .000035 | .000030 | .000969 |
| EOS R6 company-05 | .000011 | .000004 | .001035 |

These sampled transform distances do not explain .05-scale parity maxima. The comparison is an OKLab distance, not a calibrated perceptual ΔE claim. CFA/X-Trans, native compression and camera profile behavior are unproven; visual color/demosaic sign-off remains manual. Canon, X-T5 and GFX100S are **independent-fixture retests** of the home corpus camera models: Canon/X-T5 still fail parity; GFX100S differs (one passing company fixture), so neither the home finding nor support coverage is superseded.
