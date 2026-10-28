# Git delivery entry point

This directory contains the curated 2026-10-28 offline research delivery, uploaded at the user's request. Start with [FINAL_REPORT.md](FINAL_REPORT.md) and [README.md](README.md).

- Branch: `research/polaroid-offline-20261028`.
- Parent/source baseline: `92c6da2e7311c55a788898db07160047cc3d7051` (existing R5.3 work; not authored by this research).
- Exactly two offline **style approximations**, **zero** measured camera profiles, **zero** product integration.
- No edits under `src/`, `tests/`, product XAML or active HANDOFF. R5.3 UI remains the first priority.
- `DELIVERY_MANIFEST.json` authenticates the original curated files; `ZIP_IDENTITY.json` authenticates the original local ZIP (not checked in). Upload-only metadata has a separate scope in `GIT_UPLOAD_CHECKS.json`.
- Third-party source clones, dependencies, manuals and private photos are excluded. The MIT Lab reference execution JSON is research evidence only; its copyright notice is retained.
- Old creative recipes remain unchanged. None of the synthetic self-tests establish camera or film accuracy.
- Source paths in historical evidence describe the measured local environment. They are not portable runtime configuration or a claim that those files exist on another machine.

Portable generation requires Python 3.12 and the pinned requirements; the complete application is not built or launched by these tools. Read the explicit 8-bit/patch-level calibration limitations before use.
