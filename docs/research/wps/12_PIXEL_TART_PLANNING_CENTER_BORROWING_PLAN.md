# Pixel Tart Planning Center Borrowing Plan

Status: **product recommendation**, not WPS feature verification or an implementation order. The [Evidence Manifest](17_WPS_RESEARCH_EVIDENCE_MANIFEST.md) and [Feature Matrix](08_WPS_COMPLETE_FEATURE_MATRIX.md) show that WPS UI behavior and file round trips remain `NOT VERIFIED`. The ideas below are general document and presentation patterns to evaluate. They must not be described as observed WPS solutions.

## Existing Pixel Tart baseline

The [Stage V Planning Center v1 report](../../implementation-reports/STAGE_V_PLANNING_CENTER_V1_REPORT.md) records a project overview, typed Shot list, reference links, visual direction, autosave, and Planning/Tether/Calendar/Asset Library connections. The later [Planning proposal closure](../../implementation-reports/PLANNING_PROPOSAL_FINAL_PRODUCT_CLOSURE.md) records a seven-module proposal workspace, preview, draft recovery, and raster PDF export in the production app. These are historical project reports, not a fresh acceptance run for this research package. The [installed acceptance completion report](../../implementation-reports/INSTALLED_ACCEPTANCE_RUNNER_COMPLETION.md) still marks full installed UI operation and visual review as pending.

## V1 recommendation for the current Planning Center

Keep the current project and Shot graph as the source of truth. Use this research to refine the existing V1 experience and its acceptance criteria; do not replace it with a second document, deck, or image editor.

| V1 item | Existing basis | Concrete recommendation | Acceptance evidence needed |
|---|---|---|---|
| Project brief and seven-module reading flow | Stage V and proposal closure reports | Keep a readable project summary, clear module navigation, and preview; test first-use comprehension with a photographer | Installed workspace walkthrough, screenshots, and user notes |
| Shot planning | Stage V typed Shot list and statuses | Keep structured Shot rows, current-Shot context, and reference links; make status and source identity visible | Save/reopen and Planning → Tether → Planning check with actual project data |
| Photo references | Stage V source-safety record; [Free Canvas v1 report](../../implementation-reports/FREE_CANVAS_V1_REPORT.md) | Reuse stable asset references and existing Canvas/board links; preserve aspect ratio and show missing-source state | Reopen/offline-source checks; source hashes unchanged |
| Editing and recovery | Proposal closure records autosave and draft recovery | Make save/recovery state understandable and keep destructive actions reversible where the current model supports them | Interrupted edit and reopen exercise on the installed build |
| Client handoff | Proposal closure records raster PDF at 300/216 DPI | Keep PDF as the V1 delivery path, with preview and visible raster/quality limits | Installed export, page render review, real-photo print quality check |

These are **V1 priorities for validation and refinement**, not claims that the installed product has passed user acceptance. The existing proposal closure marks production integration and automated tests as passing, while installed operation, physical DPI, real camera use, and photographer sign-off remain pending.

## Later candidates, gated by evidence and use case

| Candidate | Possible Pixel Tart adaptation | Gate before commitment |
|---|---|---|
| Page-based creative document | Project-aware blocks for brief, call sheet, and reference images | Show that current seven-module text/preview cannot satisfy a real workflow; validate pagination and migration |
| Slide/deck editing | Photo-first client narrative using existing references | Confirm a photographer workflow that needs editable slides; define model and export fidelity tests |
| Templates and themes | Reusable genre structure and brand defaults | Validate with real projects and versioned serialization |
| Review layers | Separate client, team, and internal notes | Resolve visibility/privacy and stable-ID behavior; test with real reviewers |
| DOCX/PPTX interchange | Import/export with explicit compatibility reports | Build sample files and round-trip/render comparisons; WPS-specific behavior remains `NOT VERIFIED` |

WPS ribbon density, menus, shortcuts, collaboration, and specific export fidelity remain unverified in this package. The [data model](15_PIXEL_TART_PLANNING_DATA_MODEL.md) and [roadmap](16_PIXEL_TART_PLANNING_IMPLEMENTATION_ROADMAP.md) are earlier design proposals; their proposed entities and phases are not evidence that those functions are implemented or required for current V1.
