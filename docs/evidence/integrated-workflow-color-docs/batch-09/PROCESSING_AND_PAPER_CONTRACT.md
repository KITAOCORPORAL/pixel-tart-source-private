# Processing and paper contract

V1 remains readable with its original formula; V2 fields are omitted from old JSON at false/null defaults. V1 rejects V2 controls rather than silently migrating. New generation explicitly creates AlgorithmVersion=2.

Amount is base-color blend, not total intensity. Defects, texture, temperature and random variation remain independent. V2 ImageEffectsDisabled returns source image colors exactly; PaperFrame remains separate. Enabled=false retains the old full-layer bypass. Geometry still crops/fits when chosen: use LegacyFree plus frame off for whole-frame identity. Identity in cropped output is measured at mapped source coordinates.

PaperGeometry.Version=1 defines aspect, side/top/bottom relative to window width, CropFill/FitEntire and normalized crop position. Default square ratios .075/.075/.25 are original design ratios, not manufacturer measurements. Null geometry retains historical free-ratio expansion. CropFill uses an inscribed window; FitEntire uses a containing window and paper-colored margins. One isotropic scale prevents stretching. Pixel centres map to oriented source, then EXIF1–8 maps to stored coordinates. DPI changes presentation/metadata, not this pixel contract.

OutputSize, RenderOutput, HQ, regions and export rows share InstantPaperLayout. Publishing receives the same full-size TIFF intermediate and definition. Prepared geometry is reused per operation/row. Original dimensions and paper dimensions are displayed separately.

JPEG CurrentEdits captures Look, stack, old film and origin version into InstantInputRecipe and reconstructs through the existing full-resolution export renderer. No preview bitmap is used as export source. Recipe hash is part of DecodeKey. Original is explicit and retains the previous version. Existing professional RAW retains WB/exposure. Legacy RAW editing stacks currently reject CurrentEdits visibly; Original is an explicit alternative, not a silent loss of adjustments. JPEG source precision is still intrinsically 8-bit despite RGB48 processing.

The 2GiB resource ledger is admission accounting, not an OS cap. Export now reserves retained master bytes as well as output work. Cache eviction does not dispose a strong master reference used by active work. Workspace close signals export cancellation; the operation owns token-source disposal.

Publishing resize/watermark verification now compares decoded encoded output against lossless rerender of the recipe. TIFF uses RGB16, PNG RGB8, JPEG MAE<=5/RMSE<=15. This same-renderer integrity check is not an independent algorithm oracle; separate tests provide resize/alpha and fixed-font references.

