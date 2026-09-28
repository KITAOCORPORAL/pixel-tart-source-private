# Sony ILCE-7M4 / A7 IV ARW diagnosis

Fixture: `DSC09916.ARW`, 24,563,712 bytes, SHA256 `6F596BB234EE5DE206C66594BC84205349C363FD6A89AE188FF76DAB7C5E53B9`. MetadataExtractor reads Make `SONY`, Model `ILCE-7M4`, orientation Rotate 270 CW, ISO 400, 1/50 sec, f/4, 24 mm, capture `2026:06:29 06:10:41`. FileStream opens read-only; the first bytes are TIFF little-endian `II*`, and the source contains a JPEG-compressed 5120×3584 CFA SubIFD with strip offset 1,642,496 and byte count 22,919,824. The original and an ASCII-path copy have identical size and SHA.

| Probe | Result |
|---|---|
| Original path FileStream / SHA | PASS / manifest match |
| ASCII temporary path | same failure |
| Embedded preview (`TryDecodeEmbeddedPreviewAsync`) | NULL |
| FastPreview LibRaw | `FileUnsupported`: Failed opening file |
| ProfessionalDecode LibRaw | `FileUnsupported`: Failed opening file |
| Current native LibRaw | `0.21.1-Release` |
| `RawContext.SupportedCameras` | includes `Sony ILCE-7M4 (A7 IV)` |

Classification: **PROVEN native LibRaw rejection of this file; NOT PROVEN whether the cause is an unsupported ARW compression/variant or malformed/nonstandard file structure.** The path is not the root cause. The model being present in the capability list is not a successful decode claim. No fallback JPEG was used and no production dependency was upgraded.

The generic metadata `Compression=JPEG` / JPEG-coded CFA strip is recorded, but is not enough to claim the exact Sony RAW compression. A LibRaw upgrade requires an impact audit first; see [LIBRAW_UPGRADE_IMPACT_AUDIT.md](LIBRAW_UPGRADE_IMPACT_AUDIT.md).

Modernization status: the isolated compatibility probe is now committed, but no newer
`Sdcb.LibRaw` Windows x64 runtime or pinned candidate native DLL is available in this checkout.
Sony therefore remains **BLOCKED / candidate not run**, rather than being treated as fixed by a
source-version lookup.
