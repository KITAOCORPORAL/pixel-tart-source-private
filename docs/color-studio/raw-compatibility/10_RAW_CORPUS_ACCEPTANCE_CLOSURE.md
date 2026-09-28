# RAW Corpus Acceptance Closure

Status: **PARTIAL**. This is an isolated Core path acceptance, not a production WPF RAW-to-TIFF workflow. The first batch was executed against baseline source HEAD `9ff5bf904dfb3a62dc136be9fcbde2973e05b65a` using this newly added runner. Later report-only corrections did not rerun binaries; subsequent full runs use the updated runner. The stress result in the external output directory also comes from the first runner version and uses the target as its own reference; its completion times are descriptive only, not a valid non-identity Match v3 quality gate.

- Raw independent fixtures: 349
- Core pixel-path parity PASS: 159
- Product-level full pipeline PASS: 0 (not run; no wired product route)
- Decode PASS / PARTIAL / FAIL / UNSUPPORTED: 269 / 0 / 10 / 70
- GPU acceptance: `GPU_MATCH_V4_NOT_USED_IN_PRODUCTION_ACCEPTANCE`
- Preview acceptance: Core RGB24 adapter versus TIFF16 read-back only; WPF UI preview is not claimed. A distinct RAW reference was used for the Core match; the first-run JSON does not persist the chosen reference SHA, so exact run provenance is incomplete.
- Color and metadata acceptance: partial; no embedded ICC, no complete EXIF propagation, no source native-bit-depth/CFA/black-white-level proof.
- Third-party material remains local and is not redistributed.

## Support levels

Level 0 = no complete passing sample (including unknown camera); Level 1 = one concrete Core pixel-path parity PASS sample; Level 2 = multiple independent such samples for one decoded camera model; Level 3 = multiple camera models for one format. These are Core evidence levels only; no vendor-wide universal or product-level support is inferred.

## First priorities

P0: Canon EOS R6 CR3 decoded but failed Core parity; two Fuji X-T5 RAF samples and Fuji GFX100S RAF decoded but failed Core parity. Investigate the RGB24 preview conversion and TIFF16 comparison, then wire and test the product RAW16 → Match v3 → TIFF16 route. The large sample groups require genuine camera-model-specific checks; one CR3 EOS R6 sample cannot certify all Canon CR3 cameras.

P1: Validate orientation and metadata read-back; add ICC handling and RAW source precision/CFA evidence. Recheck ARW, NEF, RAF, RW2, ORF, DNG and PEF on additional camera models.

P2: X3F/Foveon, legacy CRW/KDC/MRW and medium-format MEF/MOS are not supported by this run. Keep these distinct from mainstream format status.

## Regression

Focused Core: 88 passed, 0 failed, 1 opt-in benchmark skipped. Focused WPF: 77 passed, 0 failed. Full Core: 1460 passed, 1 failed (dark theme resource assertion unrelated to RAW), 2 skipped. Full WPF: 1345 passed, 6 failed (theme/UI evidence, PowerShell execution policy, WPF application singleton), 9 skipped. Release x64 solution and runner builds: 0 warnings, 0 errors. These failures were not changed or skipped to improve the result.

## Color Match 28-gate update

Before: **12 / 28 = 42.9%**.

After: **12 / 28 = 42.9%**.

Newly closed gates: **none**. Real corpus evidence does not close product integration gates when the tested path is a separate runner/Core adapter.

This acceptance is a baseline for subsequent compatibility fixes; it does not certify every camera model represented by a vendor folder.
