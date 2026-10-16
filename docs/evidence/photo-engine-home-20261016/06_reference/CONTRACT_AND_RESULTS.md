# Stage6 ICC and tone-reproduction reference result

Home actual reference engines: existing product dependency LCMS2.12(native SHA87bf94...); Pillow12.3.0's independently compiled LCMS2.19 (module SHA recorded). Do not expand ControlledIccTransform's reviewed LCMS2.16 identity or reuse those as its gate. Profile inputs are repository's known synthetic linear-sRGB and exact current Windows sRGB output bytes; no arbitrary device input claim.

First ideal IEC sRGB formula reference FAILED at max38 codes/65535, while LCMS2.12→LCMS2.19 quantized8 matched. Reason investigated, not threshold relaxed: Windows outputprofile uses quantizedPCS matrices and1024-point tableTRC, not exact ideal piecewise OETF. Independent exact-profile matrix inverse + table-TRC inverse reference yields max6codes (limit12 unchanged); twoLCMS builds 8bit max0 (limit1). Inputs/outputs/flags/hashes in original FAILED and corrected PASSED JSON; no lost result. PCS colorants alreadyD50, chad is not applied twice. 8bit comparison precision is explicit, not proof of 16bit identity.

WIC ColorConvertedBitmap RGB48 destination returned NotSupported/0x88982F80 on this host; observed JSON records UN_SUPPORTED not PASS. Does not justify lowering product precision or claiming WIC arbitraryICC. Known numeric reference only; no product profile allowlist changes.

Production supports legacy WIC ingress as existing behavior and exact sRGB output/profile. Research PhotoSrgbLinear1 admits only two fixed encodedsRGB hashes, linear working hash and explicit outputSink; not arbitraryICC. Missing/corrupt/unknown profile refusals remain in research; production missingICC assumption is separately flagged OPEN.

ACES1SDR/OCIO2.4.2, OpenDRTv1.1Standard2.2 and darktablelog/spline substage historical outputs are NOT equivalent settings/encoding; full darktable not proved. No new real-photo blind review, no curve replacement. The number of graylevels is not a quality ranking. PV4/PV5/PV6 golden and output transforms remain unchanged. No chart purchase required; camera absolute colour accuracy remains unproved.
