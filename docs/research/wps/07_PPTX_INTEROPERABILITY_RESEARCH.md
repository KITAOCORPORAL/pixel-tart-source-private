# PPTX Interoperability Research

Status: **PARTIAL — OOXML package analysis and Pixel Tart contract; WPS round-trip not verified**

## Package map

| Part | Role | Pixel Tart phase |
|---|---|---|
| `[Content_Types].xml` | content types | Phase 6 |
| `_rels/.rels` | package relationships | Phase 6 |
| `ppt/presentation.xml` | slide list, size, presentation properties | Phase 6 |
| `ppt/slides/slide*.xml` | slide shapes and relationships | Phase 6 |
| `ppt/slideLayouts/slideLayout*.xml` | layout geometry/placeholders | Phase 6 |
| `ppt/slideMasters/slideMaster*.xml` | master defaults | Phase 6 |
| `ppt/theme/theme*.xml` | colors/fonts/effects | Phase 6 |
| `ppt/media/*` | embedded media | Phase 6 |
| `ppt/notesSlides/*` | speaker notes | Phase 4/6 |
| `ppt/slideMasters/_rels/*`, `ppt/slides/_rels/*` | object/media links | Phase 6 |
| `ppt/presProps.xml` | presentation properties | P2 |
| `ppt/viewProps.xml` | view state | P3 |
| `ppt/tableStyles.xml` | table styles | P2 |
| `ppt/presProps.xml` animation/timing references | animation metadata | DEFER |

## Export contract

Phase 1 export: slide size, background, text boxes, images, simple shapes, tables, z-order, position, size, aspect-ratio policy, basic typography, and notes. Preserve stable Pixel Tart IDs in an extension or sidecar manifest where legal and useful; never require them for a valid PPTX.

Animations, transitions, charts, video/audio, SmartArt, and custom geometry should be explicit `Approximate` or `Unsupported` until a dedicated compatibility pass exists.

## WPS-specific gap

No WPS-created PPTX was opened or round-tripped in this run. This is standards-based planning, not UI evidence.
