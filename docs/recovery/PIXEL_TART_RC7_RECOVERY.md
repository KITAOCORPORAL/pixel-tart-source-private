# Pixel Tart rc7 clean-worktree recovery

## Recovery

- Broken workspace: `pixel-tart-developer-preview-rc6` — preserved untouched as `RECOVERY_REFERENCE_ONLY`.
- New workspace: `worktrees/pixel-tart-developer-preview-rc7`.
- Clone method: fresh single-branch clone from `origin`.
- Branch: `integration/pixel-tart-developer-preview`.
- Start/remote HEAD: `c12ca359f690e8798a09a95392cc53d4009be927`.
- Solution/source tree: `RAWSelectionAssistant.sln`, `src/`, `tests/`, and `docs/` present.
- SDK: repository-hosted .NET SDK 10.0.401 (`C:\Users\Administrator\.dotnet10\dotnet.exe`).
- Restore/build baseline: PASS; Release build is 0 warnings / 0 errors.

The old rc6 directory was not reset, cleaned, restored, or deleted. No old lock file was touched.

## Current continuation result

This continuation closes two concrete correctness gaps discovered in the clean source:

1. Publishing now reports preparation, rendering, verification, and completion progress for each recipe/item instead of only reporting after a file completes.
2. Publishing preview rendering owns a cancellation token. A newer preview request cancels the older render, checks cancellation before publishing the bitmap, and never lets an obsolete result replace the latest selection.

Added tests cover the progress stages and the explicit ICC profile registry contract. Existing color profile conversion remains deliberately unavailable when a profile file is not present; the registry does not invent an ICC payload.

## Research status

Research remains evidence-led and is not an integration claim:

- OpenColorIO, Colour Science, darktable/filmic, Spektrafilm, adaptive 3D LUT, NeuralPreset, ColorTransferLib, and instant-film/Polaroid approaches remain research inputs only.
- Third-party source or shaders are not copied into production.
- Film characterization, preset rendering, and reference matching remain separate layers.
- Any future adoption requires a version-pinned license audit, color-space/transfer-function audit, deterministic fixture comparison, and a benchmark before production integration.

## Known gaps

- A native WPF visual run still requires a Windows interactive acceptance session; build and WPF automated tests are not a substitute for manual visual verification.
- The current product publishing input is still the existing JPEG/PNG/TIFF route; full RAW-to-publishing integration remains a separate task.
- Adobe RGB/Display P3/ProPhoto conversion requires an available, validated profile. The registry reports unavailable rather than tagging sRGB pixels with a different profile.
