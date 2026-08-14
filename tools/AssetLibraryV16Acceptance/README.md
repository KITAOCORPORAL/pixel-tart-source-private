# Asset Library V1.6 Acceptance Fixtures

This folder creates deterministic, synthetic JPEG input for Asset Library Phase 0 acceptance. It does not contain or copy customer media.

Run it only with a new or empty directory below the system temporary directory:

```powershell
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ("PixelTart_AssetLibrary_V16_Acceptance\" + [Guid]::NewGuid().ToString("N"))
& .\tools\AssetLibraryV16Acceptance\New-AssetLibraryV16Fixtures.ps1 -OutputRoot $runRoot
```

The default run creates:

- three Phase 0 JPEGs for Palette, Histogram, and Tone foreground review;
- 1,000 performance JPEGs, with the first 100 and first 1,000 declared as deterministic cohorts;
- `fixtures.manifest.json` with relative paths, dimensions, byte lengths, SHA-256 hashes, and intended fixture properties.

Safety rules enforced by the generator:

- output must be an explicit child of the system temporary directory;
- an existing non-empty output directory is rejected;
- no file is overwritten or deleted;
- generated JPEGs are newly encoded RGB images and do not copy EXIF, XMP, GPS, customer data, or source paths;
- the manifest itself contains relative paths only;
- generated corpus files are runtime evidence and must not be committed.

`icc_reference_included` and `raw_embedded_preview_included` are intentionally `false`. A non-sRGB ICC fixture is accepted only when its embedded profile and independently expected converted RGB values can both be verified. RAW remains false until a valid program-generated RAW/DNG with a real embedded preview can be exercised by the production resolver without demosaic or source writes.

## Full pipeline acceptance

Run the isolated acceptance suite only with a new or empty system-temporary child directory:

```powershell
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ("PixelTart_AssetLibrary_V16_Acceptance\Run-" + [Guid]::NewGuid().ToString("N"))
& .\tools\AssetLibraryV16Acceptance\Invoke-AssetLibraryV16Acceptance.ps1 -OutputRoot $runRoot
```

The runner references the real Preview and Core projects. Because the production WPF decoder is internal and this acceptance tooling must not change its visibility, the runner calls that one boundary through reflection. Everything after decode uses the public production services directly:

- generated JPEG bytes are decoded by `WpfVisualAnalysisDecoder`;
- decoded pixels pass through `AssetVisualAnalysisService`;
- cold and warm results pass through `SqliteAssetVisualAnalysisCache`;
- canonical rows are counted in `AssetVisualAnalysis`, `AssetVisualFeatures`, and `AssetVisualPaletteColors`;
- a new database/store instance reloads every Inspector feature;
- A and B enter real decoder work and are cancelled by selection changes; only C may publish.

The JSON output records measured durations but tests assert completeness and bounded counts, not machine-specific time thresholds. ICC and RAW remain skipped and `false` until independent fixtures exist.
