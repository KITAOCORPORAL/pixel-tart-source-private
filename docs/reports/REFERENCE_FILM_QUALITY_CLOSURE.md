# Reference Color + Film Quality Closure

- START_SHA: `8c2999378651cbb3704d7a80d3512a6d485d67ef`
- Product source status: implementation updated; final source SHA recorded after commit.
- Installed Candidate: NOT GENERATED.
- User QA: NOT REQUIRED.

## Workspace

Wide layout uses the intended 21/61/18 star ratio. At widths below 1440 DIP the context rail becomes a 280–340 DIP overlay and is collapsed when entering compact mode. Below 980 DIP the left rail collapses and the canvas remains primary. Focus mode collapses both rails. Simple and Pro retain the same engine state; Pro discloses protection, grain-size, Bloom, vignette, surface, texture and reference controls.

## Studio UI

The Studio slider keeps a 4 DIP track and 19 DIP warm-gray thumb. The idle accent outline was reduced to a quiet 1 DIP divider; focus remains available through the Studio focus visual. Accent values, toggle geometry and grouped parameter rhythm remain shared system resources. Film profiles and material labels are Chinese. The texture selector now binds to an explicit option model rather than raw internal identifiers.

## Reference and Film

Reference details are recorded in `REFERENCE_COLOR_ENGINE_AUDIT.md`; color spaces in `REFERENCE_FILM_COLORSPACE_AUDIT.md`. Match 0% and neutral/zero Film remain identity operations. Film runs after the 33³ reference preview; 65³ LUT export remains color-only. Grain uses continuous correlated monochrome noise, Bloom uses a neutral spatial Gaussian spread, Halation uses a warmer surrounding-edge spread, surface patterns use Pixel Tart-owned multi-scale deterministic noise, and unsupported `FrameStyle` was removed from the public v1 settings.

## Evidence and regression

Release x64 builds with zero warnings and errors. Core, modular, DPI and process-isolated Reference/Workspace gates are recorded under `artifacts/reference-film-quality-review`. Historical RC12 acceptance reports remain outside this closure and are not product-source evidence.

## Known issues

- P0: none identified by automated gates.
- P1: final visual design judgment remains the purpose of the Design Review package.
- P2: CPU Film preview can be further optimized with buffer reuse and an explicit interactive proxy tier.
