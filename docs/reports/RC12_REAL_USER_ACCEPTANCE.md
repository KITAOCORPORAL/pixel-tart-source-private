# Pixel Tart RC12 Real User Acceptance

## Scope

- Product: Pixel Tart 2.3.0-RC12
- Acceptance target: Asset Library UX Closure candidate
- Automated evidence source: the current closure HEAD recorded in the WPF, visual, scale and installer manifests.
- Explicitly excluded: RC13, Moodboard, Planning Center, AI, Browser Clipper and new Asset Library UX work.

## Installer acceptance

Installer: `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`

- First isolated install: PASS — exit code 0, executable present, `像素蛋挞` main window observed, clean close, uninstall exit code 0, install directory removed.
- Reinstall in a fresh isolated directory: PASS — the same checks passed again.
- SHA-256: `FF3FDC70C81C8B433348013AB4944A6F9527AA7CF487B5676799F9BD80AACFE0`.
- Raw result files: `artifacts/rc12-real-user-acceptance/installer-smoke/result.json` and `installer-smoke-reinstall/result.json`.

## Automated product evidence available for the physical pass

- WPF isolation: 96 fixtures, 1,193/1,193 passed, zero failures and zero skips.
- Product visual evidence: 71/71 real-app captures, including clean, context menu, submenu, folder tree, color filter, rating inspector, inspiration board, Loupe idle, Loupe active and full preview.
- DPI evidence: 32 captures at 100%, 125%, 150% and 200% logical scale.
- Resolution evidence: 1920×1080, 2560×1440 and 3840×2160 plus scaled variants.
- Scale evidence: fresh 10K/50K/100K fixtures, three samples per size, PASS.
- Product language scan: 3/3 tests passed; no primary UI leaks matching the repository's forbidden-term contract.

Screenshots are under `artifacts/rc12-product-visual/asset-library-ux-closure/` and the full manifest is `artifacts/rc12-product-visual/rc12-product-visual-evidence.json`.

## Physical-machine checklist

The following items require a photographer or QA operator on ordinary user hardware. They are not claimed as completed by automated WPF or logical-DPI evidence.

| Area | 1920×1080 | 2560×1440 | 4K | Status |
|---|---:|---:|---:|---|
| Asset Library import 100 / 1,000 / 10,000 | — | — | — | Awaiting hands-on run |
| Thumbnail, scroll, selection, drag/drop | — | — | — | Awaiting hands-on run |
| Landscape / portrait / ultra-wide / ultra-tall | — | — | — | Automated evidence ready; physical confirmation pending |
| Viewer original preview, zoom, pan, next | — | — | — | Awaiting hands-on run |
| Quick Loupe button, click, high-detail preview | — | — | — | Automated screenshot ready; physical confirmation pending |
| Context menu, submenu, hover and contrast | — | — | — | Automated screenshot ready; physical confirmation pending |
| Rating, tag, color, Hex and plane drag filters | — | — | — | Awaiting hands-on run |
| Inspector rating clarity / exposure | — | — | — | Awaiting hands-on run |
| Inspiration board drag, create and switch | — | — | — | Awaiting hands-on run |

DPI rows to execute on real hardware: 100%, 125%, 150% and 200% at each available monitor resolution.

## Photographer experience record

No human participant was available in this execution environment. The following prompts are therefore open observations, not fabricated results:

1. First open: can the user find 素材库 without guessing?
2. First import: is the import action self-explanatory?
3. First full preview: does the original-resolution viewer match the user's expectation?
4. First organization action: are folders, tags and the inspiration board understandable?
5. Any moment requiring “猜”: record the exact visible label, preceding action and expected wording.

## Blocking issue policy

No crash, encoding failure or automated layout blocker was found in this pass. Only reproducible physical-machine blockers should be fixed; visual redesign and new features remain out of scope.
