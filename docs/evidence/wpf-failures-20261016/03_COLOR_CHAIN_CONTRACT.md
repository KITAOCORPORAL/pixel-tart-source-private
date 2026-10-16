# RAW→working→edit→output→display domains (no newproductionpixelalgorithm)

## Existing paths must not be conflated
- Product legacy LibRaw0.21.1 ProfessionalDecode: finalRGB16 sRGB primaries withdecoder gamma/WB/autobrightness/matrix already baked; notsensorlinear. Black-before/WB-before/matrix-beforepixelsUNAVAILABLE. FromRaw normalizesushort/65535 into nonlinear-sRGBfloat. Existing RawMatchTiff16ProductPipeline requiresSourceBitDepth16+metadata sRGB. PV4/5/6 historicalrendersemanticsunchanged.
- Other explicit professional/linearresearch paths have ownversion/contracts; a methodnamedProfessional isnot proofallpixelsRAWlinear. Nativecandidate0.22.2 andsonycolor4 remainindependentresearch, notproductdecoder.
- Controlled ICCV1 limitedphotoTIFF/JPEGdecodedRGB24/48 usesWICIgnoreColorProfile andexactembeddedlinearprofileSHA; convertsviaLCMSrelative1/BPCoff/adaptation1 tolinearProPhotoPCSadaptedD50; frozen-1EV; converts tolinearsRGBmatrixPCS D50; allinput/intermediate/outputRGBmust0..1, otherwiseoriginalstableDOMAINrefusal. Inputsourceisrenderedphotoderivative, neverrawsensor. DisplayseparateIECOETFandstandardencodedprofile; TIFF16/PNG16linearICC, JPEGdisplayencoded8bitquantization/lossy separately. ProfilematrixprimariesD65butadaptedD50PCS, notunconditionalBradfordtwice.
- ResearchExtendedLinearClipSinkV2 keepsfiniteextendedfloatthroughworking/edit/output, exportsbefore-mapfloatTIFFthenEXPLICITnamedlossysinkclip. NoV1tolerancechange, no ordinaryentry, no automaticrecipeupdate. Hardclipnotperceptualmapping.

## Save/output
Recipepinsalgorithm/sourcehash/input/work/output/displayICC/intent/BPC/adaptation/EV/mappingstage/quantization. Samefrozenrenderusedforpreview,reopen,export. Extendedfloatsmustnotbegammaencodedaslinear; screenproxyOETFnotphysicalmonitorcalibration. Integerexportsquantizeonlyafterexplicitmapping. Missing/unsupported/invalidICC faildiagnostically, neverimplicitcoloraccuracycorrected.

Thisbatchpixelalgorithmunchanged. Scientificcomparisonbelow distinguishesmatrixfloatroundofffromrealoutgamut, withoutglobalclamp/guardband/EVcompression. Referenceversions/licenses/parametersfixedLCMS2.16MIT453baf.../skcmsBSD397adc...; RawTherapeeGPLresearchandLibRaw0.21.1/0.22.2sourceidentitiesinheritedperfile, no wholebranchmerge. CalibrationEligible=false.
