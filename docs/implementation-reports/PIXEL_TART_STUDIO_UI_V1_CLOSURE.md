> Historical freeze: continued by [Studio UI Global Rollout](PIXEL_TART_STUDIO_UI_GLOBAL_ROLLOUT.md).
> See [source reconciliation](STUDIO_UI_SOURCE_RECONCILIATION.md) for later Reference/Film/Close closures. The original status and test figures below remain historical.

# Pixel Tart Studio UI v1 — BLOCKED

This is a partial implementation and source-bound evidence handoff, not a release or
completed design gate. Remaining P1 gates below prevent READY FOR DESIGN REVIEW.
No installer, Candidate or Installed Acceptance Runner was generated or run.

## Git identity

- START_HEAD: `a0d7454bc8afe511056f18d0f3cebeba8756cd55` (existing local Studio work).
- START_REMOTE_HEAD: `d4e1b1e84447c2332c8e668f31a709a38ef1432d`.
- START_PRODUCT_SHA: `f505a177106a38fc23550edd87949f235238fb79`.
- NEW_PRODUCT_SOURCE_SHA: `993854ce0fd2437bd1fc05dc36c6bac316305a48`.
- FINAL_HEAD: resolve the evidence/report commit containing this document with
  `git log -1 --format=%H -- docs/implementation-reports/PIXEL_TART_STUDIO_UI_V1_CLOSURE.md`.
  A commit cannot contain its own hash; the handoff supplies the final exact value.

The source freeze includes product and tests. Later documentation and packaging do
not change product source. Prior diagnostic runs are not relabelled as final evidence.
No reset or force push. Pre-existing RC12 reports and staging directories are preserved
outside this delivery.

## Implementation

18 design-system documents establish colors, spacing, typography, radius, controls,
panels, loading, motion, accessibility and production examples. Existing dark palette
and emerald accent are retained. Segoe UI tokens coexist with legacy typography;
this is not a global font replacement.

Shared Studio controls cover Primary/Secondary/Ghost/Icon/Danger buttons, input aliases,
segmented selection, sliders, toggles, dark popup resources, panels/sheets/inspectors,
six-DIP scrollbar visuals with wider hit targets, focus and delayed busy feedback.
The internal test gallery is not added to production navigation.

36 authored 24×24 symbols use frame, node and lens shapes, with 1.5 stroke and 16/20/24
presentations. Global navigation aliases share the new geometry. Planning Lighting,
Styling and ReferenceColor use distinct symbols. See `docs/design-system/PIXEL_TART_SYMBOL_MAP.md`.

| Area | Implemented | Remaining scope |
| --- | --- | --- |
| Asset Library | Inspector information/source/relations/visual groups, visual analysis collapsed; toolbar, symbols, sliders and dark filters preserved | Dense lower inspector content; all keyboard/scroll/DPI states not reviewed |
| Planning | Existing 280 DIP list + document retained; hero layout, reduced separators, original menu symbols | Complete hero-count/state/DPI review and remaining micro-spacing |
| Reference Color | Core/advanced groups; compact sources; explicit order/remove/weight; error and retry; original/result split semantics retained | 200% logical layout still parameter-heavy; every offline/decode failure path not captured |
| Tether | Shared accordion/slider/toggle/segmented styles, reference preview binding and asynchronous close fix | Expanded content below viewport needs scroll-state evidence; physical camera untested |

Multi-reference weight input is now converted from percent to model units before
normalization (previously 50 was mixed with fractional peers). Regression tests verify
persisted proportions and disable boundary moves. Engine matching semantics are unchanged.
The editor continues to use native Windows file dialogs.

## Gate scope

| Gate | Status |
| --- | --- |
| Studio base / four sample pages | IMPLEMENTED, visual closure PARTIAL |
| Popup white surface leaks | No known leak in captured dark popup inventory; global proof PARTIAL |
| Text geometry | PARTIAL: wrapped/ellipsis/ancestor observations added; scroll reachability and complete states/DPI not certified |
| Border density | Reduced on core pages; legacy card outlines remain, PARTIAL |
| Loading | 300/1500 ms shared stages and six operation timings; global migration PARTIAL |
| Logical DPI | Four core pages at 100/125/150/200%, plus 2560×1440 and 3840×2160 renders |
| Physical DPI / camera / native file dialog acceptance | NOT TESTED |
| Accessibility | Production peer contracts; not installed UIA or complete keyboard-state certification |
| Full-size review | Recorded individually in visual review; contact sheets are only indexes |
| Installed Acceptance | INCOMPLETE; six native-dialog contracts remain separate |
| Global Rollout | PARTIAL — Studio base + four core samples |
| User Approval | PENDING |

## Validation

Final evidence belongs to `993854ce0fd2437bd1fc05dc36c6bac316305a48`.
Release x64 solution and Modular builds succeeded with 0 warnings / 0 errors.
Core: 1376 passed; Modular: 14 passed; Logical DPI: 90 passed; all zero failed/skipped.
Full WPF result and production peer checks are recorded in the final validation manifest.

The earlier e124bc4 isolated run completed 137 fixtures / 1281 tests with one failure:
EmbeddedAssetLibraryWpfTests still required immediate loading visibility. The updated
test checks initial suppression, the real busy binding and completion reset; separate
StudioLoadingTests exercise a loaded element through both timing stages. The entire
suite is rerun rather than treating a targeted retry as the full gate.

An earlier overlapping build hit locked DLLs. Sequential builds completed successfully;
this diagnostic infrastructure failure is not claimed as a successful build.

## Evidence and limits

Root: `artifacts/pixel-tart-studio-ui-v1/`. Full-size PNGs, SHA256 manifests, contact sheets,
TRX results and production peer checks accompany this report. These are actual production
App/MainWindow in-process renders with synthetic content, not desktop/installed captures.
Standard window content is 1906×1043; explicit logical-resolution images are 1920×1080,
2560×1440 and 3840×2160. No real user's photographs or workspace are used.

See [visual review](PIXEL_TART_STUDIO_UI_V1_VISUAL_REVIEW.md) and
[loading timing](GLOBAL_LOADING_TIMING_AUDIT.md).

## Known issues / next work

- P0: none observed in this scoped regression and render run; not a whole-product guarantee.
- P1: complete text/scroll/state/DPI geometry closure; narrow/high-DPI reference density;
  global loading consistency and full border-density review.
- P1 evidence: full-size review of every required state, interactive hover/pressed/focus
  matrix, expanded tether scrolled content and all reference failure/recovery states.
- P2: remaining low-frequency legacy symbols, mixed typography and micro-spacing.

Do not interpret published evidence as user approval. Finish the remaining design gates,
then request GPT/user design review; installer and whole-app rollout stay deferred.
