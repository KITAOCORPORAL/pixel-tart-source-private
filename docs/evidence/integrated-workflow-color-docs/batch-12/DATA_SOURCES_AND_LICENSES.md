# Public sources and licensing — checked 2026-10-13 (Europe/Berlin)

## X-Rite official ColorChecker Classic CGATS

Official support: https://www.xrite.com/service-support/new_color_specifications_for_colorchecker_sg_and_classic_charts

- https://www.xrite.com/-/media/XRite/Files/Literature/Misc/C/ColorChecker24_Before_Nov2014.zip
- https://www.xrite.com/-/media/XRite/Files/Literature/Misc/C/ColorChecker24_After_Nov2014.zip
- Terms: https://www.xrite.com/legal/terms-of-use

Official support explains a regulatory/compliance-related colorant change, including greys/blacks,
for charts produced after November 2014. Use the appropriate reference edition for chart production
date; do not merge old/new values. These are generic averaged references, not individually measured
chart truth. The exact November boundary wording follows the manufacturer's file originator; do not
infer a day of manufacture from the filename.

|file|version header / date|fields / conditions|
|---|---|---|
|ColorChecker24_Before_Nov2014.txt|ColorChecker24 - Before November2014 edition; Date 3/27/2000 Time19:04|NUMBER_OF_FIELDS4; SAMPLE_NAME Lab_L Lab_a Lab_b; NUMBER_OF_SETS24; dot decimals|
|ColorChecker24_After_Nov2014.txt|ColorChecker24 - November2014 edition and newer; 4/28/2015 Time14:33|same fields/count; comma decimals; i1Pro2 instrument; M0 / no filter|

Neither original declares XYZ values, white point, illuminant or observer. No D50/2° assumption is
promoted to an observation. LGOROWLENGTH12 and unstructured vendor preambles are retained, not
claimed as sample fields. Original ZIP and TXT bytes are preserved ONLY in ignored local
`artifacts/batch12/research-originals/`, not in Git or Release payload. SHA-256s, sizes and acquisition
URLs are in DATA_SOURCES_AND_LICENSES.json; OFFICIAL_REFERENCE_RESULT.json records actual parser
execution on both files (48 patches) and refusal gates without reproducing patch values.

The ZIP files have no license document. Current X-Rite site terms restrict site contents to personal,
non-commercial use and require prior written consent for reproduction/publication/other use.
This is not a redistribution grant. Permission is `NotEstablished`, redistribution is refused by code.
No X-Rite original/derived reference values are redistributed with source, binaries or deliverable report.
Commercial reference use and redistribution require appropriate rights/consent; do not label as open data.
Raw originals remain a local research cache, not a license-compliant distributable fixture.
Webpage screenshots/color patches or a home-printed chart are NEVER measurement truth.

## Cube++ (chosen public RAW/illumination dataset)

- Published record: https://zenodo.org/records/4153431, DOI10.5281/zenodo.4153431
- Author repository: https://github.com/Visillect/CubePlusPlus
- Technical description: https://github.com/Visillect/CubePlusPlus/blob/master/description/description.md
- CC BY 4.0: https://creativecommons.org/licenses/by/4.0/ and /legalcode
- Authors: Egor Ershov, Alexey Savchik, Illya Semenkov, Nikola Banic, Alexander Belokopytov,
  Daria Senshina, Karlo Koscevic, Marko Subasic, Sven Loncaric.

Zenodo metadata and author README both specify CC BY4.0. Attribution, source, license link,
changes and no endorsement are recorded in TestData/Batch12/ATTRIBUTION.txt. Only a ~1MB PNG,
one original GT JSON, derived sparse grid and computed metrics are committed. The ~30MB CR2
is local-only; the ~200GB corpus and large archives are not committed or downloaded in full.
The sample is `00_0010`, Canon EOS550D, dataset ds_version1.0 inherited Cube+ data within Cube++.
Do not claim that it tests all newly added Cube++ scenes. SimpleCube++ original PNG is 16-bit,
648x432, downscaled camera RGB with cube rectangle removed, not display sRGB and not native RAW.

Range downloads read ZIP/ZIP64 central directory and selected members only. Strict 206/Content-Range,
CRC32 and pinned SHA-256 checks fail closed. tools/batch12/fetch_public_raw.py can retrieve the one
original RAW with no third-party Python packages. Existing mismatching destination is never replaced.
Archive Zenodo MD5s are source metadata only; we did not download entire archives to independently
verify their MD5s. Selected member CRC and SHA were actually verified. The raw example download
host for PNG0 returned404; no PNG0 or visualization JPEG was substituted as RAW truth.

## Manufacturer Polaroid research constraints (not numerical film calibration)

- Storage: https://support.polaroid.com/hc/en-us/articles/115012362747-How-should-I-store-Polaroid-film
- Temperature: https://support.polaroid.com/hc/en-us/articles/115012361067-How-does-temperature-affect-Polaroid-film
- Scan: https://support.polaroid.com/hc/en-us/articles/360001583187-How-do-I-scan-my-Polaroid-photos

Verified official pages in the browser after direct-fetch403. Paraphrase: sealed/cool/dry storage,
flat4–18°C, no freezing, at least1hour to ambient before shooting; development works best13–28°C.
Early chemical changes can continue after visible development; record scan/measurement delay.
Scanner type/glare/perspective/Newton rings matter. These are capture constraints, not spectral or
sensitometric data, and not an ICC/scanner characterization. No manufacturer images or technical
curves are redistributed. No old T-79 curve was ingested; it must not be used for modern i-Type.

## Mathematical references

CIELAB is evaluated at explicit reference white, normalized XYZ Y=1. Existing Bradford/linear RGB
implementation is reused only in validation. Documentation sources for interpretation:
https://cie.co.at/e-ilv (CIE International Lighting Vocabulary)
https://www.color.org/ (ICC color management specifications)
These links are background authorities; the actual empirical evidence here is the pinned public data,
synthetic invariants and source-specific native contract, not a new certified standards implementation.
