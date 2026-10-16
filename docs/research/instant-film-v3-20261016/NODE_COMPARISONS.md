# Instant V3 candidate admission — OPEN / NOT APPROVED FOR PRODUCTION

Date: 2026-10-16 Asia/Shanghai. Base fe73d02cf85df518c4eaebf4a09f9a5157451eab.
Read before implementation: AGENTS.md and docs/color-science/open-source-first/POLICY.md.

## Scope decision

Missing mature-reference execution cannot be waived by this task. V3 pixel code belongs to a separate **tools-only research assembly**, not the product dependency graph. Existing eight profiles, V1/V2 recipes and PV4/5/6 math stay unchanged. No production UI default/entry is added. Creative output is NOT calibrated output. This is a runnable research candidate, not stage-2/3/4 production approval.

## Common domain contract

Input: already WB/matrix/EXIF-resolved display-referred sRGB D65 floats. Decode sRGB once to linear sRGB. No camera WB, chromatic adaptation, sensor black subtraction or extra exposure from the RAW recipe. Tone exposure here is explicitly a creative print exposure, not capture exposure. Reject non-finite values; finite negative/>1 values clamp at the declared display-referred boundary. Output: sRGB D65 [0,1] floats, one final OETF; RGB16 round-to-nearest at existing export boundary. No alpha. Existing product pipeline resolves orientation before tool candidates; frozen candidate RGB master has EXIF 1. Frame never supplies white calibration.

## Tone / apparent density / saturated colors — InstantCreativeResponseV3

Chain: oriented product display input -> linear EOTF -> creative exposure, black/toe/shoulder -> luminance-conditioned color relationship and chroma compression -> spatial optics -> emulsion -> final OETF -> paper/scan. No precision physical concentration claim. i-Type/600 share the exact modern response family; SX70 remains a creative variation; Blue and Duochrome are different palette mappings.

Reference 1: darktable release-4.8.1, fb0eb997e62c6dd52966cd35911c74afa150e209, src/iop/filmicrgb.c, process/filmic spline and gamut sections; src/iop/exposure.c process. GPL-3 reference only, NOT copied/translated into product. Numerical execution **NOT_RUN** (no admitted executable). Parameters/domain intentionally not interchangeable with this creative display-domain response.
Reference 2: RawTherapee 5.11, db575c6690d30ce6428c0e1383bec8b8f1fb8d21 (repository pinned index; release label must be rechecked), rtengine/curves.cc and rtengine/curves.h tone curve classes / curve evaluation. GPL-3 reference only, NOT copied. Numerical execution **NOT_RUN**. File/entry verification pending; do not promote a project-name list to approval.
Decision: independent research math; not physical-film inversion or arbitrary web-patch fitting. Reference source/binary hashes and individual file license review remain pending.

## Optics — InstantFiniteOpticsV3

Linear-domain finite-support normalized separable binomial [1,4,6,4,1]/16 kernel, radius 2 source pixels, clamp-to-edge, two-pixel halo, detail mix and thresholded highlight-only diffusion. Bounded tile + halo scratch accounted with StudioGlobalResources. No full-frame scratch. Read all samples by global source coordinates; local/tile/full repeat identical arithmetic. Fixed two-pixel kernel is a research budget choice, not a measured lens PSF.
References: darktable same pinned commit src/common/gaussian.c / dt_gaussian_blur (Gaussian implementation) and RawTherapee same commit rtengine/gauss.h / gaussHorizontal, gaussVertical (entry verification pending). GPL implementations are references only. Both executed component comparisons NOT_RUN. Kernel is intentionally finite, not claimed equal to recursive Gaussian references. Impulse/edge behavior, transfer-domain and reference errors must be measured before admission.

## Emulsion / frame / scan — InstantMaterialV3

Deterministic keyed value noise at multiple fixed normalized-coordinate scales. Shared and per-channel substreams; normal microstructure, development defects and aged effects separate with restrained defaults. Same root seed and full-canvas coordinates across tiles, crop, preview and reopen. No system random or trigonometric periodic base texture. Paper base/material/scratches and scan dust separate controls; original procedural material only; no reference photo is embedded in assembly/release.
References: darktable src/iop/grain.c and RawTherapee film-grain / noise entry points need file-level verification at pinned commits. **NOT_RUN**, not license-approved for reuse. Does not meet the two-executable comparison gate. No measured chemical/scan interpretation inferred from procedural noise.

## Test matrix / acceptance

Synthetic: neutrals, primaries, highlights, negative/>1, non-finite rejection, float/RGB16; all 8 EXIF orientations via existing product baseline tests; candidate tile/region/full, seed, reopen, cancellation/resource release. Authorized RAW: separate Sony DSC08015 from DSC09916, Canon TNAN8886, Fuji DSCF0347 frozen decoded inputs only (native Fuji instability is independent). ICC: product sRGB output verification, not arbitrary ICC-reader approval. Real reference execution, cross-ICC mature comparisons, source hash/config/per-stage numerical/time/memory matrix remain required.

PV4/5/6, V1/V2 golden policy: no production pixel source edited. Candidate tool recipes use an independent algorithm version; no global processing-version increment. Candidate previews/export are frozen rendered masters through the existing product encoder, not evidence that UI recipe save-reopen is integrated. Full candidate product integration remains BLOCKED_OPEN_REFERENCE_GATE. P3 performance / Fuji repeatability remain independent release blockers.
