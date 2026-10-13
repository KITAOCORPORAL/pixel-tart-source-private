# Batch 12 — source-first scope and versions

Base: `9e0fc52b0fd3598bae36ad1ccb5db2b1520ad0d8`, verified remotely on 2026-10-13 (Europe/Berlin).
Branch: `feature/kitao-studio-batch12-public-color-validation-20261013`.
Independent checkout: `D:/AI AGENT/pixel-tart-batch12-20261013`.

## Actual source read before implementation

- batch-11/COLOR_DOMAINS_AND_VERSIONS.md and TEST_MATRIX.md (not merely the batch12 request).
- ProfessionalRawPipeline.cs / ProfessionalRawDecoder.cs / ProfessionalLibRawAdapter.cs:
  PV4 LibRaw021LinearProPhotoD65V1, metadata black and camera WB, RGB16 linear working output;
  downstream display shoulder/gamut/sRGB. Decoder clipping is already irreversible.
- InstantInputRecipe.cs / LegacyRawInstantInput.cs / RawMatchTiff16ProductPipeline.cs:
  PV5 rendered sRGB identity, PV6 legacy RAW full-precision edits in rendered sRGB identity.
- CandidateColorBoundary.cs: camera-linear needs explicit camera matrix; display-encoded cannot receive RAW WB twice.
- LinearRgbColorScience.cs: existing Bradford and linear-space conversions are candidate math, not a camera profile.
- InstantFilm.cs: profiles already say `CreativeApproximationV1`, `ModernIntegralCreative`, film algorithm1/2;
  no measured i-Type/600 characterization.
- BatchSevenRawQualityTests, BatchTenLegacyRawTests, ProfessionalRawPipelineTests,
  RawImageOrientationTests, LinearRgbColorScienceTests and WPF batch11 contracts; local company RAW
  fixtures were located, but public licensed data is used here instead.

## New validation-only definitions

`ClassicCgatsV1`, `ReferenceLabXyzBradfordV1`, `PublicSensorBlackWbV1`, paired metadata schema1.
New code lives in Core/Services/ColorValidation. No production renderer/decoder/profile/UI changes.
Reference Lab/XYZ and `CameraPatchResponse` have different types. RAW sensor RGB illuminant truth
is not Lab/XYZ reference truth; a known chart reference is not a camera measurement.

No change to PV4/5/6, recipe versions, instantFilm1/2, XAML, app ViewModels or Grok's worktree.
Future pixel-changing corrections require proposed PV7 (or separately versioned input transform),
explicit source/whitepoint/profile IDs, new DecodeKey/cache keys and opt-in migration; PV4/5/6 remain callable.
The proposed identifier is NOT implemented or admitted by historical validation.

## Import behavior

Strict Classic 24-patch subset, exact 4 columns, A1..F4 unique IDs, deterministic physical numbering:
A1..F1=1..6, A2..F2=7..12, A3..F3=13..18, A4..F4=19..24.
Dot or comma decimal is explicitly supported; no thousands/grouping syntax, no NaN/Infinity,
no silent count/version/hash/duplicate/range repair. Original bytes hashed before UTF-8 decoding.
Vendor descriptive preamble is preserved externally, not silently interpreted as measurement conditions.
Unknown white point/observer is retained as null and refuses conversion. Explicit caller conditions are
recorded as evidence, not inferred as manufacturer truth. Lab/XYZ reflective bounds are importer support
limits (not claimed universal physical bounds). Versions coexist; same dataset/version with changed
payload/provenance/conditions fails, rather than replacing a chart edition.

Known synthetic D50/2° → XYZ Y=1 → existing Bradford → linear RGB; no gamut clipping.
CIELAB inverse toe and cube-root round trip, white/grey/saturated checks are validation math only.
The production LibRaw ProPhoto-D65 convention is NOT reinterpreted as ICC ROMM-D50.

## Worktree preservation

All refs, remotes and worktree status were enumerated before changes. WORKTREE_STATE_BEFORE.json
retains HEAD/status/diff fingerprints. Other checkouts were never reset/stashed/merged/cleaned.
The separate color-studio closure checkout already had dirty source/test changes and was left untouched.
Any observed concurrent evolution is recorded, not overwritten.
