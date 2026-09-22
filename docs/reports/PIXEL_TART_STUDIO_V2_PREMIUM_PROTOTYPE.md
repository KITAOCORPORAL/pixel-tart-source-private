# Pixel Tart Studio v2 Premium Prototype

Status: READY FOR PREMIUM VISUAL REVIEW

## Identity

- Start head: `f90772712ca351f4441ae384aaaabd2f750ce79b`
- Product source: `9fab3447a88d2b0d7f3ebff3feeaf7706118558f`
- Product commit is reachable on `integration/pixel-tart-developer-preview`.
- Evidence was regenerated from the same product source. Evidence commit is recorded after this report.

## Visual diagnosis

The old surface read as a Windows utility: stacked boxed regions, high border density, saturated action color, and a large fixed navigation footprint. Studio v2 keeps the existing workflow and native Windows behavior while changing the visual hierarchy to image → space → information → control.

## Premium shell

The shell now has a quiet custom command bar, compact menus, lightweight Windows controls, a shared-tonal sidebar, soft selected marker, and a dedicated close-safe area. Expanded navigation is 178 DIP and collapsed navigation is 56 DIP. The command bar remains draggable and keyboard/menu behavior is retained.

## Visual system

- Surface: warm-neutral dark ladder, documented in `25_DARK_TONAL_HIERARCHY.md`.
- Accent: Muted Violet v2 for review; Emerald remains available for A/B.
- Typography: optical roles rather than blanket weight; values use accent-light text.
- Slider: 4 DIP track, 20 DIP neutral thumb, quiet active/focus treatment.
- Toggle: 32 × 18 DIP compact geometry.
- Popup: 10–12 DIP radius, low shadow, soft selected wash.
- Spacing: parameter and section rhythm documented in `26_PARAMETER_RHYTHM.md`.
- Border: dividers replace repeated cards; parameter groups are not cards.

## Prototype pages

- Workbench: default, content, and compact captures; current project is visually primary and metrics are quiet.
- Asset Library: grid, selected, and inspector captures; image tiles lead and the inspector recedes.
- Reference Color: simple, professional, and film captures; source bars, segmented preview modes, canvas dominance, film controls, and responsive rails are in one workspace.

## A/B

- Shell: `01_SHELL_V1_V2.png`.
- Reference Studio v1/v2: `08_REFERENCE_V1_V2.png`.
- Emerald/Muted Violet: `09_ACCENT_AB_V2.png`.

## DPI

Evidence renders are 1920×1080 at 100%, 125%, 150%, and 200%. The Reference workspace collapses the context rail at compact widths, reflows header actions, and lets the product shell collapse navigation for the 200% viewport. Geometry audit and close-collision audit were written for every capture.

## Regression

- Full solution Release build: PASS.
- Premium prototype evidence test: PASS.
- Reference workspace wide-ratio and close-safe-area focused tests: PASS when run in isolated WPF test processes.
- Evidence test uses synthetic images and an isolated portable library; no user data is touched.

## Evidence

All deliverables are under `artifacts/studio-v2-premium-prototype/`:

`01_SHELL_V1_V2.png`, `02_WORKBENCH_V2.png`, `03_ASSET_GRID_V2.png`, `04_ASSET_SELECTED_V2.png`, `05_REFERENCE_SIMPLE_V2.png`, `06_REFERENCE_PRO_V2.png`, `07_REFERENCE_FILM_V2.png`, `08_REFERENCE_V1_V2.png`, `09_ACCENT_AB_V2.png`, and `10_DPI_REFERENCE_V2.png`.

Known issue: the repository's existing full-suite real-app test requires an explicit isolated data-root environment and was not used as a product acceptance gate. Installer was not generated.
