# Pixel Tart Studio UI v1 — visual review (PARTIAL)

## Method and provenance

Production App/MainWindow, production styles and view models; synthetic seed data.
Gallery uses production resources inside a test-only window. No screenshots are mockups,
but none are physical desktop or installed UIA proof. Contact sheets only index full PNGs.

Initial full-size inspection at e124bc4 covered all 12 component images, Asset selected,
Planning text, Reference multi/loading/error/recovered, Tether expanded, Reference 200%,
Calendar, Toolbox and Online Selection. The final source adds only a loading regression
test, but final screenshots are regenerated rather than re-stamped. Final per-image
review metadata distinguishes inspected images from images merely captured.

## Findings

| Criterion | Observation | Disposition |
| --- | --- | --- |
| Identity | Charcoal surfaces, emerald accents, original frame/lens symbols; no traffic lights or macOS chrome | Pixel Tart identity retained |
| Precision | Shared slider/thumb/radius/segmented and button metrics are more consistent | Base implemented; all interaction states not certified |
| Image dominance | Reference target and Planning hero have clear priority at normal width | 200% reference parameters consume too much relative width, P1 |
| Asset inspector | Sections clarify source/relationships/analysis; low-frequency analysis collapsed | Lower metadata/tag density still needs review |
| Planning | Reading hierarchy and three-hero composition stay image-led | Other hero counts and long documents need full-size coverage |
| Border density | Core panels remove outlines, weaker section boundaries | Calendar/Toolbox/legacy layouts retain pronounced outlines, PARTIAL |
| Symbols | New navigation and Planning menu semantics distinct | Some legacy/glyph actions remain P2 |
| Accent | Used for active/primary/progress and focus, not glowing decoration | Legacy wide primary actions/pinned cards less restrained |
| Popup | Captured context/submenu/date/combobox surfaces are dark | Hover and every dismissal/keyboard path not certified |
| Loading | 20 DIP reserved space fixes clipped spinner; short work suppressed | Asset/RAW/legacy global consistency incomplete |
| Error/retry | Render error retains original and offers real retry; recovered result renders | Offline/reference-decode matrix not exhaustive |
| Tether | Main preview and section-based inspector retained | Expanded capture needs scrolled lower content to prove all controls |
| Focus | Accent focus gallery and peer contracts retained | Complete keyboard walkthrough remains separate |
| DPI | Four logical scales and 2K/4K renders captured | Not physical monitor testing |

## Geometry interpretation

The audit now observes wrapped text, explicit ellipsis, own bounds and clipping ancestors.
`SCROLL_REACHABLE_REQUIRES_INTERACTION_CHECK` is not a PASS: it records a scroll-container
case which needs a real reachability check. Likewise explicit ellipsis is a designed
truncation, not evidence that every value is readable without a tooltip/detail view.
The historical `PASS_RECORDED_UNWRAPPED_TEXT` field is only its narrow measured scope.
Do not promote it to the requested complete text geometry gate.

## Review outcome

PARTIAL. Existing dark UI improvements are retained and no captured popup white-surface
regression is known. Remaining high-DPI density, complete geometry/loading closure and
unreviewed interaction states prevent READY FOR DESIGN REVIEW. User approval is PENDING;
Installed Acceptance remains INCOMPLETE; Global Rollout remains PARTIAL.
