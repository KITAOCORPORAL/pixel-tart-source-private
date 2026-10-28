# R5.3 UI correction — PARTIAL, not visually complete
Date: 2026-10-28 Asia/Shanghai. Baseline: 4e9dfc4a9b65ab78379c1fa2bf1ff514742a0bec.

## Design authority
Verified original archive SHA256: 16f75c0f40b506e4db57f140f389967264040cd8c5930ad24946b11c171e2ca9.
Read UI设计规划文档-R5.3.md, final dev-spec supplement, darkroom-r5.html, instant-r3.html, collage-r5.html and original simple/pro/instant/narrow/canvas screenshots.
Only WPF production controls used. No browser, WebView, CDP or Computer Use.

## Corrected visible layout
- Full shell: real global navigation contracts retained; darkroom host rail 40 DIP, duplicate page rail hidden. Title 24 DIP, work toolbar 34 DIP. Host menu, redundant bottom status and emergency overlay hidden for active R5.3 darkroom; menu exit and OS close retained. Native title hit-test height follows compact row.
- Parameters: no unconditional width<1440 disappearance. Effective page DIP already excludes host rail; explicit 40 DIP recovery rail, docking or drawer. Explicit open survives repeated layout. Full-height instant controls and fixed footer; scrollable parameters.
- Simple: histogram, real 38 DIP node-backed layer rows (visibility, prefix-render thumbnail, mask, name, lock), layer strength/blend, basic sections. Raster preset gallery uses <=192px production renderer with cancellation/target/version guards. Unsupported RAW gallery cards disabled rather than faked.
- Professional: same document/history, real serial nodes and selected-node controls, category buttons curve/wheel/HSL/basic; other existing tools retained in menu. Node and strip independently collapse to 22 DIP. Node visibility/lock/mask actions use existing editor APIs.
- Instant: unique left paper picker, grouped fixed three-column layout, right frame picker restored; camera/frames/common/defects/advanced sections and locked footer. Paper thumbnails use same frozen input/recipe/full production pipeline at 240px, not CSS/color-only simulation. Automatic refresh is cancellable and stale publications rejected. Entering tab and save completion restore real paper preview without changing committed history.
- Canvas: original CanvasEditor/renderer retained, left material thumbnails, middle surface, right properties over layers with splitter, inline brush controls, explicit save. No second edit host or new brush algorithms.

## Explicit remaining differences / NOT COMPLETE
- Five existing response groups only; not the approved 16 paper cards. No synthetic card multiplication or invented response differences.
- Camera photo distribution rights not confirmed. Official frame photos say reference-only even when prototype shippable=true. 12 original frame assets explicitly allow distribution but are not yet integrated through rendering/export; their omission is an implementation gap, not a licensing block. Collaboration assets excluded pending rights. See DESIGN_RESOURCE_RIGHTS.json.
- Six existing programmatic frame choices remain; this is not the approved textured frame collection or camera photography presentation.
- Compact toolbar still has some text controls, resource category/search ordering and layer/strip selection styling differ from final art direction; simple basic tools remain sectioned rather than the exact ten-row final panel.
- 3D inspector behavior remains inherited (dock/focus path), not the approved default floating source-side placement. Not claimed complete.
- Canvas is structural recovery only: existing sources and layers, not all approved ten material categories, brush presets and detailed visual polish. At 150% effective DIP, properties scroll independently; lower brush controls require scrolling.
- Full host minimum size/actual 125%/150% device DPI, physical mouse/keyboard, multi-monitor and ACM/HDR acceptance NOT_RUN. Effective-DIP offscreen captures do not substitute for native acceptance.

## Evidence interpretation
All generated UI images are OFFSCREEN WPF renders with synthetic input. Hosted images instantiate the full production app and real shell; they are not desktop captures. Original reference images are preserved in the local design delivery, not packaged as product assets. Initial failed tests/logs retained; final report must cite only sealed final runs.

## Product safety
No node algorithm, Core renderer, LibRaw/ICC/PV4/5/6 processing or default-off research gate changed. Tutorials remain retired. WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false; all modules and planning 15-section architecture retained; no Setup generation, merge, force push, reset/stash/clean or changes to other worktrees.
