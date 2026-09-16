# Pixel Tart RC12 Release Readiness Report

## Decision

**Candidate ready for controlled physical-machine acceptance; not yet declared physical-machine complete.**

The current 2.3.0-RC12 candidate has a reproducible installer and a complete automated closure evidence chain. The remaining gate is the human checklist on ordinary photographer hardware.

## Current build and identity

- Installer: `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`
- Installer SHA-256: `FF3FDC70C81C8B433348013AB4944A6F9527AA7CF487B5676799F9BD80AACFE0`
- Exact source identity: `artifacts/releases/2.3.0/installer/rc12-ux-candidate-current-head.json`
- No RC13 work or new product feature was introduced.

## Installation test

| Operation | Result | Evidence |
|---|---|---|
| Install | PASS | `artifacts/rc12-real-user-acceptance/installer-smoke/result.json` |
| Launch | PASS; main window handle observed, title `像素蛋挞` | same result |
| Close | PASS; exit code 0 | same result |
| Uninstall | PASS; exit code 0 and directory removed | same result |
| Reinstall | PASS in a fresh directory | `installer-smoke-reinstall/result.json` |

## Automated test and screenshot evidence

- Release build: 0 warnings, 0 errors.
- Core: 1,307/1,307.
- DPI: 90/90.
- Modular harness: 14/14.
- WPF process isolation: 1,193/1,193 across 96 fixtures.
- Product Visual Harness: 71/71, including the real active Quick Loupe surface.
- Scale gate: 10K, 50K and 100K, three fresh samples per size, PASS.
- Product-language scan: 3/3.

Evidence roots:

- `artifacts/rc12-product-visual/`
- `artifacts/rc12-wpf-process-isolation/asset-library-ux-closure-current-head/`
- `artifacts/rc12-asset-library-ux-scale-current-head/`
- `artifacts/rc12-real-user-acceptance/`

## Real-machine status

The following remain open until a human operates the installed build on ordinary hardware: 100/1,000/10,000-photo import, drag/drop, viewer zoom/pan/next, Quick Loupe click and detail, context-menu hover/submenu, color-plane and Hex filtering, Inspector clarity, Inspiration Board workflows, and all 1920×1080 / 2560×1440 / 4K × 100/125/150/200% combinations.

The complete operator checklist and photographer interview prompts are in [RC12_REAL_USER_ACCEPTANCE.md](RC12_REAL_USER_ACCEPTANCE.md).

## Findings and fixes

- Fixed the invalid preview-cache test image fixture discovered during isolated acceptance.
- Fixed false-positive overlap detection for the Asset Library empty-state and Inspiration Board modal surfaces.
- Fixed evidence composition so active Quick Loupe visibly includes the real bound 1600-pixel preview popup.
- No product crash, encoding defect or automated layout blocker remains.

## Release recommendation

Distribute the installer only as an RC12 controlled acceptance candidate until the physical checklist and photographer experience record are filled with PASS/FAIL observations. After those rows are signed off, the same installer identity can be promoted to the formal Release Candidate; no new feature work is required for that promotion.
