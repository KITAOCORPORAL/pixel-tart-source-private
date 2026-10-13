# Paired digital RAW + Polaroid I-2 metadata / quality contract v1

No physical pair was created, imported or claimed in batch12. Unit-test metadata and byte blobs are
explicitly artificial and are NEVER data fixtures for film fitting. The import/QC API is programmatic;
no UI layout or user-facing button was added. `IPairedCaptureImporter` / `PairedCaptureImporter.Read`
read strict JSON with required constructor properties; duplicate keys, unknown properties, broken
JSON and missing structure reject. `Check` reports errors and warnings, not a repaired metadata record.

## Required independent stages

|stage|record / required contents|
|---|---|
|Identity|SchemaVersion1, SampleId, SceneId; RAW and scan root-relative artifact paths + SHA-256; physical capture flag|
|FilmStockMetadata|model, batch, production date and evidence, storage history, min/max storage temperature; history must include humidity/pack-open/expiry/transport if known; never invent unavailable production conditions|
|Digital exposure|camera model, timestamp/timezone, seconds, aperture, ISO, compensation, mode, illuminant and measurement evidence|
|I-2 exposure|camera exactly Polaroid I-2; same exposure fields and light evidence; record metering/flash/filter/firmware in evidence/quality notes|
|DevelopmentMetadata|ejection timestamp, actual temperature, duration seconds, shielding and handling; no fixed duration assumed for all stocks|
|ScannerMetadata|device, serial, exact settings, dpi,8/16bits, ICC file and SHA-256, auto-correction flag, scanner-characterization version and scan time|
|Measurement|MeasuredAt, method/instrument/conditions, registration and quality evidence; digital correction version separate from film and scanner versions|

Preserve unknowns in acquisition notes; required unknown fields must prevent admission for that stage,
not be filled with guessed defaults. `ProductionEvidence`/`StorageHistory` are descriptive evidence
fields, not a claim of access to factory process data. ICC existence/hash is checked, not proof the
profile is structurally valid or appropriate. RAW/scan hashes are identity checks, not native decoding
or sufficient visual quality. Future image validators must check formats, channels, clipped patches,
blur, glare/Newton rings, registration, illumination stability and usable dynamic range independently.

## Separation to avoid fitting scan bias as film response

1. Digital RAW → versioned camera input correction → scene-related comparison representation.
2. Exposure / light / film batch / development → measured instantaneous film response.
3. Physical print → separately measured scanner response / ICC → comparison representation.

Never fit a single digital→scan LUT and call it film-only. Hold out repeated exposures, batches,
illumination, scanner sessions and measurement delays. A scanner with automatic corrections or no
characterization is explicitly warned. CreativePresetPendingMeasurement remains creative/pending.
Even complete metadata/hash QC returns `CalibrationEligible=false`: this batch does NOT implement
pixel QC or approve a calibration. `IsCalibrationClaim=true` is always rejected. No fitting or LUT
export API exists here. No modern i-Type/600 precise LUT, A7M4-specific profile or T-79-based model.

## Artifact integrity and trust boundaries

Root-relative paths only; lexical traversal/absolute paths rejected. Symlink/junction/reparse path
segments refused. Missing paths, SHA mismatch, scan-before-development/measurement-before-scan,
negative/nonfinite exposure, invalid temperature chronology and schema conflict are QC errors.
No file is modified, renamed or deleted by this API. Metadata describes real-world acquisition but
cannot by itself prove that a supplied sample is genuinely physical. Independent physical evidence
review is mandatory before any downstream calibration claim.
