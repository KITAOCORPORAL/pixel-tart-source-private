# WPS Complete Feature Matrix

Status: **PARTIAL — inventory scaffold; all UI-dependent observations remain NOT VERIFIED**

Evidence audit: [Evidence Manifest](17_WPS_RESEARCH_EVIDENCE_MANIFEST.md). The screenshot folders contain only `.gitkeep` files; there are no WPS UI screenshots, action logs, or round-trip artifacts in this package. `NOT VERIFIED` means no evidence of WPS behavior was found here; it is not a negative capability claim. The rows below are **features to probe**, not observed controls. `Decision` and `Priority` are proposal labels for Pixel Tart and do not establish WPS parity, product implementation status, or an approved V1 scope. `VERIFIED` is limited to the recorded installation/host metadata in the version baseline and the screenshot inventory in the manifest; no feature row below qualifies.

| Product | Area | Feature | Subfeature | Observed | Evidence | Photography relevance | Decision | Priority |
|---|---|---|---|---|---|---|---|---|
| Writer | File | New/Open/Save | recent files, Save As | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | File | Export | PDF, DOCX | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | File | Print | print preview, page range | NOT VERIFIED | no UI session | Medium | ADAPT | P1 |
| Writer | Edit | Undo/Redo | transaction history | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | Edit | Find/Replace | scoped text search | NOT VERIFIED | no UI session | High | ADOPT | P1 |
| Writer | Text | Character formatting | font, size, weight, italic, underline, color | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | Text | Advanced typography | tracking, effects, case, super/subscript | NOT VERIFIED | no UI session | Medium | ADAPT/DEFER | P2 |
| Writer | Paragraph | Layout | align, indent, spacing, tabs | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | Paragraph | Lists | bullets, numbering, multilevel | NOT VERIFIED | no UI session | High | ADOPT | P1 |
| Writer | Paragraph | Decoration | borders, shading, keep rules | NOT VERIFIED | no UI session | Medium | ADAPT | P2 |
| Writer | Styles | Named styles | heading, title, custom style | NOT VERIFIED | no UI session | High | ADAPT | P0 |
| Writer | Page | Page setup | size, orientation, margins, columns, breaks | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | Header/footer | Fields | page number, date, header/footer | NOT VERIFIED | no UI session | High | ADOPT | P1 |
| Writer | Insert | Media | image, text box, shape, hyperlink | NOT VERIFIED | no UI session | High | ADAPT | P0 |
| Writer | Insert | Structured content | table, chart, symbol, equation | NOT VERIFIED | no UI session | High/Low | table ADOPT; chart/equation DEFER | P1/P3 |
| Writer | Table | Structure | rows, columns, merge, split, sort | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Writer | Table | Appearance | fill, border, width, height, autofit | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Writer | Image | Geometry | resize, crop, rotate, wrap, position | NOT VERIFIED | no UI session | High | ADAPT | P0 |
| Writer | Image | Effects | opacity, border, shadow, correction, color | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Writer | Review | Comments | add, reply, resolve | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Writer | Review | Track changes | accept/reject, compare | NOT VERIFIED | no UI session | Medium | DEFER | P3 |
| Writer | Navigation | Outline | TOC, headings, navigation pane | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Writer | Quality | Spell check | language and suggestions | NOT VERIFIED | no UI session | Medium | ADOPT via OS/API | P2 |
| Presentation | Slide | Lifecycle | new, duplicate, delete, reorder, hide, section | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Presentation | Canvas | View aids | zoom, pan, ruler, grid, guides, snap | NOT VERIFIED | no UI session | High | ADOPT | P0 |
| Presentation | Text | Typography | box, paragraph, list, spacing | NOT VERIFIED | no UI session | High | ADAPT | P0 |
| Presentation | Image | Photo handling | insert, crop, mask, fit/fill, replace | NOT VERIFIED | no UI session | Critical | ADAPT | P0 |
| Presentation | Shape | Drawing | fill, stroke, transparency, shadow | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Presentation | Arrange | Layering | order, align, distribute, group | NOT VERIFIED | no UI session | Critical | ADOPT | P0 |
| Presentation | Table | Structured layout | rows, columns, merge, styles | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Presentation | Chart | Data visualization | type, data, axis, legend | NOT VERIFIED | no UI session | Low | DEFER | P3 |
| Presentation | Media | Video/audio | playback, embed | NOT VERIFIED | no UI session | Medium | DEFER | P3 |
| Presentation | Master | Theme system | master, layout, fonts, colors | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Presentation | Transition | Slide transitions | duration, advance, sound | NOT VERIFIED | no UI session | Low | DEFER | P3 |
| Presentation | Animation | Object timelines | entrance, emphasis, exit, motion | NOT VERIFIED | no UI session | Low | DEFER | P3 |
| Presentation | Present | Delivery | slide show, presenter view, notes, timer | NOT VERIFIED | no UI session | High | ADAPT | P1 |
| Presentation | Export | Delivery | PPTX, PDF, image, video | NOT VERIFIED | no UI session | Critical | ADOPT | P0 |
| Both | Collaboration | Cloud/enterprise | sharing, real-time collaboration | NOT VERIFIED | no UI session | Medium | DEFER | P3 |
| Both | Automation | Macro/VBA | scripts and add-ins | NOT VERIFIED | no UI session | Low | IGNORE | P3 |
| Both | Commercial | Ads/membership/store | promotional surfaces | NOT VERIFIED | no UI session | None | IGNORE | P3 |
