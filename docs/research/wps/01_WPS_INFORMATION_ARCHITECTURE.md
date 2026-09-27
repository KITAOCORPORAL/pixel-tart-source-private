# WPS Information Architecture

Status: **PARTIAL — UI traversal not performed**

## Evidence rule

The tables below are a black-box test plan and a capability hypothesis, not a claim that every item was observed in WPS 12.1.0.28505. An item can be upgraded to `VERIFIED` only after manual operation in the installed Writer/Presentation applications with a screenshot or a reproducible action log.

## Writer probe map

| Surface | Probe areas | Expected user-visible controls | Status |
|---|---|---|---|
| File/backstage | New, Open, Recent, Save, Save As, Export, Print, Properties | file lifecycle, format conversion, print/PDF | NOT VERIFIED |
| Home | clipboard, font, paragraph, styles, editing | text and paragraph formatting | NOT VERIFIED |
| Insert | pages, table, picture, shape, chart, link, header/footer, symbols, equation, comment | content insertion | NOT VERIFIED |
| Layout | margins, size, orientation, columns, breaks, indents, spacing | page geometry | NOT VERIFIED |
| References | TOC, footnotes/endnotes, captions, citations | long-form navigation | NOT VERIFIED |
| Review | spell check, comments, track changes, compare, protect | editorial review | NOT VERIFIED |
| View | print/web/read layout, navigation pane, ruler, gridlines, zoom | document inspection | NOT VERIFIED |
| Sidebar/drawers | styles, navigation, comments, task panels | contextual inspection | NOT VERIFIED |
| Context menus | text, paragraph, table/cell, image, shape, page, link, comment | object-local actions | NOT VERIFIED |
| Status bar | page, word count, language, zoom, view | document state | NOT VERIFIED |
| Dialogs | font, paragraph, page setup, table properties, image properties, options | advanced configuration | NOT VERIFIED |
| Export/print | PDF, DOCX, image, printer | delivery | NOT VERIFIED |

## Presentation probe map

| Surface | Probe areas | Expected user-visible controls | Status |
|---|---|---|---|
| File/backstage | New, Open, Save, Export, Print, Package | slide lifecycle and delivery | NOT VERIFIED |
| Home | new/duplicate/layout slides, clipboard, text, shapes, arrange | slide editing | NOT VERIFIED |
| Insert | text, picture, shape, table, chart, media, hyperlink | slide content | NOT VERIFIED |
| Design | themes, variants, slide size, background | visual system | NOT VERIFIED |
| Transitions | transition category, duration, sound, advance | slide-to-slide behavior | NOT VERIFIED |
| Animations | entrance/emphasis/exit/motion, order, timing | object timelines | NOT VERIFIED |
| Slide show | present, presenter view, notes, rehearsal/timer | delivery | NOT VERIFIED |
| View | normal, sorter, notes, master, ruler, guides, grid, selection pane | composition and inspection | NOT VERIFIED |
| Context menus | slide, canvas, text, image, shape, table, group/layer | object-local actions | NOT VERIFIED |
| Sidebars/dialogs | format object, animation, selection, master, theme | advanced configuration | NOT VERIFIED |
| Status bar | slide count, language, notes, zoom | slide state | NOT VERIFIED |

## Manual traversal protocol

1. Start with a blank file and capture the initial window.
2. Visit each top-level tab left to right; capture the tab and any overflow menu.
3. Insert one object of each type, select it, and capture contextual tabs and the context menu.
4. Open advanced dialogs and record field names, defaults, units, and destructive actions.
5. Repeat with a saved DOCX/PPTX and record import warnings.
6. Store screenshots under `screenshots/writer/` or `screenshots/presentation/`; use `NOT CAPTURED` rather than synthetic images.
